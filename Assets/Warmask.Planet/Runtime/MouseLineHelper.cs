using UnityEngine;
using UnityEngine.InputSystem;

public class MouseLineHelper : MonoBehaviour
{
    private static MouseLineHelper _instance;
    public static MouseLineHelper Instance
    {
        get
        {
            if (!_instance)
                _instance = FindFirstObjectByType<MouseLineHelper>();
            return _instance;
        }
    }

    public LineRenderer lineRenderer;
    public Transform startTransform;
    public Transform endTransform;
    public Gradient lineColorGradient;
    private Camera _camera;

    public void SetLineType(Globals.eType starttype, Globals.eType endtype = Globals.eType.Unknown)
    {
        var startColor = Globals.Instance.GetTypeColor(starttype);
        var endColor = Globals.Instance.GetTypeColor(endtype);
        if(starttype == endtype) //make the end color brighter if same type
            endColor = Color.Lerp(startColor, Color.white, 0.5f);
        else if(endtype == Globals.eType.Unknown)
            endColor = Color.white;
        
        //lines should change color based on type, and always end with white, therefore we update the gradient
        lineColorGradient.colorKeys = new GradientColorKey[]
        {
            new GradientColorKey(startColor, 0f),
            new GradientColorKey(endColor, 1f),
        };
        lineRenderer.colorGradient = lineColorGradient;
    }

    void Start()
    {
        _camera = Camera.main;
        if (lineRenderer)
        {
            lineRenderer.positionCount = 2;
            lineRenderer.colorGradient = lineColorGradient;
        }
    }

    void Update()
    {
        if (lineRenderer && startTransform)
        {
            lineRenderer.SetPosition(0, startTransform.position);
            if (endTransform)
            {
                lineRenderer.SetPosition(1, endTransform.position);
            }
            else
            {
                // Follow mouse cursor when end is not set
                Vector2 mousePos = Mouse.current.position.ReadValue();
                Vector3 mouseWorldPos = _camera.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, _camera.nearClipPlane));
                mouseWorldPos.z = 0f; // Assuming 2D plane
                lineRenderer.SetPosition(1, mouseWorldPos);
            }
        }
    }

    public void SetStartPos(Transform rectTransform)
    {
        startTransform = rectTransform;
        lineRenderer.enabled = startTransform;
    }

    public void SetEndPos(Transform rectTransform)
    {
        endTransform = rectTransform;
    }
}
