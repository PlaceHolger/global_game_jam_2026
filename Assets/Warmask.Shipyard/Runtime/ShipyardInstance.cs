using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Pool;
using Warmask.Core;
using Warmask.Ship;
using Warmask.Planet;

namespace Warmask.Shipyard
{
    [RequireComponent(typeof(PlanetInstance))]
    public class ShipyardInstance : MonoBehaviour, IShipPool, IShipOwner
    {
        [Header("Owner")]
        [SerializeField] private int playerId;

        [Header("Production")]
        [SerializeField] private Globals.eType type;
        [SerializeField] private GameObject shipPrefab;
        [SerializeField] private float spawnInterval = 2f;
        [SerializeField] private int maxActiveShips = 20;

        [Header("Spawn Settings")]
        [SerializeField] private Vector2 spawnOffset = Vector2.right;
        [SerializeField] private float spawnRandomRadius = 0.5f;

        [Header("Pool Settings")]
        [SerializeField] private int defaultPoolCapacity = 10;
        [SerializeField] private int maxPoolSize = 50;

        [Header("Combat Settings")]
        [SerializeField] private float planetRadius = 2f;

        //private UnityEvent<Dictionary<int, int>> shipCountUpdate;
        
        private Dictionary<int,int> shipCountCache = new Dictionary<int, int>();
        
        private EntityId instanceId;
        private float sqrPlanetShipOwnershipRadius = 1.0f;

        public EntityId OwnerId => instanceId;
        public GameObject ShipPrefab => shipPrefab;
        public ShipInstance LastSpawnedShip { get; private set; }
        
        public void UnregisterShip(object ship)
        {
            if (ship is ShipInstance shipInstance)
            {
                ownedShips.Remove(shipInstance);
            }
        }
        
        public float PlanetRadius
        {
            get => planetRadius;
            set
            {
                planetRadius = value;
            }
        
        }

        private ObjectPool<GameObject> shipPool;
        private float nextSpawnTime;
        private const float ProximityCacheInterval = 0.1f;
        private float nextProximityCacheTime;
        private Transform cachedTransform;
        private List<ShipInstance> ownedShips = new();
        
        private PlanetInstance planetInstance = default;

        public IReadOnlyList<ShipInstance> OwnedShips => ownedShips;
        public int OwnedShipCount => ownedShips.Count;

        // Production cap: own ships assigned to this planet, including ones ordered
        // here. Sending ships away moves them to the destination, so the origin can rebuild.
        private int OwnShipCount
        {
            get
            {
                if (ownedShips == null || planetInstance == null) return 0;

                int ownerId = (int)planetInstance.OwnedBy;
                int count = 0;
                foreach (ShipInstance ship in ownedShips)
                {
                    if (ship && ship.IsAlive && ship.GetPlayerId() == ownerId)
                        count++;
                }
                return count;
            }
        }

        // Fleet cap scales with captures so re-funneling production can't grow
        // the player's total beyond what they actually hold: sum every planet's
        // own ship cap and add the stacking buffer.
        public static int GetFleetCap(Globals.ePlayer player, int buffer)
        {
            PlanetsManager planets = PlanetsManager.Instance;
            if (!planets)
                return int.MaxValue; // scene not ready; don't block early spawns

            int cap = buffer;
            foreach (PlanetInstance planet in planets.GetAllPlanets())
            {
                if (planet.OwnedBy != player) continue;
                if (planet.TryGetComponent(out ShipyardInstance yard))
                    cap += yard.maxActiveShips;
            }
            return cap;
        }

        private bool HasProductionCapacity()
        {
            if (planetInstance == null) return false;
            if (OwnShipCount >= maxActiveShips) return false;

            ShipManagerInstance ships = ShipManagerInstance.Instance;
            if (!ships)
                return true; // scene not set up yet, don't block early spawns

            int buffer = Globals.Instance ? Globals.Instance.fleetCapBuffer : 0;
            return ships.GetPlayerShipCount((int)planetInstance.OwnedBy)
                   < GetFleetCap(planetInstance.OwnedBy, buffer);
        }

        private void Awake()
        {
            EnsureInitialized();
            FleetStatusHud.EnsureExists();
            TroopMovementManager.OnTroopMovementStarted.RemoveListener(MoveTroops);
            TroopMovementManager.OnTroopMovementStarted.AddListener(MoveTroops);
        }

