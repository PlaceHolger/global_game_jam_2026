using UnityEngine;
using UnityEngine.InputSystem;

namespace Warmask.Planet
{
    public class GamepadCursorPointer : MonoBehaviour, IPointerSource
    {
        [SerializeField] private float cursorSpeed = 900f;

        private Gamepad _gamepad;
        private Vector2 _position;
        private Camera _camera;

        public Globals.ePlayer Player => Globals.ePlayer.Player2;
        public Vector2 ScreenPosition => _position;
        public bool ConfirmPressed => _gamepad != null && _gamepad.buttonSouth.wasPressedThisFrame;
        public bool CancelPressed => _gamepad != null && _gamepad.buttonEast.wasPressedThisFrame;
        public bool SendAllHeld => _gamepad != null && _gamepad.leftTrigger.isPressed;
        public bool AddToSelectionHeld => _gamepad != null && _gamepad.rightShoulder.isPressed;

        public void Initialize(Gamepad gamepad)
        {
            _gamepad = gamepad;
            _position = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        }

        private void Awake()
        {
            _camera = Camera.main;
        }

        private void Update()
        {
            if (_gamepad == null || !_gamepad.added)
                return;

            if (!_camera)
                _camera = Camera.main;

            // D-pad jumps planet to planet; the stick keeps the fine-grained drift.
            Vector2 direction = Vector2.zero;
            if (_gamepad.dpad.left.wasPressedThisFrame) direction.x -= 1f;
            if (_gamepad.dpad.right.wasPressedThisFrame) direction.x += 1f;
            if (_gamepad.dpad.down.wasPressedThisFrame) direction.y -= 1f;
            if (_gamepad.dpad.up.wasPressedThisFrame) direction.y += 1f;

            if (direction.sqrMagnitude > 0f &&
                CursorSnapping.TrySnap(_camera, _position, direction, out Vector2 target))
            {
                _position = target;
            }
            else
            {
                Vector2 stick = _gamepad.leftStick.ReadValue();
                if (stick.sqrMagnitude > 0.01f)
                {
                    _position += stick * (cursorSpeed * Time.unscaledDeltaTime);
                    _position.x = Mathf.Clamp(_position.x, 0f, Screen.width);
                    _position.y = Mathf.Clamp(_position.y, 0f, Screen.height);
                }
            }
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
    }
}
