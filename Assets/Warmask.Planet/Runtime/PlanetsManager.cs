using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// Manages all planets in the scene, providing a centralized point for planet-related operations and for providing the planets for systems that that need them (enemyAI)

namespace Warmask.Planet
{
    public class PlanetsManager : MonoBehaviour
    {
        private static PlanetsManager _instance;
        
        //public UnityEvent<Globals.ePlayer> OnPlayerLost = new UnityEvent<Globals.ePlayer>();
        public UnityEvent OnPlayerLost = new();
        public UnityEvent OnPlayerWin = new();

        public static PlanetsManager Instance
        {
            get
            {
                if (!_instance)
                    _instance = FindAnyObjectByType<PlanetsManager>();
                return _instance;
            }
        }

        private readonly List<PlanetInstance> _allPlanets = new();

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            //fetch children planets
            var planets = GetComponentsInChildren<PlanetInstance>();
            _allPlanets.AddRange(planets);
            MatchStats.Ensure(this);
        }

        public List<PlanetInstance> GetAllPlanets()
        {
            return _allPlanets;
        }

        public List<PlanetInstance> GetAllPlanetsForPlayer(Globals.ePlayer player)
        {
            var result = new List<PlanetInstance>();
            foreach (var planet in _allPlanets)
            {
                if (planet.OwnedBy == player)
                    result.Add(planet);
            }

            return result;
        }

        public List<PlanetInstance> GetAllPlanetsNotOwnedByPlayer(Globals.ePlayer player)
        {
            var result = new List<PlanetInstance>();
            foreach (var planet in _allPlanets)
            {
                if (planet.OwnedBy != player)
                    result.Add(planet);
            }

            return result;
        }

        public void CheckIfPlayerLostAllPlanets(Globals.ePlayer playerThatLostPlanet)
        {
            if(playerThatLostPlanet == Globals.ePlayer.None)
                return;  //we don't care about neutral planets

            if (StillOwnsPlanet(playerThatLostPlanet))
                return;

            List<int> remainingTeams = RemainingTeams();
            if (remainingTeams.Count <= 1)
            {
                int winningTeam = remainingTeams.Count == 1 ? remainingTeams[0] : 0;
                Globals.ePlayer winner = winningTeam > 0 ? WinnerFor(winningTeam) : Globals.ePlayer.None;
                MatchStats.NoteWinner(winner);
                if (winningTeam > 0 && MatchConfig.TeamHasHuman(winningTeam))
                    OnPlayerWin.Invoke();
                else
                    OnPlayerLost.Invoke();
                return;
            }

            // A team keeps fighting when one of its players loses their last planet.
            foreach (int team in remainingTeams)
            {
                if (MatchConfig.TeamHasHuman(team))
                    return;
            }

            MatchStats.NoteWinner(Globals.ePlayer.None);
            OnPlayerLost.Invoke();
        }

        private bool StillOwnsPlanet(Globals.ePlayer player)
        {
            foreach (var planet in _allPlanets)
            {
                if (planet.OwnedBy == player)
                    return true;
            }
            return false;
        }

        private List<int> RemainingTeams()
        {
            var remaining = new List<int>();
            foreach (Globals.ePlayer player in MatchConfig.ActivePlayers())
            {
                if (!StillOwnsPlanet(player))
                    continue;

                int team = MatchConfig.TeamOf(player);
                if (!remaining.Contains(team))
                    remaining.Add(team);
            }
            return remaining;
        }

        private static Globals.ePlayer WinnerFor(int team)
        {
            foreach (Globals.ePlayer player in MatchConfig.ActivePlayers())
                if (MatchConfig.TeamOf(player) == team && Globals.Instance.IsHuman(player))
                    return player;

            foreach (Globals.ePlayer player in MatchConfig.ActivePlayers())
                if (MatchConfig.TeamOf(player) == team)
                    return player;

            return Globals.ePlayer.None;
        }

        /// <summary>True when at least one player owns a planet and every owner is human.</summary>
        public static bool IsOnlyHumanLeft(IEnumerable<Globals.ePlayer> owners)
        {
            bool anyPlayer = false;
            foreach (var owner in owners)
            {
                if (owner == Globals.ePlayer.None)
                    continue;
                if (!Globals.Instance.IsPlayer(owner))
                    return false;
                anyPlayer = true;
            }
            return anyPlayer;
        }
    }
}