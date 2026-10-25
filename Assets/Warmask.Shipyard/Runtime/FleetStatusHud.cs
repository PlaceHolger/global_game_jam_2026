using UnityEngine;
using Warmask.Planet;
using Warmask.Ship;

namespace Warmask.Shipyard
{
    // Temporary OnGUI overlay until phase 3 builds the real HUD.
    // P1 top-left, P2 top-right; only the owned ship count, colored per player.
    // At the fleet cap the number blinks red with a "MAX" marker.
    public class FleetStatusHud : MonoBehaviour
    {
        private const float RefreshInterval = 0.25f;
        private static FleetStatusHud _instance;

        private GUIStyle _leftStyle;
        private GUIStyle _rightStyle;
        private readonly GUIContent _leftContent = new GUIContent();
        private readonly GUIContent _rightContent = new GUIContent();
        private string _leftText = "0";
        private string _rightText = "0";
        private bool _leftMaxed;
        private bool _rightMaxed;
        private float _nextRefresh;

        public static void EnsureExists()
        {
            if (_instance) return;

            _instance = new GameObject("Fleet Status Hud").AddComponent<FleetStatusHud>();
        }

        private void Awake()
        {
            _instance = this;
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextRefresh)
                return;
            _nextRefresh = Time.unscaledTime + RefreshInterval;

            if (!Globals.Instance || !PlanetsManager.Instance || !ShipManagerInstance.Instance)
                return;

            RefreshLabel(Globals.ePlayer.Player1, ref _leftText, ref _leftMaxed, _leftContent);
            RefreshLabel(Globals.ePlayer.Player2, ref _rightText, ref _rightMaxed, _rightContent);
        }

        private static void RefreshLabel(Globals.ePlayer player, ref string text, ref bool maxed, GUIContent content)
        {
            int buffer = Globals.Instance ? Globals.Instance.fleetCapBuffer : 0;
            int cap = ShipyardInstance.GetFleetCap(player, buffer);
            int alive = ShipManagerInstance.Instance.GetPlayerShipCount((int)player);

            maxed = cap != int.MaxValue && alive >= cap;
            text = maxed ? $"{alive} MAX" : alive.ToString();
            if (content.text != text)
                content.text = text;
        }

        private void OnGUI()
        {
            if (!Globals.Instance || !PlanetsManager.Instance || !ShipManagerInstance.Instance)
                return;

            if (_leftStyle == null)
            {
                // Match the planet labels, which use spaceranger SDF (same source TTF).
                Font font = Resources.Load<Font>("spaceranger");
                _leftStyle = new GUIStyle(GUI.skin.label) { fontSize = 38, alignment = TextAnchor.MiddleLeft, font = font };
                _rightStyle = new GUIStyle(GUI.skin.label) { fontSize = 38, alignment = TextAnchor.MiddleRight, font = font };
            }

            DrawPlayerLabel(Globals.ePlayer.Player1, 20f, _leftStyle, _leftMaxed, _leftText, _leftContent);
            DrawPlayerLabel(Globals.ePlayer.Player2, Screen.width - 20f, _rightStyle, _rightMaxed, _rightText, _rightContent);
        }

        private void DrawPlayerLabel(Globals.ePlayer player, float x, GUIStyle style, bool maxed, string text, GUIContent content)
        {
            // blink between the alarm color and the player color so the number stays identifiable
            bool alarm = maxed && Mathf.Repeat((float)Time.time, 0.8f) < 0.4f;
            style.normal.textColor = alarm ? new Color(1f, 0.25f, 0.2f) : Globals.Instance.GetPlayerColor(player);

            Vector2 size = style.CalcSize(content);
            // WebGL can under-report CalcSize; the slack keeps the last glyph from clipping.
            float width = size.x + 96f;
            float left = style.alignment == TextAnchor.MiddleRight ? x - width : x;
            GUI.Label(new Rect(left, 8f, width, size.y), text, style);
        }
    }
}
