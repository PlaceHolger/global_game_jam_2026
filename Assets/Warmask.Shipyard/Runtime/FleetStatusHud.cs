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
        private static FleetStatusHud _instance;

        private GUIStyle _leftStyle;
        private GUIStyle _rightStyle;

        public static void EnsureExists()
        {
            if (_instance) return;

            _instance = new GameObject("Fleet Status Hud").AddComponent<FleetStatusHud>();
        }

        private void Awake()
        {
            _instance = this;
        }

        private void OnGUI()
        {
            if (!Globals.Instance || !PlanetsManager.Instance || !ShipManagerInstance.Instance)
                return;

            if (_leftStyle == null)
            {
                _leftStyle = new GUIStyle(GUI.skin.label) { fontSize = 38, alignment = TextAnchor.MiddleLeft };
                _rightStyle = new GUIStyle(GUI.skin.label) { fontSize = 38, alignment = TextAnchor.MiddleRight };
            }

            DrawPlayerLabel(Globals.ePlayer.Player1, 20f, _leftStyle);
            DrawPlayerLabel(Globals.ePlayer.Player2, Screen.width - 20f, _rightStyle);
        }

        private void DrawPlayerLabel(Globals.ePlayer player, float x, GUIStyle style)
        {
            int buffer = Globals.Instance ? Globals.Instance.fleetCapBuffer : 0;
            int cap = ShipyardInstance.GetFleetCap(player, buffer);
            int alive = ShipManagerInstance.Instance.GetPlayerShipCount((int)player);

            bool maxed = cap != int.MaxValue && alive >= cap;
            string text = maxed ? $"{alive} MAX" : alive.ToString();

            // blink between the alarm color and the player color so the number stays identifiable
            bool alarm = maxed && Mathf.Repeat((float)Time.time, 0.8f) < 0.4f;
            style.normal.textColor = alarm ? new Color(1f, 0.25f, 0.2f) : Globals.Instance.GetPlayerColor(player);

            Vector2 size = style.CalcSize(new GUIContent(text));
            float left = style.alignment == TextAnchor.MiddleRight ? x - size.x : x;
            GUI.Label(new Rect(left, 8f, size.x, size.y), text, style);
        }
    }
}
