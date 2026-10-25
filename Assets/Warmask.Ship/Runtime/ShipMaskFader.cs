using UnityEngine;

namespace Warmask.Ship
{
    public class ShipMaskFader : MonoBehaviour
    {
        [SerializeField, Min(0f), Tooltip("Seconds for ships to fade between masks.")]
        private float fadeDuration = 1.5f;

        [SerializeField, Range(0f, 1f), Tooltip("Alpha multiplier for idle ships, so moving ships stand out.")]
        private float idleAlpha = 0.8f;

        [SerializeField, Range(0f, 1f), Tooltip("How far moving ships tint toward white.")]
        private float moveHighlightStrength = 0.35f;

        [SerializeField, Range(1f, 3f), Tooltip("HDR multiplier for ships with a task, so bloom picks them up.")]
        private float moveBrightness = 1.5f;

        [SerializeField, Range(0f, 1f), Tooltip("Trail start alpha while the ship has no task.")]
        private float idleTrailAlpha = 0.2f;

        [SerializeField, Range(0f, 1f), Tooltip("Trail start alpha while the ship has a task.")]
        private float activeTrailAlpha = 0.75f;

        private Globals.eType type = Globals.eType.Unknown;
        private float currentAlpha = 1f;
        private float targetAlpha = 1f;
        private float fadeStartAlpha = 1f;
        private float fadeElapsed;
        private bool fading;
        private bool isMoving;
        private SpriteRenderer[] spriteRenderers;
        private Color[] baseColors;
        private TrailRenderer[] trailRenderers;
        private bool spritesVisible = true;
        private bool trailsVisible = true;
        private bool lastRenderingEnabled = true;
        private bool lastTrailsEnabled = true;
        private float nextFadeTick;
        private const float FadeUpdateInterval = 0.05f;

        private void Awake()
        {
            EnsureInitialized();
        }

        public void EnsureInitialized()
        {
            if (spriteRenderers != null && baseColors != null &&
                trailRenderers != null && trailRenderers.Length > 0)
                return;

            if (spriteRenderers == null || spriteRenderers.Length == 0)
            {
                spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
                baseColors = new Color[spriteRenderers.Length];
                for (int i = 0; i < spriteRenderers.Length; i++)
                {
                    if (spriteRenderers[i])
                        baseColors[i] = spriteRenderers[i].color;
                }

                spritesVisible = spriteRenderers.Length == 0 || spriteRenderers[0] == null || spriteRenderers[0].enabled;
            }

            if (trailRenderers == null || trailRenderers.Length == 0)
            {
                trailRenderers = GetComponentsInChildren<TrailRenderer>(true);
                trailsVisible = trailRenderers.Length == 0 || trailRenderers[0] == null || trailRenderers[0].enabled;
            }

            lastRenderingEnabled = ShipProfiling.EnableRendering;
            lastTrailsEnabled = ShipProfiling.EnableTrails;
        }

        private void OnEnable()
        {
            isMoving = false;
            ApplyMask(Globals.Instance.currentMask, instant: true);
        }

        public void SetType(Globals.eType newtype)
        {
            EnsureInitialized();
            type = newtype;
            ApplyMask(Globals.Instance.currentMask, instant: true);
        }

        public void SetPlayerColor(SpriteRenderer renderer, Color color)
        {
            EnsureInitialized();
            if (!renderer || baseColors == null)
                return;

            bool matched = false;
            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                if (spriteRenderers[i] == renderer)
                {
                    baseColors[i] = color;
                    matched = true;
                    break;
                }
            }

            // Fall back to tinting every sprite if the cached renderer reference does
            // not line up (prefab/pool edge cases). Otherwise ships stay untinted.
            if (!matched)
            {
                for (int i = 0; i < baseColors.Length; i++)
                    baseColors[i] = color;
            }

            ApplyAlpha(currentAlpha);
        }

        public void SetMoving(bool moving)
        {
            if (isMoving == moving)
                return;

            isMoving = moving;
            ApplyAlpha(currentAlpha);
        }

        private void OnDisable()
        {
            // Visibility is driven by Tick, so there is no per-ship mask event
            // subscription to remove. This avoids 500 callbacks on every mask switch.
        }

