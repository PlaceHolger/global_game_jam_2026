using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Warmask.Mask
{
    public class MaskManager : MonoBehaviour
    {
        [SerializeField] Volume Mask1Volume;
        [SerializeField] Volume Mask2Volume;

        public void ToggleMask(Globals.eType type)
        {
            if (Globals.Instance.currentMask != type)
                UpdateMask(type);
        }

        public void Mask1Activate(bool value)
        {
            if (value)
                ToggleMask(Globals.eType.TypeA);
            else
                ToggleMask(Globals.eType.TypeB);
        }

        private void UpdateMask(Globals.eType type)
        {
            Globals.Instance.OnMaskChanged.Invoke(type);
            //tween volume weights
            if (Mask1Volume && Mask2Volume)
            {
                DOTween.To(() => Mask1Volume.weight, x => Mask1Volume.weight = x, type == Globals.eType.TypeA ? 1f : 0f,
                    0.5f);
                DOTween.To(() => Mask2Volume.weight, x => Mask2Volume.weight = x, type == Globals.eType.TypeB ? 1f : 0,
                    0.5f);
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                ToggleMask(Globals.eType.TypeA);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                ToggleMask(Globals.eType.TypeB);
            }
        }
    }
}