using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;

public class MaskManager : MonoBehaviour
{
    [SerializeField]
    UnityEvent OnMask1Activated;
    [SerializeField]
    UnityEvent OnMask2Activated;
    [SerializeField]
    Volume Mask1Volume;
    [SerializeField]
    Volume Mask2Volume;
    
    Globals.eMask currentMask = Globals.eMask.None;
    
    public void ToggleMask(Globals.eMask mask)
    {
        if (currentMask == Globals.eMask.Mask1)
            ActivateMask1();
        else //if (currentMask == Globals.eMask.Red)  //also handles none case
            ActivateMask2();
    }
    
    public void Mask1Activate(bool value)
    {
        if (value)
            ActivateMask1();
        else
            ActivateMask2();
    }

    public void ActivateMask1()
    {
        currentMask = Globals.eMask.Mask1;
        OnMask1Activated?.Invoke();
        //tween volume weights
        if (Mask1Volume && Mask2Volume)
        {
            DOTween.To(() => Mask1Volume.weight, x => Mask1Volume.weight = x, 0f, 0.5f);
            DOTween.To(() => Mask2Volume.weight, x => Mask2Volume.weight = x, 1f, 0.5f);
        }
    }

    public void ActivateMask2()
    {
        currentMask = Globals.eMask.Mask2;
        OnMask2Activated?.Invoke();
        if (Mask1Volume && Mask2Volume)
        {
            DOTween.To(() => Mask1Volume.weight, x => Mask1Volume.weight = x, 1f, 0.5f);
            DOTween.To(() => Mask2Volume.weight, x => Mask2Volume.weight = x, 0f, 0.5f);
        }
    }
}
