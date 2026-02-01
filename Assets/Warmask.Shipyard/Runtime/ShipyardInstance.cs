using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Pool;
using Warmask.Ship;
using Warmask.Planet;

namespace Warmask.Shipyard
{
    [RequireComponent(typeof(PlanetInstance))]
    public class ShipyardInstance : MonoBehaviour, IShipPool
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

        public float PlanetRadius
        {
            get => planetRadius;
            set => planetRadius = value;
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
                TransferShipsTo(toPlanet.GetComponent<ShipyardInstance>(), troopCount);
            }
        }

        private void Update()
        {
            if (Time.time >= nextSpawnTime && activeShipCount < maxActiveShips && spawnInterval > 0f)
            {
                SpawnShip();
                nextSpawnTime = Time.time + spawnInterval;
            }

            planetInstance.OnShipCountChanged(shipCountCache);
            //shipCountUpdate?.Invoke(shipCountCache);
        }

        public void SpawnShip()
        {
            if (activeShipCount >= maxActiveShips) return;

            
            
            GameObject ship = shipPool.Get();
            Vector2 spawnPos = (Vector2)cachedTransform.position + spawnOffset + Random.insideUnitCircle * spawnRandomRadius;
            ship.transform.position = spawnPos;
            ship.transform.rotation = cachedTransform.rotation;

            if (ship.TryGetComponent(out ShipInstance shipInstance))
            {
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
            if (ship && !ownedShips.Contains(ship))
            {
                shipCountCache[ship.GetPlayerId()] = shipCountCache.GetValueOrDefault(ship.GetPlayerId(), 0) + 1;
                
                ownedShips.Add(ship);
            }
        }

        public void UnregisterShip(ShipInstance ship)
        {
            if (ship && ownedShips.Contains(ship))
            {
                shipCountCache[ship.GetPlayerId()] = Mathf.Max(0, shipCountCache.GetValueOrDefault(ship.GetPlayerId(), 0) - 1);
            } 
            
            ownedShips.Remove(ship);
        }

        public void TransferShipsTo(ShipyardInstance targetShipyard, int count)
        {
            if (!targetShipyard || targetShipyard == this || count <= 0) return;

            int transferCount = Mathf.Min(count, ownedShips.Count);

            for (int i = 0; i < transferCount; i++)
            {
                if (ownedShips.Count == 0) break;

                ShipInstance ship = ownedShips[ownedShips.Count - 1];
                ownedShips.RemoveAt(ownedShips.Count - 1);

                if (ship != null)
                {
                    targetShipyard.RegisterShip(ship);
                    ship.SetTarget(targetShipyard.cachedTransform);
                    ship.SetMinTargetDistance(targetShipyard.planetRadius);
                }
            }
        }

        public void TransferAllShipsTo(ShipyardInstance targetShipyard)
        {
            TransferShipsTo(targetShipyard, ownedShips.Count);
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

            if (ship.TryGetComponent(out ShipInstance shipInstance))
            {
                UnregisterShip(shipInstance);
            }

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
