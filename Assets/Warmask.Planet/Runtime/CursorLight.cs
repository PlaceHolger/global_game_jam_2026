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

        [SerializeField, Tooltip("Diameter of the player-coloured cursor ring in world units. 0 hides it.")]
        private float ringDiameter = 0.8f;

        private Light2D _light2d;
        private SpriteRenderer _ring;
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
                cursorLight.CreateRing(playerColor);
            }

            clone.SetActive(true);
            return cursorLight;
        }

        private void CreateRing(Color playerColor)
        {
            if (ringDiameter <= 0f)
                return;

            Sprite ringSprite = Resources.Load<Sprite>("Selection Ring");
            if (!ringSprite)
                return;

            var ringObject = new GameObject("Cursor Ring");
            ringObject.transform.SetParent(transform, false);
            ringObject.transform.localScale = Vector3.one * ringDiameter;

            _ring = ringObject.AddComponent<SpriteRenderer>();
            _ring.sprite = ringSprite;
            _ring.color = playerColor;
            _ring.sortingOrder = short.MaxValue;
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

            if (_ring)
                _ring.transform.localScale = Vector3.one * (ringDiameter * (1f + 0.08f * Mathf.Sin(Time.time * 3f)));
        }
    }
}
