using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Warmask.Ship;

namespace Warmask.Planet
{
    /// <summary>
    /// Counts the things the end screen reports. Created by PlanetsManager after
    /// the map's homes are assigned, so setup hand-offs are not captures.
    /// </summary>
    public class MatchStats : MonoBehaviour
    {
        public static MatchStats Instance { get; private set; }
        public static Globals.ePlayer Winner { get; private set; }

        private sealed class Row
        {
            public int captured;
            public int lost;
            public int sent;
            public int shipsLost;
        }

        private readonly Dictionary<Globals.ePlayer, Row> _rows = new();
        private PlanetsManager _planets;
        private float _startedAt;
        private float _endedAt = -1f;
        private bool _initialized;

        public float Elapsed => (_endedAt >= 0f ? _endedAt : Time.unscaledTime) - _startedAt;

        public static void Ensure(PlanetsManager planets)
        {
            if (Instance)
            {
                Instance.Bind(planets);
                return;
            }

            var go = new GameObject("Match Stats");
            Instance = go.AddComponent<MatchStats>();
            Instance.Bind(planets);
        }

        private void Awake()
        {
            Instance = this;
            Winner = Globals.ePlayer.None;
            _startedAt = Time.unscaledTime;
        }

        private void Bind(PlanetsManager planets)
        {
            if (_initialized || !planets)
                return;

            _initialized = true;
            _planets = planets;
            foreach (Globals.ePlayer player in MatchConfig.ActivePlayers())
                RowFor(player);

            foreach (PlanetInstance planet in planets.GetAllPlanets())
                planet.OwnerChanged += OnOwnerChanged;

            TroopMovementManager.OnTroopMovementStarted.AddListener(OnTroopSent);
            ShipInstance.ShipDestroyed += OnShipDestroyed;
        }

        private void OnDestroy()
        {
            if (_planets)
            {
                foreach (PlanetInstance planet in _planets.GetAllPlanets())
                    if (planet)
                        planet.OwnerChanged -= OnOwnerChanged;
            }

            TroopMovementManager.OnTroopMovementStarted.RemoveListener(OnTroopSent);
            ShipInstance.ShipDestroyed -= OnShipDestroyed;
            if (Instance == this)
                Instance = null;
        }

        private void OnOwnerChanged(Globals.ePlayer oldOwner, Globals.ePlayer newOwner)
        {
            if (oldOwner != Globals.ePlayer.None)
                RowFor(oldOwner).lost++;
            if (newOwner != Globals.ePlayer.None)
                RowFor(newOwner).captured++;
        }

        private void OnTroopSent(PlanetInstance from, PlanetInstance to, int count, Globals.ePlayer player)
        {
            if (player != Globals.ePlayer.None)
                RowFor(player).sent += count;
        }

        private void OnShipDestroyed(Globals.ePlayer player)
        {
            if (player != Globals.ePlayer.None)
                RowFor(player).shipsLost++;
        }

        public static void NoteWinner(Globals.ePlayer winner)
        {
            Winner = winner;
            if (Instance)
                Instance._endedAt = Time.unscaledTime;
        }

        public static string BuildReport()
        {
            var text = new StringBuilder();
            text.Append("<b>MATCH TIME  ").Append(FormatTime(Instance ? Instance.Elapsed : 0f)).Append("</b>");
            text.Append("      WINNER  ").Append(WinnerName(Winner));
            text.Append("      DIFFICULTY  ").Append(MatchConfig.Difficulty.ToString("0.0"));
            text.Append('\n');
            text.Append("<size=80%><color=#8FA3BF>PLAYER   PLANETS   SHIPS   CAPTURED   LOST   SENT   SHIPS LOST</color></size>\n");

            foreach (Globals.ePlayer player in MatchConfig.ActivePlayers())
            {
                Row row = null;
                if (Instance)
                    Instance._rows.TryGetValue(player, out row);
                if (row == null)
                    row = new Row();
                int planets = Instance && Instance._planets ? Instance._planets.GetAllPlanetsForPlayer(player).Count : 0;
                int ships = ShipManagerInstance.Instance
                    ? ShipManagerInstance.Instance.GetPlayerShipCount((int)player)
                    : 0;

                string color = ColorUtility.ToHtmlStringRGB(Globals.Instance.GetPlayerColor(player));
                text.Append($"<color=#{color}>P{(int)player}</color>       " +
                            $"{planets,3}      {ships,3}      {row.captured,4}     {row.lost,3}    {row.sent,4}      {row.shipsLost,3}\n");
            }

            return text.ToString();
        }

        private static string WinnerName(Globals.ePlayer winner)
        {
            if (winner == Globals.ePlayer.None)
                return "—";

            int team = MatchConfig.TeamOf(winner);
            var members = MatchConfig.TeamMembers(team);
            if (members.Count > 1)
            {
                string names = "";
                foreach (Globals.ePlayer player in members)
                    names += (names.Length > 0 ? " + " : "") + "P" + (int)player;
                return "TEAM " + team + " (" + names + ")";
            }

            string color = ColorUtility.ToHtmlStringRGB(Globals.Instance.GetPlayerColor(winner));
            return $"<color=#{color}>P{(int)winner}</color>";
        }

        private static string FormatTime(float seconds)
        {
            int total = Mathf.Max(0, Mathf.RoundToInt(seconds));
            return $"{total / 60}:{total % 60:00}";
        }

        private Row RowFor(Globals.ePlayer player)
        {
            if (!_rows.TryGetValue(player, out Row row))
            {
                row = new Row();
                _rows[player] = row;
            }
            return row;
        }
    }
}
