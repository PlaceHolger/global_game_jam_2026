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
        private IPointerSource _pointerSource;
        private int _orderCount = -1;
        private Color _playerColor = Color.white;
        private GUIStyle _previewStyle;

        [SerializeField]
        private bool EndPointFollowsMouseWhenNotSet = true;

        private const float MinOrderLabelLineLengthScreenHeight = 0.1f;

        public void SetPointer(IPointerSource pointerSource)
        {
            _pointerSource = pointerSource;
        }

        public void SetPlayerColor(Color color)
        {
            _playerColor = color;
        }

        public void SetOrderCount(int count)
        {
            _orderCount = Mathf.Max(0, count);
        }

        public void ClearOrderPreview()
        {
            _orderCount = -1;
        }

        public void SetLineType(Globals.eType starttype, Globals.eType endtype = Globals.eType.Unknown)
        {
            if (!lineRenderer)
                return;

            Color startColor;
            Color endColor;

            if (_playerColor == Color.white && Globals.Instance)
            {
                // Used by the AI debug line, which has no player color of its own.
                startColor = Globals.Instance.GetTypeColor(starttype);
                endColor = Globals.Instance.GetTypeColor(endtype);
                if (starttype == endtype)
                    endColor = Color.Lerp(startColor, Color.white, 0.5f);
                else if (endtype == Globals.eType.Unknown)
                    endColor = Color.white;
            }
            else
            {
                // Keep each player's ray clearly colored instead of fading it to white.
                startColor = Saturate(_playerColor);
                endColor = Color.Lerp(startColor, Color.black, 0.2f);
            }

            _lineGradient.SetKeys(
                new[]
                {
                    new GradientColorKey(startColor, 0f),
                    new GradientColorKey(endColor, 1f),
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.35f, 1f),
                });
            lineRenderer.colorGradient = _lineGradient;
        }

        private static Color Saturate(Color color)
        {
            Color.RGBToHSV(color, out float hue, out float saturation, out float value);
            return Color.HSVToRGB(hue, Mathf.Clamp01(saturation + 0.25f), value * 0.75f);
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
            if (!EndPointFollowsMouseWhenNotSet || !lineRenderer || _endPoint)
                return;

            Vector2 screenPosition;
            if (_pointerSource != null)
            {
                screenPosition = _pointerSource.ScreenPosition;
            }
            else
            {
                if (Mouse.current == null)
                    return;
                screenPosition = Mouse.current.position.ReadValue();
            }

            if (!_camera)
                _camera = Camera.main;
            if (!_camera)
                return;

            Vector3 worldPosition =
                _camera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, _camera.nearClipPlane));
            worldPosition.z = 0f; // Assuming 2D plane
            lineRenderer.SetPosition(1, worldPosition);
        }

        public void SetStartPos(Transform rectTransform)
        {
            if (rectTransform)
            {
                _endPoint = null; // a new selection follows the cursor again
                lineRenderer.SetPosition(0, rectTransform.position);
                if (EndPointFollowsMouseWhenNotSet)
                    lineRenderer.SetPosition(1, rectTransform.position);
            }
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

        private void OnGUI()
        {
            if (_orderCount < 0 || !lineRenderer || !lineRenderer.enabled)
                return;
            if (Event.current.type != EventType.Repaint)
                return;

            if (!_camera)
                _camera = Camera.main;
            if (!_camera)
                return;

            Vector3 startScreen = _camera.WorldToScreenPoint(lineRenderer.GetPosition(0));
            Vector3 endScreen = _camera.WorldToScreenPoint(lineRenderer.GetPosition(1));
            if (startScreen.z < 0f || endScreen.z < 0f)
                return;
            float minLineLength = Mathf.Max(60f, Screen.height * MinOrderLabelLineLengthScreenHeight);
            if (Vector3.Distance(startScreen, endScreen) < minLineLength)
                return;

            Vector3 screenPosition = Vector3.Lerp(startScreen, endScreen, 0.75f);

            string text = _orderCount > 0 ? _orderCount.ToString() : "No ships to send";

            if (_previewStyle == null)
            {
                _previewStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 20,
                    fontStyle = FontStyle.Bold
                };
                _previewStyle.normal.textColor = Color.white;
            }

            Vector2 size = _previewStyle.CalcSize(new GUIContent(text));
            var rect = new Rect(
                screenPosition.x - size.x * 0.5f,
                Screen.height - screenPosition.y - size.y * 0.5f,
                size.x,
                size.y);

            Color previousColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.9f);
            GUI.Label(new Rect(rect.x - 2f, rect.y, rect.width, rect.height), text, _previewStyle);
            GUI.Label(new Rect(rect.x + 2f, rect.y, rect.width, rect.height), text, _previewStyle);
            GUI.Label(new Rect(rect.x, rect.y - 2f, rect.width, rect.height), text, _previewStyle);
            GUI.Label(new Rect(rect.x, rect.y + 2f, rect.width, rect.height), text, _previewStyle);
            GUI.color = Color.white;
            GUI.Label(rect, text, _previewStyle);
            GUI.color = previousColor;
        }
    }
}
