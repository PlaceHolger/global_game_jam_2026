using UnityEngine;
using Warmask.Planet;
using Warmask.Ship;

namespace Warmask.Shipyard
{
    // Temporary OnGUI overlay until phase 3 builds the real HUD.
    // One label per player slot: odd slots down the left edge, even slots down the right.
    // At the fleet cap the number blinks red with a "MAX" marker.
    public class FleetStatusHud : MonoBehaviour
    {
        private const float RefreshInterval = 0.25f;
        private static FleetStatusHud _instance;

        private GUIStyle _leftStyle;
        private GUIStyle _rightStyle;
        private readonly GUIContent[] _contents = new GUIContent[5];
        private readonly string[] _texts = new string[5];
        private readonly bool[] _maxed = new bool[5];
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

            int count = Globals.Instance.playerCount;
            for (int i = 1; i <= count && i < _texts.Length; i++)
                RefreshLabel((Globals.ePlayer)i, ref _texts[i], ref _maxed[i], Content(i));
        }

        private GUIContent Content(int player) => _contents[player] ??= new GUIContent();

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

            int count = Globals.Instance.playerCount;
            for (int i = 1; i <= count && i < _texts.Length; i++)
                DrawPlayerLabel((Globals.ePlayer)i, i, _maxed[i], _texts[i], Content(i));
        }

        private void DrawPlayerLabel(Globals.ePlayer player, int slot, bool maxed, string text, GUIContent content)
        {
            // blink between the alarm color and the player color so the number stays identifiable
            bool alarm = maxed && Mathf.Repeat((float)Time.time, 0.8f) < 0.4f;
            bool right = slot % 2 == 0;
            GUIStyle style = right ? _rightStyle : _leftStyle;
            style.normal.textColor = alarm ? new Color(1f, 0.25f, 0.2f) : Globals.Instance.GetPlayerColor(player);

            Vector2 size = style.CalcSize(content);
            // WebGL can under-report CalcSize; the slack keeps the last glyph from clipping.
            float width = size.x + 96f;
            float x = right ? Screen.width - 20f : 20f;
            float y = slot > 2 ? Screen.height - size.y - 12f : 8f;
            float left = right ? x - width : x;
            GUI.Label(new Rect(left, y, width, size.y), text, style);
        }
    }
}
