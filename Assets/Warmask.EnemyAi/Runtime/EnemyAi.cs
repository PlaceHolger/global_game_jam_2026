using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using Warmask.Planet;

//okay, so enemyAi current approach:
// 1. check available troops (starhips waiting at a planet)
// check all planets if they are not owned by us (by enemy or neutral)
// 2. evaluate which planet is the best to attack (based on distance, production rate, current troops there)
// 3. send available troops to attack that planet
// 4. IF there is no good target to attack, check if we can move troops to a better position (closer to player planets, or to planets that produce more troops)

// lets skip for now if it's a good idea to move troops when this might open an opportunity for the player to attack us
// also skip for now, the current mask bonus system

public class EnemyAi : MonoBehaviour
{
    [SerializeField] private Globals.ePlayer enemyPlayer = Globals.ePlayer.Player1;
    [SerializeField] private Globals.ePlayer ownPlayer = Globals.ePlayer.Player2;
    
    [SerializeField] float decisionInterval = 1f;
    [SerializeField] float IntervalAfterAttack = 4f;
    
    [SerializeField, Tooltip("How much weight has a smaller distance in target evaluation")]
    float DistanceFactor = 2f; //weight for distance in target evaluation
    [SerializeField, Tooltip("How much bonus in points do we give to planets owned by the enemy in target evaluation")]
    int PreferEnemyPlanets = 3; 
    [SerializeField, Tooltip("How much weight has a smaller number of troops in target evaluation")]
    float PreferPlanetsWithLessTroops = 2f; 
    [SerializeField, Tooltip("How much weight has a bigger planet size in target evaluation")]
    float PreferBiggerPlanets = 10f; //weight for planet size in target evaluation

    float nextDecisionTime;
    
    MouseLineHelper _mouseLineHelper;

    private void Awake()
    {
        _mouseLineHelper = GetComponentInChildren<MouseLineHelper>();
    }

    void Update()
    {
        if (Time.time < nextDecisionTime) return;
        nextDecisionTime = Time.time + decisionInterval;

        MakeDecision();
    }
    
    void MakeDecision()
    {
        var planetsManager = PlanetsManager.Instance;
        
        // 1. Get our planets with available troops
        var ourPlanets = planetsManager.GetAllPlanetsForPlayer(ownPlayer);
        int totalAvailableTroops = 0;
        PlanetInstance mostAvailableTroopsOnOnePlanetPlanet = null;
        int mostAvailableTroopsOnOnePlanet = 0;
        foreach (var planet in ourPlanets)
        {
            int availableTroops = planet.DefendingShipsInOrbit - 1; // Keep at least 1 troop for defense
            if (availableTroops > 0)
            {
                totalAvailableTroops += availableTroops;
                if (availableTroops > mostAvailableTroopsOnOnePlanet)
                {
                    mostAvailableTroopsOnOnePlanetPlanet = planet;
                    mostAvailableTroopsOnOnePlanet = availableTroops;
                    
                }
            }
        }

        // 2. Find best attack target
        var availablePlanets = planetsManager.GetAllPlanetsNotOwnedByPlayer(ownPlayer);
        var targetPlanet = EvaluateBestTarget(availablePlanets, mostAvailableTroopsOnOnePlanet, mostAvailableTroopsOnOnePlanetPlanet);
        if (targetPlanet)  //cool, we have a target that can be attacked by sending troops from one of our planets
        {
            //TODO: SendTroops(mostAvailableTroopsOnOnePlanetPlanet, targetPlanet);
            Debug.Log("Enemy AI: Attacking planet " + targetPlanet.name);
            float neededTroops = Mathf.Min(targetPlanet.DefendingShipsInOrbit + 4, mostAvailableTroopsOnOnePlanet) ; // Send enough troops to conquer
            neededTroops = Mathf.Max(mostAvailableTroopsOnOnePlanet * 0.666f, neededTroops); //at least send half the troops, otherwise the new planet is too easy to be re-concquered
            int sendTroops = TroopMovementManager.GetInstance().MoveTroops(mostAvailableTroopsOnOnePlanetPlanet, targetPlanet, (int)neededTroops, ownPlayer);
            if(sendTroops > 0)
            {
                //update mouse line for debug visualization
                if (_mouseLineHelper && mostAvailableTroopsOnOnePlanetPlanet)
                {
                    _mouseLineHelper.SetLineType(mostAvailableTroopsOnOnePlanetPlanet.PlanetType, targetPlanet.PlanetType);
                    _mouseLineHelper.SetStartPos(mostAvailableTroopsOnOnePlanetPlanet.transform);
                    _mouseLineHelper.SetEndPos(targetPlanet.transform);
                    Debug.Log($"Enemy AI: Sent {sendTroops} troops from {mostAvailableTroopsOnOnePlanetPlanet.name} to {targetPlanet.name}");
                    DOVirtual.DelayedCall(decisionInterval * 0.66f, () =>
                    {
                        _mouseLineHelper.SetStartPos(null);
                        _mouseLineHelper.SetEndPos(null);
                    });
                }
                nextDecisionTime += IntervalAfterAttack; //wait a bit longer before next decision
                return;
            }
        }

        // 3. No good attack target - consider repositioning
        Debug.Log("No good attack target found for Enemy AI.");
        //TODO: ConsiderRepositioning(ourPlanets);
    }

    private PlanetInstance EvaluateBestTarget(List<PlanetInstance> ourPlanets, int availableTroops, PlanetInstance troopPlanet)
    {
        // iterate over all planets, check if less troops than we can send + some heuristic (distance, production rate)
        PlanetInstance bestTarget = null;
        float bestScore = float.MinValue;
        foreach (var planet in ourPlanets)
        {
            int defendingTroops = planet.DefendingShipsInOrbit;
            if (defendingTroops >= availableTroops)
                continue; // Can't attack this planet
            // Simple heuristic: prefer planets with fewer troops and closer distance
            int ownedByEnemyBonus = (planet.OwnedBy == enemyPlayer) ? (int)PreferEnemyPlanets : 0;
            float distance = (troopPlanet) ? Vector3.Distance(planet.transform.position, troopPlanet.transform.position) : 0;
            float score = (availableTroops - defendingTroops) * PreferPlanetsWithLessTroops +
                          planet.PlanetSize * PreferBiggerPlanets -
                          distance * DistanceFactor + // Larger planets are more valuable, nearer planets are easier to attack
                          ownedByEnemyBonus -
                          planet.DefendingUnityOnGround * 0.05f; // Penalize planets with more ground troops
            if (score > bestScore)
            {
                bestScore = score;
                bestTarget = planet;
            }
        }
        return bestTarget;
    }
}
