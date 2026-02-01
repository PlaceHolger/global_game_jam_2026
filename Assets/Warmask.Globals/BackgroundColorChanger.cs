using UnityEngine;

public class BackgroundColorChanger : MonoBehaviour
{
    public Color mask1Color;
    public Color mask2Color;
    public SpriteRenderer imageToChange;
    
    public bool rotateBackground = false;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Globals.Instance.OnMaskChanged.AddListener(OnMaskChanged);
    }

    private void OnMaskChanged(Globals.eType arg0)
    {
        if (imageToChange == null)
            return;

        if (arg0 == Globals.eType.TypeA)
        {
            imageToChange.color = mask1Color;
            if (rotateBackground)
                imageToChange.transform.rotation = Quaternion.Euler(0, 180, 0);
        }
        else if (arg0 == Globals.eType.TypeB)
        {
            imageToChange.color = mask2Color;
            if (rotateBackground)
                imageToChange.transform.rotation = Quaternion.Euler(0, 0, 0);
        }
    }
}
