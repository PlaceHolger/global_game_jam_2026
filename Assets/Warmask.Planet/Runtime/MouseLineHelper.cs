using UnityEngine;
using UnityEngine.InputSystem;

namespace Warmask.Planet
{
    public class MouseLineHelper : MonoBehaviour
    {
        public LineRenderer lineRenderer;
        private Camera _camera;
        private readonly Gradient _lineGradient = new Gradient();
        private Transform _endPoint;
        
        [SerializeField]
        private bool EndPointFollowsMouseWhenNotSet = true;

        public void SetLineType(Globals.eType starttype, Globals.eType endtype = Globals.eType.Unknown)
        {
            var startColor = Globals.Instance.GetTypeColor(starttype);
            var endColor = Globals.Instance.GetTypeColor(endtype);
            if (starttype == endtype) //make the end color brighter if same type
                endColor = Color.Lerp(startColor, Color.white, 0.5f);
            else if (endtype == Globals.eType.Unknown)
                endColor = Color.white;

            //lines should change color based on type, and always end with white, therefore we update the gradient
            _lineGradient.colorKeys = new GradientColorKey[]
            {
                new GradientColorKey(startColor, 0f),
                new GradientColorKey(endColor, 1f),
            };
            lineRenderer.colorGradient = _lineGradient;
        }

        void Start()
        {
            _camera = Camera.main;
            if (lineRenderer)
            {
                lineRenderer.colorGradient = _lineGradient;
                lineRenderer.positionCount = 2;
            }
        }

        void Update()
        {
            if (EndPointFollowsMouseWhenNotSet && lineRenderer && !_endPoint)
            {
                // Follow mouse cursor when end is not set
                Vector2 mousePos = Mouse.current.position.ReadValue();
                Vector3 mouseWorldPos =
                    _camera.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, _camera.nearClipPlane));
                mouseWorldPos.z = 0f; // Assuming 2D plane
                lineRenderer.SetPosition(1, mouseWorldPos);
            }
        }

        public void SetStartPos(Transform rectTransform)
        {
            if(rectTransform)
                lineRenderer.SetPosition(0, rectTransform.position);
            if(EndPointFollowsMouseWhenNotSet || !rectTransform)
                lineRenderer.enabled = rectTransform;
        }

        public void SetEndPos(Transform rectTransform)
        {
            _endPoint = rectTransform;
            lineRenderer.enabled = rectTransform;
            if (rectTransform)
                lineRenderer.SetPosition(1, rectTransform.position);
        }
    }
}