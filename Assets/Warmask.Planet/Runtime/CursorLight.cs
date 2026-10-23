using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace Warmask.Planet
{
    /// <summary>
    /// Player-colored point light riding along with that player's cursor.
    /// Clones a disabled scene point light (config: blend style, sorting layers, cookie)
    /// and moves it to the cursor so the planet normal maps read as lit spheres.
    /// Shortcut: bump shading only shows while a cursor light is near; upgrade when
    /// dedicated per-planet lights become a thing.
    /// </summary>
    public class CursorLight : MonoBehaviour
    {
        private static Light2D _template;

        [SerializeField, Tooltip("Brightness of the cursor light (subtle pointer glow)")]
        private float intensity = 12f;

        [SerializeField, Tooltip("Outer radius of the cursor light in world units")]
        private float radius = 3f;

        private Light2D _light2d;
        private IPointerSource _pointerSource;
        private Camera _camera;

        public static CursorLight TryGetOrCreate(Transform parent, Globals.ePlayer player, IPointerSource pointerSource)
        {
            Light2D template = FindTemplate();
            if (!template)
                return null;

            GameObject clone = Instantiate(template.gameObject);
            clone.name = $"Cursor Light {player}";
            clone.transform.localScale = Vector3.one;
            if (parent)
                clone.transform.SetParent(parent, false);

            CursorLight cursorLight = clone.AddComponent<CursorLight>();
            cursorLight._light2d = clone.GetComponent<Light2D>();
            cursorLight._pointerSource = pointerSource;

            // The template (planet's "Spot Light 2D") carries an Animator that plays
            // SpotHighlight, which animates the outer radius up to 3 and re-applies the
            // template's high prefab intensity. Disable it so our ApplySettings values hold.
            Animator animator = clone.GetComponent<Animator>();
            if (animator)
                Destroy(animator);

            if (Globals.Instance)
            {
                Color playerColor = Globals.Instance.GetPlayerColor(player);
                cursorLight._light2d.color = Color.Lerp(playerColor, Color.white, 0.5f);
            }

            cursorLight.ApplySettings();
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

        private void ApplySettings()
        {
            if (!_light2d)
                return;

            // Renderer2D blend style 0 is "Multiply": tints/darkens toward the light color,
            // which reads as real illumination. Style 1 is "Additive" (glow/flare).
            _light2d.blendStyleIndex = 0;
            // no cookie: the soft-gradient cookie reads as a flat flare sprite,
            // while cookie-less falloff reads as real radial lighting
            _light2d.lightCookieSprite = null;
            _light2d.intensity = intensity;
            _light2d.pointLightOuterRadius = radius;
            _light2d.pointLightInnerRadius = radius * 0.25f;
            // Only affect the Default sorting layer; UI sprites (on the "UI" layer)
            // are excluded so the cursor light never tints/brightens UI.
            _light2d.targetSortingLayers = new int[] { 0 };
        }

        private static Light2D FindTemplate()
        {
            if (_template)
                return _template;

            // Planets ship with an inactive "Spot Light 2D" child (radius 3, no cookie,
            // smooth falloff). That is the look we want at the cursor position.
            PlanetInstance planet = FindFirstObjectByType<PlanetInstance>(FindObjectsInactive.Exclude);
            if (planet)
            {
                Light2D planetLight = planet.GetComponentInChildren<Light2D>(true);
                if (planetLight && !planetLight.GetComponent<CursorLight>())
                {
                    _template = planetLight;
                    return _template;
                }
            }

            Light2D[] lights = FindObjectsByType<Light2D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Light2D light in lights)
            {
                if (!light || light.lightType != Light2D.LightType.Point)
                    continue;
                // never clone one of our own cursor lights
                if (light.GetComponent<CursorLight>())
                    continue;

                _template = light;
                return _template;
            }

            return null;
        }
    }
}
