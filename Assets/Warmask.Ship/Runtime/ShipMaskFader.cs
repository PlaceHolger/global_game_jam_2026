using System;
using DG.Tweening;
using UnityEngine;

namespace Warmask.Ship
{
    public class ShipMaskFader : MonoBehaviour
    {
        private Globals.eType type = Globals.eType.Unknown;

        private void OnEnable()
        {
            Globals.Instance.OnMaskChanged.AddListener( MaskChanged );
            MaskChanged(Globals.Instance.currentMask);
        }

        public void SetType(Globals.eType newtype)
        {
            type = newtype;
            //MaskChanged(type);
            MaskChanged(Globals.Instance.currentMask);
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
                if (sr)
                {
                    sr.color = c;
                }
            }
            TrailRenderer[] trailRenderers = GetComponentsInChildren<TrailRenderer>();
            foreach (TrailRenderer tr in trailRenderers)
            {
                if (tr)
                {
                    tr.startColor = c;
                }
            }
        }
    }
}