        // Hot reload clears non-serialized fields while the scene keeps running.
        private void EnsureInitialized()
        {
            if (cachedTransform == null)
                cachedTransform = transform;

            if (shipPool == null)
            {
                shipPool = new ObjectPool<GameObject>(
                    createFunc: CreateShip,
                    actionOnGet: OnGetShip,
                    actionOnRelease: OnReleaseShip,
                    actionOnDestroy: OnDestroyShip,
                    collectionCheck: false,
                    defaultCapacity: defaultPoolCapacity,
                    maxSize: maxPoolSize
                );

                instanceId = GetEntityId();
            }

            if (ownedShips == null)
                ownedShips = new List<ShipInstance>();
            if (shipCountCache == null)
                shipCountCache = new Dictionary<int, int>();

            if (planetInstance == null)
            {
                planetInstance = GetComponent<PlanetInstance>();
                if (planetInstance != null)
                {
                    planetInstance.OwnerChanged -= OnPlanetOwnerChanged;
                    planetInstance.OwnerChanged += OnPlanetOwnerChanged;

                    spawnRandomRadius = planetInstance.PlanetSize;
                    planetRadius = planetInstance.PlanetSize;
                    sqrPlanetShipOwnershipRadius = (2 * 1.28f * planetInstance.gameObject.transform.localScale.x) * (2 * 1.528f * planetInstance.gameObject.transform.localScale.x);
                }
            }
        }

        private void OnDestroy()
        {
            TroopMovementManager.OnTroopMovementStarted.RemoveListener(MoveTroops);
            if (planetInstance)
                planetInstance.OwnerChanged -= OnPlanetOwnerChanged;
        }

        // Defenders die with the planet. Ships still in transit keep their owner,
        // so they can counter-attack instead of flipping hands mid-flight.
        private void OnPlanetOwnerChanged(Globals.ePlayer oldOwner, Globals.ePlayer newOwner)
        {
            if (ownedShips == null) return;

            for (int i = ownedShips.Count - 1; i >= 0; i--)
            {
                ShipInstance ship = ownedShips[i];
                if (!ship || ship.GetPlayerId() != (int)oldOwner || ship.IsInTransit) continue;

                ship.Die();
            }
        }

        private void MoveTroops(PlanetInstance fromPlanet, PlanetInstance toPlanet, int troopCount, Globals.ePlayer movingPlayer)
        {
            EnsureInitialized();
            if (fromPlanet && fromPlanet.gameObject == this.gameObject)
            {
                TransferShipsTo(toPlanet.GetComponent<ShipyardInstance>(), troopCount, movingPlayer);
            }
        }

        private void Update()
        {
            EnsureInitialized();
            if (shipPool == null || planetInstance == null || cachedTransform == null)
                return;

            if (Time.time >= nextSpawnTime && HasProductionCapacity() && spawnInterval > 0f)
            {
                SpawnShip();
                float boost = MatchConfig.ProductionBoost(planetInstance.OwnedBy);
                nextSpawnTime = Time.time + spawnInterval / Mathf.Max(0.1f, boost);
            }

            // Ship scans feed only labels and transfer counts; 10 Hz is plenty and
            // cuts the every-frame walk over every shipyard's ship list.
            if (Time.time >= nextProximityCacheTime)
            {
                nextProximityCacheTime = Time.time + ProximityCacheInterval;
                UpdateProximityShipCache();
                planetInstance.OnShipCountChanged(shipCountCache);
            }
        }

        private void UpdateProximityShipCache()
        {
            shipCountCache.Clear();
            int ownerPlayerId = (int)planetInstance.OwnedBy;
            int transferableShips = 0;

            foreach (ShipInstance ship in ownedShips)
            {
                if (!ship || !ship.IsAlive) continue;

                if (CanTransferShip(ship, ownerPlayerId, planetInstance.OwnedBy))
                    transferableShips++;

                float sqrShipDistance = (ship.transform.position - cachedTransform.position).sqrMagnitude;
                if (sqrShipDistance > sqrPlanetShipOwnershipRadius ) continue;

                int shipPlayerId = ship.GetPlayerId();
                shipCountCache[shipPlayerId] = shipCountCache.GetValueOrDefault(shipPlayerId, 0) + 1;
            }

            planetInstance.SetTransferableShipCount(transferableShips);
        }

        public void SpawnShip()
        {
            SpawnShipInternal(ignoreCapacity: false);
        }

        /// <summary>
        /// Spawns a ship without checking production or fleet caps. Used by stress
        /// tests and benchmarks that need a deterministic ship count.
        /// </summary>
        public void SpawnShipUnchecked()
        {
            SpawnShipInternal(ignoreCapacity: true);
        }

