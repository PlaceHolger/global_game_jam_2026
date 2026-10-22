using UnityEngine;
using UnityEngine.InputSystem;

namespace Warmask.Planet
{
    public class GamepadCursorPointer : MonoBehaviour, IPointerSource
    {
        [SerializeField] private float cursorSpeed = 900f;

        private Gamepad _gamepad;
        private Vector2 _position;
        private SpriteRenderer _cursorRenderer;
        private Camera _camera;

        public Globals.ePlayer Player => Globals.ePlayer.Player2;
        public Vector2 ScreenPosition => _position;
        public bool ConfirmPressed => _gamepad != null && _gamepad.buttonSouth.wasPressedThisFrame;
        public bool CancelPressed => _gamepad != null && _gamepad.buttonEast.wasPressedThisFrame;
        public bool SendAllHeld => _gamepad != null && _gamepad.leftTrigger.isPressed;

        public void Initialize(Gamepad gamepad)
        {
            _gamepad = gamepad;
            _position = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        }

        private void Awake()
        {
            _camera = Camera.main;
            CreateCursor();
        }

        private void Update()
        {
            if (_gamepad == null || !_gamepad.added)
                return;

            Vector2 stick = _gamepad.leftStick.ReadValue();
            if (stick.sqrMagnitude > 0.01f)
            {
                _position += stick * (cursorSpeed * Time.unscaledDeltaTime);
                _position.x = Mathf.Clamp(_position.x, 0f, Screen.width);
                _position.y = Mathf.Clamp(_position.y, 0f, Screen.height);
            }

            if (!_cursorRenderer)
                CreateCursor();
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
            if (_cursorRenderer)
                return;

            Sprite sprite = Globals.Instance.ShipSprite;
            if (!sprite)
                return;

            var cursor = new GameObject("P2 Cursor");
            cursor.transform.SetParent(transform, false);
            cursor.transform.localScale = Vector3.one * 0.6f;
            _cursorRenderer = cursor.AddComponent<SpriteRenderer>();
            _cursorRenderer.sprite = sprite;
            _cursorRenderer.color = Globals.Instance.GetPlayerColor(Globals.ePlayer.Player2);
            _cursorRenderer.sortingOrder = 100;
        }
    }
}
