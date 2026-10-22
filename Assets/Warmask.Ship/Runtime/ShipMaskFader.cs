using UnityEngine;

namespace Warmask.Ship
{
    public class ShipMaskFader : MonoBehaviour
    {
        [SerializeField, Min(0f), Tooltip("Seconds for ships to fade between masks.")]
        private float fadeDuration = 1.5f;

        private Globals.eType type = Globals.eType.Unknown;
        private float currentAlpha = 1f;
        private float targetAlpha = 1f;
        private float fadeStartAlpha = 1f;
        private float fadeElapsed;
        private bool fading;
        private SpriteRenderer[] spriteRenderers;
        private TrailRenderer[] trailRenderers;

        private void Awake()
        {
            spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
            trailRenderers = GetComponentsInChildren<TrailRenderer>(true);
        }

        private void OnEnable()
        {
            Globals.Instance.OnMaskChanged.AddListener(OnMaskChanged);
            ApplyMask(Globals.Instance.currentMask, instant: true);
        }

        public void SetType(Globals.eType newtype)
        {
            type = newtype;
            ApplyMask(Globals.Instance.currentMask, instant: true);
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
            foreach (SpriteRenderer sr in spriteRenderers)
            {
                if (sr)
                    sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, alpha);
            }

            Color typeColor = Globals.Instance.GetTypeColor(type);
            typeColor.a = alpha;
            foreach (TrailRenderer tr in trailRenderers)
            {
                if (!tr) continue;
                tr.startColor = Globals.trailInPlayerColor
                    ? new Color(tr.startColor.r, tr.startColor.g, tr.startColor.b, alpha)
                    : typeColor;
            }
        }
    }
}
