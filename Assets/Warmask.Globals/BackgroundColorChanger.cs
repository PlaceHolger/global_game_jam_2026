using UnityEngine;

public class BackgroundColorChanger : MonoBehaviour
{
    // public Color mask1Color;
    // public Color mask2Color;
    public float colorModifier = 1.0f;
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

        // based on modifier, adjust color brightness by lerp with white
        imageToChange.color = Color.Lerp(Color.white, Globals.Instance.GetTypeColor(arg0), colorModifier);
        
        if (arg0 == Globals.eType.TypeA)
        {
            if (rotateBackground)
                imageToChange.transform.rotation = Quaternion.Euler(0, 180, 0);
        }
        else if (arg0 == Globals.eType.TypeB)
        {
            if (rotateBackground)
                imageToChange.transform.rotation = Quaternion.Euler(0, 0, 0);
        }
    }
}
