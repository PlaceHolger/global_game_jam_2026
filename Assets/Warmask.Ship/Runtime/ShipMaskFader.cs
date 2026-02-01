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
            Color typeColor = Globals.Instance.GetTypeColor(type);
            typeColor.a = (newType == type && type == Globals.Instance.currentMask) ? 1f : 0f;
            SpriteRenderer[] spriteRenderers = GetComponentsInChildren<SpriteRenderer>();
            foreach (SpriteRenderer sr in spriteRenderers)
            {
                if (sr)
                {
                    if(Globals.trailInPlayerColor)
                        sr.color = typeColor;
                    else 
                        sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, typeColor.a);  ////for sprites just set color directly, we keep the player color intact
                }
            }
            TrailRenderer[] trailRenderers = GetComponentsInChildren<TrailRenderer>();
            foreach (TrailRenderer tr in trailRenderers)
            {
                if (tr)
                {
                    if (Globals.trailInPlayerColor)
                    {
                        //for trails just set color directly, we keep the player color intact
                        tr.startColor = new Color(tr.startColor.r, tr.startColor.g, tr.startColor.b, typeColor.a);
                    }
                    else
                    {
                        tr.startColor = typeColor;
                    }
                }
            }
        }
    }
}