        public void Tick()
        {
            // A/B profiling toggles can change at runtime; reapply visibility.
            if (lastRenderingEnabled != ShipProfiling.EnableRendering ||
                lastTrailsEnabled != ShipProfiling.EnableTrails)
            {
                lastRenderingEnabled = ShipProfiling.EnableRendering;
                lastTrailsEnabled = ShipProfiling.EnableTrails;
                ApplyAlpha(currentAlpha);
            }

            // Single source of truth for mask visibility. This replaces the old
            // per-ship OnMaskChanged listener (500 delegate calls in one frame).
            float desiredAlpha = type == Globals.Instance.currentMask
                ? 1f
                : Globals.Instance.inactiveShipAlpha;

            if (!Mathf.Approximately(targetAlpha, desiredAlpha))
                SetTarget(desiredAlpha);

            if (!fading)
                return;

            if (Time.time < nextFadeTick)
                return;

            nextFadeTick = Time.time + FadeUpdateInterval;
            fadeElapsed += FadeUpdateInterval;
            float t = fadeDuration > 0f ? Mathf.Clamp01(fadeElapsed / fadeDuration) : 1f;
            float eased = 1f - (1f - t) * (1f - t); // OutQuad: fast start, slow tail
            currentAlpha = Mathf.Lerp(fadeStartAlpha, targetAlpha, eased);
            ApplyAlpha(currentAlpha);

            if (t >= 1f)
                fading = false;
        }

        private void ApplyMask(Globals.eType newType, bool instant)
        {
            float nextTarget = (newType == type && type == Globals.Instance.currentMask)
                ? 1f
                : Globals.Instance.inactiveShipAlpha;

            if (instant)
            {
                targetAlpha = nextTarget;
                currentAlpha = nextTarget;
                fading = false;
                ApplyAlpha(currentAlpha);
                return;
            }

            if (!Mathf.Approximately(nextTarget, targetAlpha))
                SetTarget(nextTarget);
        }

        private void SetTarget(float nextTarget)
        {
            targetAlpha = nextTarget;

            if (fadeDuration <= 0f || Mathf.Approximately(currentAlpha, nextTarget))
            {
                currentAlpha = nextTarget;
                fading = false;
                ApplyAlpha(currentAlpha);
                return;
            }

            fadeStartAlpha = currentAlpha;
            fadeElapsed = 0f;
            nextFadeTick = 0f;
            fading = true;
        }

        private void ApplyAlpha(float alpha)
        {
            float visibleAlpha = isMoving ? alpha : alpha * idleAlpha;

            // Fully transparent ships are not just invisible: keeping their
            // SpriteRenderer and TrailRenderer enabled still costs draw calls and
            // per-frame trail mesh updates. Disable them until they fade back in.
            if (visibleAlpha <= 0.001f)
            {
                SetRenderersVisible(false);
                return;
            }

            SetRenderersVisible(true);

            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                SpriteRenderer sr = spriteRenderers[i];
                if (!sr) continue;

                Color baseColor = baseColors != null && i < baseColors.Length ? baseColors[i] : sr.color;
                Color nextColor = isMoving
                    ? Color.Lerp(baseColor, Color.white, moveHighlightStrength) * moveBrightness
                    : baseColor;
                nextColor.a = visibleAlpha;
                sr.color = nextColor;
            }

            float trailAlpha = visibleAlpha * (isMoving ? activeTrailAlpha : idleTrailAlpha);

            foreach (TrailRenderer tr in trailRenderers)
            {
                if (!tr) continue;

                // Preserve the player RGB; only the mask alpha is driven here.
                Color trailColor = tr.startColor;
                tr.startColor = new Color(trailColor.r, trailColor.g, trailColor.b, trailAlpha);
                tr.endColor = new Color(trailColor.r, trailColor.g, trailColor.b, 0f);
            }
        }

        private void SetRenderersVisible(bool visible)
        {
            bool showSprites = visible && ShipProfiling.EnableRendering;
            bool showTrails = visible && ShipProfiling.EnableTrails;

            if (spritesVisible != showSprites)
            {
                spritesVisible = showSprites;
                for (int i = 0; i < spriteRenderers.Length; i++)
                {
                    if (spriteRenderers[i])
                        spriteRenderers[i].enabled = showSprites;
                }
            }

            if (trailsVisible != showTrails)
            {
                trailsVisible = showTrails;
                for (int i = 0; i < trailRenderers.Length; i++)
                {
                    TrailRenderer trail = trailRenderers[i];
                    if (!trail) continue;

                    trail.enabled = showTrails;
                    if (showTrails)
                        trail.Clear();
                }
            }
        }
    }
}
