using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace Warmask.Planet
{
    /// <summary>
    /// Player-coloured light riding along with that player's cursor. It is restricted
    /// to the Background rendering layer and adds a soft, low-intensity pool, so it
    /// lifts the dark nebula without adding a white flare over ships.
    /// </summary>
    public class CursorLight : MonoBehaviour
    {
        private static Light2D _template;

        [SerializeField, Tooltip("Brightness of the cursor light on the background")]
        private float intensity = 0.15f;

        [SerializeField, Tooltip("Outer radius of the cursor light in world units")]
        private float radius = 4f;

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

            // Additive at a low intensity lifts a near-black background more than the
            // already bright ships; light layers keep it off ships/planets entirely.
            // High additive intensities were the white-flare bug, not additive itself.
            _light2d.blendStyleIndex = 1;
            // no cookie: the soft-gradient cookie reads as a flat flare sprite,
            // while cookie-less falloff reads as real radial lighting
            _light2d.lightCookieSprite = null;
            _light2d.intensity = intensity;
            _light2d.pointLightOuterRadius = radius;
            _light2d.pointLightInnerRadius = 0f;
            // Rendering layer 2 is "Background"; the background renderer opts in with
            // mask 3, while ships/planets stay on Default (1) and ignore this light.
            _light2d.renderingLayerMask = 2;
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
            PlanetInstance planet = FindAnyObjectByType<PlanetInstance>(FindObjectsInactive.Exclude);
            if (planet)
            {
                Light2D planetLight = planet.GetComponentInChildren<Light2D>(true);
                if (planetLight && !planetLight.GetComponent<CursorLight>())
                {
                    _template = planetLight;
                    return _template;
                }
            }

            Light2D[] lights = FindObjectsByType<Light2D>(FindObjectsInactive.Include);
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
