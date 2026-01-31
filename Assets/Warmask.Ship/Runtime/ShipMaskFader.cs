using System;
using DG.Tweening;
using UnityEngine;

namespace Warmask.Ship
{
    public class ShipMaskFader : MonoBehaviour
    {
        [SerializeField] private Globals.eType type;

        private void OnEnable()
        {
            Globals.Instance.OnMaskChanged.AddListener( MaskChanged );
            MaskChanged(type);
        }

        private void OnDisable()
        {
            Globals.Instance.OnMaskChanged.RemoveListener( MaskChanged );
        }

        private void MaskChanged(Globals.eType newType)
        {
            Color c = Globals.Instance.GetTypeColor(type);
            c.a = (newType == type) ? 1f : 0f;
            SpriteRenderer[] spriteRenderers = GetComponentsInChildren<SpriteRenderer>();
            foreach (SpriteRenderer sr in spriteRenderers)
            {
                if (sr != null)
                {
                    sr.color = c;
                }
            }
            TrailRenderer[] trailRenderers = GetComponentsInChildren<TrailRenderer>();
            foreach (TrailRenderer tr in trailRenderers)
            {
                if (tr != null)
                {
                    tr.startColor = c;
                }
            }
        }
    }
}