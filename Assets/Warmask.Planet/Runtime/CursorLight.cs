using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace Warmask.Planet
{
    /// <summary>
    /// Player-coloured light riding along with that player's cursor. The look is
    /// authored in Assets/Resources/Cursor Light.prefab; this only tints and follows.
    /// </summary>
    public class CursorLight : MonoBehaviour
    {
        private const string PrefabPath = "Cursor Light";

        [SerializeField, Range(0f, 1f)]
        [Tooltip("How far the light colour is mixed toward white. 0 = pure player colour.")]
        private float whiten = 0.5f;

        private Light2D _light2d;
        private IPointerSource _pointerSource;
        private Camera _camera;

        public static CursorLight TryGetOrCreate(Transform parent, Globals.ePlayer player, IPointerSource pointerSource)
        {
            var prefab = Resources.Load<GameObject>(PrefabPath);
            if (!prefab)
                return null;

            GameObject clone = Instantiate(prefab);
            clone.name = $"Cursor Light {player}";
            if (parent)
                clone.transform.SetParent(parent, false);

            CursorLight cursorLight = clone.GetComponent<CursorLight>();
            if (!cursorLight)
                cursorLight = clone.AddComponent<CursorLight>();

            cursorLight._light2d = clone.GetComponent<Light2D>();
            cursorLight._pointerSource = pointerSource;

            if (cursorLight._light2d && Globals.Instance)
            {
                Color playerColor = Globals.Instance.GetPlayerColor(player);
                cursorLight._light2d.color = Color.Lerp(playerColor, Color.white, cursorLight.whiten);
            }

            clone.SetActive(true);
            return cursorLight;
        }

        private void Update()
        {
            if (!_camera)
                _camera = Camera.main;
            if (!_camera)
                return;

            Vector3 world;
            if (_pointerSource != null)
            {
                if (!_pointerSource.TryGetWorldPoint(_camera, out world))
                    return;
            }
            else
            {
                if (Mouse.current == null)
                    return;
                Vector2 screen = Mouse.current.position.ReadValue();
                world = _camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, _camera.nearClipPlane));
                world.z = 0f;
            }

            transform.position = world;
        }
    }
}
