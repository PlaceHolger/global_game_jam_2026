using UnityEngine;

namespace Warmask.Ship
{
    /// <summary>
    /// Simple FPS counter for performance testing. Attach to any GameObject.
    /// Displays FPS in the corner of the screen. Logs average FPS every 5 seconds.
    /// </summary>
    public class FpsCounter : MonoBehaviour
    {
        private float _deltaTime;
        private float _logTimer;
        private int _frameCount;
        private float _fpsSum;

        private void Update()
        {
            _deltaTime += (Time.unscaledDeltaTime - _deltaTime) * 0.1f;
            _frameCount++;
            _fpsSum += 1f / _deltaTime;
            _logTimer += Time.unscaledDeltaTime;

            if (_logTimer >= 5f)
            {
                float avgFps = _fpsSum / _frameCount;
                Debug.Log($"[FPS] avg={avgFps:F1} min={1f / _deltaTime:F1} ships={ShipManagerInstance.Instance?.ShipCount ?? 0}");
                _logTimer = 0f;
                _frameCount = 0;
                _fpsSum = 0f;
            }
        }

        private void OnGUI()
        {
            int fps = Mathf.RoundToInt(1f / _deltaTime);
            GUI.Label(new Rect(10, 10, 200, 30), $"FPS: {fps}  Ships: {ShipManagerInstance.Instance?.ShipCount ?? 0}");
        }
    }
}