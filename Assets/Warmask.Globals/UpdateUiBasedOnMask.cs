using UnityEngine;
using UnityEngine.UI;

public class UpdateUiBasedOnMask : MonoBehaviour
{
    public Image imageToChange;
    public Sprite[] sprites;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Globals.Instance.OnMaskChanged.AddListener(OnMaskChanged);
        OnMaskChanged(Globals.Instance.currentMask); //initialize on start
    }

    private void OnMaskChanged(Globals.eType newMaskType)
    {
        if (imageToChange == null || sprites.Length < 2)
            return;

        imageToChange.color = Globals.Instance.GetTypeColor(newMaskType);
        int spriteIndex = (int)newMaskType;
        if (spriteIndex < 0 || spriteIndex >= sprites.Length)
            return;
        imageToChange.sprite = sprites[(int)newMaskType];
    }
}
