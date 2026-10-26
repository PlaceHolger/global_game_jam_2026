using UnityEngine;
using UnityEngine.InputSystem;

namespace Warmask.Core
{
    /// <summary>
    /// Orthographic camera which frames whichever map is loaded and stays inside it.
    /// Bounds come from the map's <see cref="MapBounds"/>, so the core scene stays map-agnostic.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class OrthographicCameraController : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float zoomStep = 0.9f;
        [SerializeField, Min(0f)] private float gamepadPanSpeed = 12f;
        [SerializeField, Min(0f)] private float gamepadZoomSpeed = 4f;

        private Camera _camera;
        private MapBounds _bounds;
        private float _lastAspect;
        private Vector2 _lastMouseScreen;

        public Vector2 ViewCenter => transform.position;
        public float CurrentOrthoSize => _camera ? _camera.orthographicSize : 0f;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
        }

        public void SetBounds(MapBounds bounds)
        {
            if (_bounds == bounds)
                return;

            _bounds = bounds;
            if (_bounds)
                FitToBounds();
        }

        public void FitToBounds()
        {
            if (!_bounds || !_camera)
                return;

            _camera.orthographicSize = CalculateFitOrthoSize(_bounds.Size, _camera.aspect, _bounds.Padding);
            ApplyCenter(_bounds.Center);
        }

        public void SetZoom(float orthographicSize)
        {
            if (!_camera)
                return;

            _camera.orthographicSize = ClampSize(orthographicSize);
            if (_bounds)
                ApplyCenter(ViewCenter);
        }

        public void Pan(Vector2 worldDelta)
        {
            if (_bounds)
                ApplyCenter(ViewCenter + worldDelta);
        }

        public void Zoom(float wheelDelta, Vector2 screenAnchor)
        {
            if (!_camera || Mathf.Approximately(wheelDelta, 0f))
                return;

            Vector2 before = ScreenToWorld(screenAnchor);
            SetZoom(CurrentOrthoSize * Mathf.Pow(zoomStep, wheelDelta));
            Vector2 after = ScreenToWorld(screenAnchor);
            Pan(before - after);
        }

        public static float CalculateFitOrthoSize(Vector2 mapSize, float aspect, float padding)
        {
            aspect = Mathf.Max(0.01f, aspect);
            return Mathf.Max(mapSize.y * 0.5f, mapSize.x * 0.5f / aspect) + padding;
        }

        public static Vector2 ClampCenter(Vector2 center, Vector2 mapCenter, Vector2 mapSize, float orthoSize, float aspect)
        {
            aspect = Mathf.Max(0.01f, aspect);
            float halfHeight = orthoSize;
            float halfWidth = orthoSize * aspect;

            center.x = halfWidth * 2f >= mapSize.x
                ? mapCenter.x
                : Mathf.Clamp(center.x, mapCenter.x - mapSize.x * 0.5f + halfWidth, mapCenter.x + mapSize.x * 0.5f - halfWidth);
            center.y = halfHeight * 2f >= mapSize.y
                ? mapCenter.y
                : Mathf.Clamp(center.y, mapCenter.y - mapSize.y * 0.5f + halfHeight, mapCenter.y + mapSize.y * 0.5f - halfHeight);

            return center;
        }

        private void Update()
        {
            if (!_bounds)
            {
                _bounds = FindAnyObjectByType<MapBounds>();
                if (!_bounds)
                    return;

                FitToBounds();
            }

            if (!_camera)
                return;

            if (!Mathf.Approximately(_camera.aspect, _lastAspect))
            {
                _lastAspect = _camera.aspect;
                // Re-clamp the current zoom to the new aspect; don't jump back to the whole map.
                SetZoom(CurrentOrthoSize);
            }

            ReadMouse();
            ReadGamepad();
        }

        private void ReadMouse()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
                return;

            Vector2 scroll = mouse.scroll.ReadValue();
            if (!Mathf.Approximately(scroll.y, 0f))
                Zoom(scroll.y / 120f, mouse.position.ReadValue());

            Vector2 screen = mouse.position.ReadValue();
            if (mouse.middleButton.isPressed)
            {
                Vector2 previousWorld = ScreenToWorld(_lastMouseScreen);
                Vector2 currentWorld = ScreenToWorld(screen);
                Pan(currentWorld - previousWorld);
            }

            _lastMouseScreen = screen;
        }

        private void ReadGamepad()
        {
            Gamepad gamepad = Gamepad.current;
            if (gamepad == null)
                return;

            Pan(gamepad.rightStick.ReadValue() * (gamepadPanSpeed * Time.unscaledDeltaTime));

            float zoom = gamepad.dpad.ReadValue().y * gamepadZoomSpeed * Time.unscaledDeltaTime;
            if (!Mathf.Approximately(zoom, 0f))
                Zoom(zoom, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
        }

        private float ClampSize(float orthographicSize)
        {
            float fit = CalculateFitOrthoSize(_bounds.Size, _camera.aspect, _bounds.Padding);
            return Mathf.Clamp(orthographicSize, Mathf.Min(_bounds.MinOrthoSize, fit), fit);
        }

        private void ApplyCenter(Vector2 center)
        {
            if (!_camera || !_bounds)
                return;

            Vector2 clamped = ClampCenter(center, _bounds.Center, _bounds.Size, _camera.orthographicSize, _camera.aspect);
            Vector3 position = transform.position;
            transform.position = new Vector3(clamped.x, clamped.y, position.z);
        }

        private Vector2 ScreenToWorld(Vector2 screenPoint)
        {
            if (!_camera)
                return screenPoint;

            float distance = Mathf.Abs(_camera.transform.position.z);
            Vector3 world = _camera.ScreenToWorldPoint(new Vector3(screenPoint.x, screenPoint.y, distance));
            return world;
        }
    }
}
