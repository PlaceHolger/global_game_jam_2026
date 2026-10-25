using UnityEngine;

public class BackgroundColorChanger : MonoBehaviour
{
    // public Color mask1Color;
    // public Color mask2Color;
    public float colorModifier = 1.0f;
    public SpriteRenderer imageToChange;
    public Camera targetCamera;
    
    public bool rotateBackground = false;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (Globals.Instance != null)
        {
            Globals.Instance.OnMaskChanged.AddListener(OnMaskChanged);
            // The first mask switch may be seconds away; show the current theme now.
            OnMaskChanged(Globals.Instance.currentMask);
        }
    }

    // Uniform cover scale keeps the lit sprite material (and therefore the 2D lights) intact.
    private void LateUpdate()
    {
        if (imageToChange == null || imageToChange.sprite == null)
            return;

        Camera cam = targetCamera != null ? targetCamera : Camera.main;
        if (cam == null || !cam.orthographic)
            return;

        Vector2 spriteSize = imageToChange.sprite.bounds.size;
        if (spriteSize.x <= 0f || spriteSize.y <= 0f)
            return;

        float viewHeight = cam.orthographicSize * 2f;
        float scale = Mathf.Max(viewHeight * cam.aspect / spriteSize.x, viewHeight / spriteSize.y);

        Transform t = imageToChange.transform;
        t.localScale = new Vector3(scale, scale, 1f);

        float z = t.position.z;
        Vector3 offset = t.rotation * (imageToChange.sprite.bounds.center * scale);
        Vector3 position = cam.transform.position - offset;
        position.z = z;
        t.position = position;
    }

    private void OnDestroy()
    {
        if (Globals.Instance != null)
            Globals.Instance.OnMaskChanged.RemoveListener(OnMaskChanged);
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
