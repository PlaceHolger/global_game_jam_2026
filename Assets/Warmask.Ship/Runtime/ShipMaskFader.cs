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

        private void Awake()
        {
            spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
            baseColors = new Color[spriteRenderers.Length];
            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                if (spriteRenderers[i])
                    baseColors[i] = spriteRenderers[i].color;
            }
            trailRenderers = GetComponentsInChildren<TrailRenderer>(true);
        }

        private void OnEnable()
        {
            isMoving = false;
            Globals.Instance.OnMaskChanged.AddListener(OnMaskChanged);
            ApplyMask(Globals.Instance.currentMask, instant: true);
        }

        public void SetType(Globals.eType newtype)
        {
            type = newtype;
            ApplyMask(Globals.Instance.currentMask, instant: true);
        }

        public void SetPlayerColor(SpriteRenderer renderer, Color color)
        {
            if (!renderer || baseColors == null)
                return;

            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                if (spriteRenderers[i] == renderer)
                {
                    baseColors[i] = color;
                    break;
                }
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
            Globals.Instance.OnMaskChanged.RemoveListener(OnMaskChanged);
        }

        private void Update()
        {
            // Local MP changes the ghost alpha at join/leave; pick that up without a mask event.
            if (type != Globals.Instance.currentMask)
            {
                float ghostAlpha = Globals.Instance.inactiveShipAlpha;
                if (!Mathf.Approximately(targetAlpha, ghostAlpha))
                    SetTarget(ghostAlpha);
            }

            if (!fading)
                return;

            fadeElapsed += Time.deltaTime;
            float t = fadeDuration > 0f ? Mathf.Clamp01(fadeElapsed / fadeDuration) : 1f;
            float eased = 1f - (1f - t) * (1f - t); // OutQuad: fast start, slow tail
            currentAlpha = Mathf.Lerp(fadeStartAlpha, targetAlpha, eased);
            ApplyAlpha(currentAlpha);

            if (t >= 1f)
                fading = false;
        }

        private void OnMaskChanged(Globals.eType newType)
        {
            ApplyMask(newType, instant: false);
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
            fading = true;
        }

        private void ApplyAlpha(float alpha)
        {
            float visibleAlpha = isMoving ? alpha : alpha * idleAlpha;

            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                SpriteRenderer sr = spriteRenderers[i];
                if (!sr) continue;

                Color baseColor = baseColors != null && i < baseColors.Length ? baseColors[i] : sr.color;
                Color nextColor = isMoving
                    ? Color.Lerp(baseColor, Color.white, moveHighlightStrength)
                    : baseColor;
                nextColor.a = visibleAlpha;
                sr.color = nextColor;
            }

            Color typeColor = Globals.Instance.GetTypeColor(type);
            if (isMoving)
                typeColor = Color.Lerp(typeColor, Color.white, moveHighlightStrength);
            typeColor.a = visibleAlpha;

            foreach (TrailRenderer tr in trailRenderers)
            {
                if (!tr) continue;
                tr.startColor = Globals.trailInPlayerColor
                    ? new Color(tr.startColor.r, tr.startColor.g, tr.startColor.b, visibleAlpha)
                    : typeColor;
            }
        }
    }
}
