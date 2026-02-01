using NUnit.Framework;
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
        
        private int instanceId;
        private float sqrPlanetShipOwnershipRadius = 1.0f;

        public int OwnerId => instanceId;
        
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
        private int activeShipCount;
        private Transform cachedTransform;
        private readonly List<ShipInstance> ownedShips = new();
        
        private PlanetInstance planetInstance = default;

        public IReadOnlyList<ShipInstance> OwnedShips => ownedShips;
        public int OwnedShipCount => ownedShips.Count;

        private void Awake()
        {
            instanceId = GetInstanceID();
            cachedTransform = transform;
            shipPool = new ObjectPool<GameObject>(
                createFunc: CreateShip,
                actionOnGet: OnGetShip,
                actionOnRelease: OnReleaseShip,
                actionOnDestroy: OnDestroyShip,
                collectionCheck: false,
                defaultCapacity: defaultPoolCapacity,
                maxSize: maxPoolSize
            );

            planetInstance = GetComponent<PlanetInstance>();
            
            spawnRandomRadius = planetInstance.PlanetSize;
            planetRadius = planetInstance.PlanetSize;
            sqrPlanetShipOwnershipRadius = (2 * 1.28f * planetInstance.gameObject.transform.localScale.x) * (2 * 1.528f * planetInstance.gameObject.transform.localScale.x);

            TroopMovementManager.OnTroopMovementStarted.AddListener(MoveTroops);
        }

        private void OnDestroy()
        {
            TroopMovementManager.OnTroopMovementStarted.RemoveListener(MoveTroops);
        }

        private void MoveTroops(PlanetInstance fromPlanet, PlanetInstance toPlanet, int troopCount, Globals.ePlayer movingPlayer)
        {
            if (fromPlanet && fromPlanet.gameObject == this.gameObject)
            {
                TransferShipsTo(toPlanet.GetComponent<ShipyardInstance>(), troopCount, movingPlayer);
            }
        }

        private void Update()
        {
            if (Time.time >= nextSpawnTime && activeShipCount < maxActiveShips && spawnInterval > 0f)
            {
                SpawnShip();
                nextSpawnTime = Time.time + spawnInterval;
            }

            UpdateProximityShipCache();
            planetInstance.OnShipCountChanged(shipCountCache);
        }

        private void UpdateProximityShipCache()
        {
            shipCountCache.Clear();
            
            foreach (ShipInstance ship in ownedShips)
            {
                if (!ship) continue;
                
                float sqrShipDistance = (ship.transform.position - cachedTransform.position).sqrMagnitude;
                if (sqrShipDistance > sqrPlanetShipOwnershipRadius ) continue;

                int shipPlayerId = ship.GetPlayerId();
                shipCountCache[shipPlayerId] = shipCountCache.GetValueOrDefault(shipPlayerId, 0) + 1;
            }
        }

        public void SpawnShip()
        {
            if (activeShipCount >= maxActiveShips) return;

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
            }

            if (!ship.TryGetComponent(out PooledShip _))
            {
                PooledShip pooled = ship.AddComponent<PooledShip>();
                pooled.Initialize(this, transform);
            }

            activeShipCount++;
        }

        public void RegisterShip(ShipInstance ship)
        {
            if (!ship || ownedShips.Contains(ship)) return;

            int shipPlayerId = ship.GetPlayerId();
            shipCountCache[shipPlayerId] = shipCountCache.GetValueOrDefault(shipPlayerId, 0) + 1;
            ownedShips.Add(ship);
        }

        public void UnregisterShip(ShipInstance ship)
        {
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

            int transferred = 0;
            int ownerPlayerId = (int)planetInstance.OwnedBy;

            for (int i = ownedShips.Count - 1; i >= 0 && transferred < count; i--)
            {
                ShipInstance ship = ownedShips[i];

                // Validierung: Schiff existiert, gehört uns, und ist noch am Leben
                if (ship == null || !ship.IsAlive) continue;
                if (ship.GetPlayerId() != ownerPlayerId) continue;
                if (Globals.Instance.IsPlayer(executingPlayer))
                {
                    if (ship.ShipType != Globals.Instance.currentMask) continue;    
                }
                

                // Aus alter Liste entfernen
                UnregisterShip(ship);

                // Neue Zuordnung setzen
                ship.TransferToNewOwner(targetShipyard);

                // In neue Liste eintragen
                targetShipyard.RegisterShip(ship);
                ship.SetTarget(targetShipyard.cachedTransform);
                ship.SetMinTargetDistance(targetShipyard.planetRadius);

                transferred++;
            }
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
            if (ship == null || !ship.activeInHierarchy) return;

            // UnregisterShip wird NICHT hier aufgerufen!
            // Das hat bereits HandleDeath in ShipInstance gemacht

            shipPool.Release(ship);
            activeShipCount = Mathf.Max(0, activeShipCount - 1);
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
