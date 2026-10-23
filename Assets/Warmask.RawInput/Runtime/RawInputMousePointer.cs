using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Warmask.RawInput
{
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
    /// <summary>
    /// A virtual pointer fed by one physical Windows mouse.
    /// </summary>
    public sealed class RawInputMousePointer : MonoBehaviour, Warmask.Planet.IPointerSource
    {
        [SerializeField] private float sensitivity = 1f;

        private RawInputMouseRouter _router;
        private IntPtr _device;
        private Vector2 _position;
        private SpriteRenderer _cursorRenderer;
        private Camera _camera;

        public Globals.ePlayer Player { get; private set; }
        public Vector2 ScreenPosition => _position;
        public bool ConfirmPressed => _router != null && _router.WasLeftPressed(_device);
        public bool CancelPressed => _router != null && _router.WasRightPressed(_device);
        public bool SendAllHeld => Keyboard.current != null &&
            (Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed);

        public void Initialize(RawInputMouseRouter router, IntPtr device, Globals.ePlayer player)
        {
            _router = router;
            _device = device;
            Player = player;
            _position = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            CreateCursor();
        }

        private void Awake()
        {
            _camera = Camera.main;
            _position = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        }

        private void Update()
        {
            if (_router == null)
                return;

            if (_router.ConsumeDelta(_device, out int deltaX, out int deltaY))
            {
                // Raw mouse Y is positive downward; Unity screen Y is positive upward.
                _position += new Vector2(deltaX, -deltaY) * sensitivity;
                _position.x = Mathf.Clamp(_position.x, 0f, Screen.width);
                _position.y = Mathf.Clamp(_position.y, 0f, Screen.height);
            }

            if (!_cursorRenderer)
                CreateCursor();
            if (!_camera)
                _camera = Camera.main;
            if (_cursorRenderer && TryGetWorldPoint(_camera, out Vector3 worldPoint))
                _cursorRenderer.transform.position = worldPoint;
        }

        public bool TryGetWorldPoint(Camera camera, out Vector3 worldPoint)
        {
            if (!camera)
            {
                worldPoint = default;
                return false;
            }

            worldPoint = camera.ScreenToWorldPoint(new Vector3(_position.x, _position.y, camera.nearClipPlane));
            worldPoint.z = 0f;
            return true;
        }

        private void CreateCursor()
        {
            if (_cursorRenderer || !Globals.Instance)
                return;

            Sprite sprite = Globals.Instance.ShipSprite;
            if (!sprite)
                return;

            var cursor = new GameObject($"{Player} Raw Cursor");
            cursor.transform.SetParent(transform, false);
            cursor.transform.localScale = Vector3.one * 0.6f;
            _cursorRenderer = cursor.AddComponent<SpriteRenderer>();
            _cursorRenderer.sprite = sprite;
            _cursorRenderer.color = Globals.Instance.GetPlayerColor(Player);
            _cursorRenderer.sortingOrder = 100;
        }

        private void OnDestroy()
        {
            if (_cursorRenderer)
                Destroy(_cursorRenderer.gameObject);
        }
    }
#endif
}
