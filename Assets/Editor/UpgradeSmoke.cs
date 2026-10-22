using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Warmask.Editor
{
    public static class UpgradeSmoke
    {
        private static readonly string[] Scenes =
        {
            "Assets/Warmask.Scenes/Menu.unity",
            "Assets/Warmask.Scenes/SampleScene.unity"
        };

        [MenuItem("Warmask/Smoke/Check Scenes")]
        public static void CheckScenes()
        {
            int failures = 0;

            foreach (string path in Scenes)
            {
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

                int missing = 0;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (Component component in root.GetComponentsInChildren<Component>(true))
                    {
                        if (component == null) missing++;
                    }
                }

                Debug.Log($"[Smoke] {path}: roots={scene.rootCount} missingScripts={missing}");
                if (missing > 0) failures++;
            }

            if (Camera.main == null)
            {
                Debug.LogError("[Smoke] No main camera after loading scenes.");
                failures++;
            }

            if (failures > 0) throw new Exception($"[Smoke] {failures} scene check(s) failed.");
            Debug.Log("[Smoke] All scene checks passed.");
        }

        [MenuItem("Warmask/Build/Windows")]
        public static void BuildWindows()
        {
            string output = Path.GetFullPath("Builds/upgrade-smoke/Warmask.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(output) ?? ".");

            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(options);
            Debug.Log($"[Build] {report.summary.result} -> {output} ({report.summary.totalSize} bytes)");
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new Exception($"[Build] failed: {report.summary.result}");
        }
    }
}
