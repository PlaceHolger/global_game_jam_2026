using System.Collections.Generic;
using UnityEngine;
using Warmask.Planet.Runtime;

/// Manages all planets in the scene, providing a centralized point for planet-related operations and for providing the planets for systems that that need them (enemyAI)

public class PlanetsManager : MonoBehaviour
{
    private static PlanetsManager _instance;
    public static PlanetsManager Instance
    {
        get
        {
            if (!_instance)
                _instance = FindFirstObjectByType<PlanetsManager>();
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
}
