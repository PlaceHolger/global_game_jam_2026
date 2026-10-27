using UnityEngine;
using UnityEngine.InputSystem;

namespace Warmask.Planet
{
    public class KeyboardCursorPointer : MonoBehaviour, IPointerSource
    {
        private Vector2 _position;
        private Camera _camera;

        public Globals.ePlayer Player => Globals.ePlayer.Player2;
        public Vector2 ScreenPosition => _position;
        public bool ConfirmPressed => Keyboard.current != null &&
            Keyboard.current.enterKey.wasPressedThisFrame;
        public bool CancelPressed => Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
        public bool SendAllHeld => Keyboard.current != null &&
            (Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed);
        public bool AddToSelectionHeld => Keyboard.current != null &&
            (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);

        private void Awake()
        {
            _camera = Camera.main;
            _position = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            Vector2 direction = Vector2.zero;
            if (keyboard.aKey.wasPressedThisFrame || keyboard.leftArrowKey.wasPressedThisFrame) direction.x -= 1f;
            if (keyboard.dKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame) direction.x += 1f;
            if (keyboard.sKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame) direction.y -= 1f;
            if (keyboard.wKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame) direction.y += 1f;

            if (!_camera)
                _camera = Camera.main;

            if (direction.sqrMagnitude > 0f &&
                CursorSnapping.TrySnap(_camera, _position, direction, out Vector2 target))
                _position = target;
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