        private void SpawnShipInternal(bool ignoreCapacity)
        {
            EnsureInitialized();
            if (shipPool == null || cachedTransform == null || planetInstance == null)
                return;
            if (!ignoreCapacity && !HasProductionCapacity())
                return;

            GameObject ship = shipPool.Get();
            Vector2 spawnPos = (Vector2)cachedTransform.position + spawnOffset + 
                               Random.insideUnitCircle * spawnRandomRadius;
            ship.transform.position = spawnPos;
            ship.transform.rotation = cachedTransform.rotation;

            if (ship.TryGetComponent(out ShipInstance shipInstance))
            {
                // Erst Owner setzen, dann registrieren
                shipInstance.SetOwnerShipyard(this);
                shipInstance.SetPlayerId((int)planetInstance.OwnedBy);
                shipInstance.SetType(type);
                shipInstance.SetTarget(cachedTransform);
                shipInstance.SetMinTargetDistance(planetRadius);
                RegisterShip(shipInstance);
                LastSpawnedShip = shipInstance;
            }

            if (!ship.TryGetComponent(out PooledShip _))
            {
                PooledShip pooled = ship.AddComponent<PooledShip>();
                pooled.Initialize(this, transform);
            }
        }

        public void RegisterShip(ShipInstance ship)
        {
            EnsureInitialized();
            if (!ship || ownedShips.Contains(ship)) return;

            int shipPlayerId = ship.GetPlayerId();
            shipCountCache[shipPlayerId] = shipCountCache.GetValueOrDefault(shipPlayerId, 0) + 1;
            ownedShips.Add(ship);
        }

        public void UnregisterShip(ShipInstance ship)
        {
            EnsureInitialized();
            if (!ship) return;

            int index = ownedShips.IndexOf(ship);
            if (index < 0)
            {
                // Schiff war nicht registriert - kein Fehler, kann bei Race Conditions passieren
                return;
            }

            int shipPlayerId = ship.GetPlayerId();
            shipCountCache[shipPlayerId] = Mathf.Max(0, shipCountCache.GetValueOrDefault(shipPlayerId, 0) - 1);
            ownedShips.RemoveAt(index);
        }
        
        void IShipOwner.UnregisterShip(object ship)
        {
            if (ship is ShipInstance shipInstance)
            {
                UnregisterShip(shipInstance);
            }
        }

        public void TransferShipsTo(ShipyardInstance targetShipyard, int count, Globals.ePlayer executingPlayer )
        {
            if (!targetShipyard || targetShipyard == this || count <= 0) return;

            EnsureInitialized();
            if (planetInstance == null) return;

            int transferred = 0;
            int ownerPlayerId = (int)planetInstance.OwnedBy;

            for (int i = ownedShips.Count - 1; i >= 0 && transferred < count; i--)
            {
                ShipInstance ship = ownedShips[i];

                if (!CanTransferShip(ship, ownerPlayerId, executingPlayer)) continue;

                // Aus alter Liste entfernen
                UnregisterShip(ship);

                // Neue Zuordnung setzen
                ship.TransferToNewOwner(targetShipyard);

                // In neue Liste eintragen
                targetShipyard.RegisterShip(ship);
                ship.SetMoveCommand(targetShipyard.cachedTransform, targetShipyard.planetRadius);

                transferred++;
            }
        }

        private static bool CanTransferShip(ShipInstance ship, int ownerPlayerId, Globals.ePlayer executingPlayer)
        {
            if (ship == null || !ship.IsAlive) return false;
            if (ship.GetPlayerId() != ownerPlayerId) return false;
            return !Globals.Instance.IsHuman(executingPlayer) || ship.ShipType == Globals.Instance.currentMask;
        }

        private GameObject CreateShip()
        {
            GameObject ship = Instantiate(shipPrefab);
            PooledShip pooled = ship.AddComponent<PooledShip>();
            pooled.Initialize(this, transform);
            return ship;
        }

        public void ReturnShipToPool(GameObject ship)
        {
            EnsureInitialized();
            if (ship == null || !ship.activeInHierarchy || shipPool == null) return;

            // UnregisterShip wird NICHT hier aufgerufen!
            // Das hat bereits HandleDeath in ShipInstance gemacht

            shipPool.Release(ship);
        }

        private void OnGetShip(GameObject ship)
        {
            ship.SetActive(true);
        }

        private void OnReleaseShip(GameObject ship)
        {
            ship.SetActive(false);
        }

        private void OnDestroyShip(GameObject ship)
        {
            Destroy(ship);
        }
    }
}
