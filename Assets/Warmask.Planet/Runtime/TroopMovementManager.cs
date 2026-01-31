using UnityEngine;
using UnityEngine.Events;

namespace Warmask.Planet
{
    public class TroopMovementManager : MonoBehaviour
    {
        static UnityEvent<PlanetInstance, PlanetInstance, int, Globals.ePlayer>
            OnTroopMovementStarted = new(); //from, to, num, who

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

        // returns number of troops moved, or -1 on error
        public int MoveTroops(PlanetInstance fromPlanet, PlanetInstance toPlanet, int numberOfTroops,
            Globals.ePlayer movingPlayer)
        {
            if (!fromPlanet || !toPlanet || numberOfTroops <= 0)
            {
                Debug.LogError("Troops can't be null or less than zero");
                return -1;
            }

            if (fromPlanet.OwnedBy != movingPlayer)
                return -1; // Can't move troops from a planet you don't own

            if (fromPlanet.UnitCount < numberOfTroops)
                numberOfTroops = fromPlanet.UnitCount; // Adjust to available troops

            OnTroopMovementStarted.Invoke(fromPlanet, toPlanet, numberOfTroops, movingPlayer);

            fromPlanet.UnitCount -= numberOfTroops;

            if (toPlanet.OwnedBy == movingPlayer)
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
                    toPlanet.SetOwner(movingPlayer);
                    toPlanet.UnitCount = numberOfTroops - toPlanet.UnitCount;
                }
                else
                {
                    // Failed attack
                    toPlanet.UnitCount -= numberOfTroops;
                }
            }

            return numberOfTroops;
        }
    }
}