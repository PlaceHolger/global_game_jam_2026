using UnityEngine;
using Warmask.Planet.Runtime;

public class TroopMovementManager : MonoBehaviour
{
    //for now, this class is just a placeholder for future troop movement management logic
    //nor now it will get a public methode moveTroops(fromPlanet, toPlanet, numberOfTroops, player)
    //for now it will then reduce the number of troops on fromPlanet and increase on toPlanet accordingly if owned by same player
    //if toPlanet is owned by different player, it will initiate an attack, reducing troops on fromPlanet and comparing with troops on toPlanet to determine outcome of attack
    //if successful, toPlanet will change ownership to attacking player

    private static TroopMovementManager Instance;
    public static TroopMovementManager GetInstance()
    {
        if (!Instance)
            Instance = FindFirstObjectByType<TroopMovementManager>();
        return Instance;
    }
    
    public void MoveTroops(PlanetInstance fromPlanet, PlanetInstance toPlanet, int numberOfTroops, Globals.ePlayer player)
    {
        if (!fromPlanet || !toPlanet || numberOfTroops <= 0)
        {
            Debug.LogError("Troops can't be null or less than zero");
            return;
        }

        if (fromPlanet.OwnedBy != player)
            return; // Can't move troops from a planet you don't own

        if (fromPlanet.UnitCount < numberOfTroops)
            numberOfTroops = fromPlanet.UnitCount; // Adjust to available troops

        fromPlanet.UnitCount -= numberOfTroops;

        if (toPlanet.OwnedBy == player)
        {
            // Reinforce own planet
            toPlanet.UnitCount += numberOfTroops;
        }
        else
        {
            // Attack enemy or neutral planet
            if (numberOfTroops > toPlanet.UnitCount)
            {
                // Successful attack
                toPlanet.SetOwner(player);
                toPlanet.UnitCount = numberOfTroops - toPlanet.UnitCount;
            }
            else
            {
                // Failed attack
                toPlanet.UnitCount -= numberOfTroops;
            }
        }
    }
}
