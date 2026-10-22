using UnityEngine;

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
public static class RawInputSpikeLauncher
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateSpike()
    {
        if (Object.FindAnyObjectByType<Warmask.RawInput.RawInputSpike>() == null)
            new GameObject("Two Mouse Spike").AddComponent<Warmask.RawInput.RawInputSpike>();
    }
}
#endif

