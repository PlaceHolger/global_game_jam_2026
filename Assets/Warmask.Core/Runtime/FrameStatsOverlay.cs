// Unity 6.7 defines DEBUG and DEVELOPMENT_BUILD for a Development Build even
// with Script Debugging off, but NOT UNITY_INCLUDE_INSTRUMENTATION (verified in
// the Web player .rsp). Release defines none of these.
#if DEBUG || UNITY_EDITOR
using System.Text;
using Unity.Profiling;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using UnityEngine.Profiling;

namespace Warmask.Core
{
    /// <summary>
    /// Dev-build only frame stats overlay. Shows FPS, frame timing and GC
    /// allocations so a browser build reports its own numbers without the
    /// Profiler attached. Compiled out of release players.
    /// </summary>
    [AutoStaticsCleanup]
    public partial class FrameStatsOverlay : MonoBehaviour
    {
        public static FrameStatsOverlay Instance { get; private set; }

        public float Fps { get; private set; }
        public int SampleCount { get; private set; }

        private const float RefreshInterval = 0.5f;

        private readonly FrameTiming[] timings = new FrameTiming[1];
        private readonly StringBuilder builder = new StringBuilder(192);
        private readonly GUIContent content = new GUIContent(string.Empty);

        private ProfilerRecorder gcAlloc;
        private GUIStyle style;
        private int frames;
        private float elapsed;
        private float nextRefresh;
        private bool visible;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if (Instance)
                return;

            var host = new GameObject("Frame Stats Overlay") { hideFlags = HideFlags.DontSave };
            DontDestroyOnLoad(host);
            host.AddComponent<FrameStatsOverlay>();
        }

        private void Awake()
        {
            Instance = this;
            gcAlloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc");
            Debug.Log("[FrameStats] F2 toggles the frame stats overlay.");
        }

        private void OnDestroy()
        {
            gcAlloc.Dispose();
            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
            // The manager only records the frames it was asked to capture.
            FrameTimingManager.CaptureFrameTimings();

            frames++;
            elapsed += Time.unscaledDeltaTime;
            if (Time.unscaledTime < nextRefresh)
                return;

            Fps = frames / Mathf.Max(elapsed, 0.0001f);
            frames = 0;
            elapsed = 0f;
            nextRefresh = Time.unscaledTime + RefreshInterval;

            double cpuMs = 0, mainMs = 0, renderMs = 0, gpuMs = 0;
            if (FrameTimingManager.GetLatestTimings(1, timings) > 0)
            {
                cpuMs = timings[0].cpuFrameTime;
                mainMs = timings[0].cpuMainThreadFrameTime;
                renderMs = timings[0].cpuRenderThreadFrameTime;
                gpuMs = timings[0].gpuFrameTime;
            }

            long gcBytes = gcAlloc.Valid ? gcAlloc.CurrentValue : 0;

            builder.Clear();
            builder.Append("FPS ").Append(Fps.ToString("F1"));
            builder.Append("   CPU ").Append(cpuMs > 0 ? cpuMs.ToString("F2") : "n/a").Append(" ms");
            builder.Append("   GPU ").Append(gpuMs > 0 ? gpuMs.ToString("F2") : "n/a").Append(" ms");
            builder.Append('\n').Append("Main ").Append(mainMs.ToString("F1")).Append(" ms");
            builder.Append("   Render ").Append(renderMs.ToString("F1")).Append(" ms");
            builder.Append("   GC ").Append((gcBytes / 1024f).ToString("F1")).Append(" KB/frame");
            builder.Append("   Mem ").Append((Profiler.GetMonoUsedSizeLong() / 1048576f).ToString("F0")).Append(" MB");

            content.text = builder.ToString();
            SampleCount++;
        }

        private void OnGUI()
        {
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.F2)
            {
                visible = !visible;
                Event.current.Use();
            }

            if (!visible)
                return;

            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 14,
                    alignment = TextAnchor.LowerLeft
                };
            }

            GUI.Label(new Rect(20f, Screen.height - 64f, 900f, 44f), content, style);
        }
    }
}
#endif
