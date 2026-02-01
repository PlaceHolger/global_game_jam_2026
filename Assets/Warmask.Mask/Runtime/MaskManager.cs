using System;
using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace Warmask.Mask
{
    public class MaskManager : MonoBehaviour
    {
        [SerializeField] Globals.eType startingMask = Globals.eType.TypeA;
        
        [SerializeField] Volume Mask1Volume;
        [SerializeField] Volume Mask2Volume;
        
        public UnityEvent OnMask1Activated;
        public UnityEvent OnMask2Activated;

        [SerializeField] private InputActionReference mask1Action;
        [SerializeField] private InputActionReference mask2Action;

        private IEnumerator Start()
        {
            yield return null; // Wait one frame
            //initialize mask based on globals
            UpdateMask(startingMask);
        }

        private void Update()
        {
            if(Keyboard.current.spaceKey.wasPressedThisFrame)
                ToggleMask();
        }

        public void ToggleMask()
        {
            ToggleMask(Globals.Instance.currentMask == Globals.eType.TypeA ? Globals.eType.TypeB : Globals.eType.TypeA);
        }
        

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
            Debug.Log($"[{nameof(MaskManager)}] Updating mask to {type}");
            Globals.Instance.currentMask = type;
            Globals.Instance.OnMaskChanged.Invoke(type);
            //tween volume weights
            if (Mask1Volume)
            {
                DOTween.To(() => Mask1Volume.weight, x => Mask1Volume.weight = x, type == Globals.eType.TypeA ? 1f : 0f,
                    0.5f);
            }

            if (Mask2Volume)
            {
                DOTween.To(() => Mask2Volume.weight, x => Mask2Volume.weight = x, type == Globals.eType.TypeB ? 1f : 0,
                    0.5f);
            }
        }

        private void OnEnable()
        {
            if (mask1Action != null) mask1Action.action.performed += ChangeMask1;
            if (mask2Action != null) mask2Action.action.performed += ChangeMask2;
        }

        private void OnDisable()
        {
            if (mask1Action != null) mask1Action.action.performed -= ChangeMask1;
            if (mask2Action != null) mask2Action.action.performed -= ChangeMask2;
        }

        private void ChangeMask1(InputAction.CallbackContext ctx)
        {
            if (ctx.phase == InputActionPhase.Started) ToggleMask(Globals.eType.TypeA);
        }

        private void ChangeMask2(InputAction.CallbackContext ctx)
        {
            if (ctx.phase == InputActionPhase.Started) ToggleMask(Globals.eType.TypeB);
        }
    }
}