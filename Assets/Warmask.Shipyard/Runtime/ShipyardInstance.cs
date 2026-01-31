using UnityEngine;
using UnityEngine.Pool;
using Warmask.Ship;

namespace Warmask.Shipyard
{
    public class ShipyardInstance : MonoBehaviour, IShipPool
    {
        [Header("Owner")]
        [SerializeField] private int playerId;

        [Header("Production")]
        [SerializeField] private GameObject shipPrefab;
        [SerializeField] private float spawnInterval = 2f;
        [SerializeField] private int maxActiveShips = 20;

        [Header("Spawn Settings")]
        [SerializeField] private Vector2 spawnOffset = Vector2.right;
        [SerializeField] private float spawnRandomRadius = 0.5f;

        [Header("Pool Settings")]
        [SerializeField] private int defaultPoolCapacity = 10;
        [SerializeField] private int maxPoolSize = 50;

        private ObjectPool<GameObject> shipPool;
        private float nextSpawnTime;
        private int activeShipCount;
        private Transform cachedTransform;

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
        }

        private void Update()
        {
            if (Time.time >= nextSpawnTime && activeShipCount < maxActiveShips)
            {
                SpawnShip();
                nextSpawnTime = Time.time + spawnInterval;
            }
        }

        private void SpawnShip()
        {
            GameObject ship = shipPool.Get();
            Vector2 spawnPos = (Vector2)cachedTransform.position + spawnOffset + Random.insideUnitCircle * spawnRandomRadius;
            ship.transform.position = spawnPos;
            ship.transform.rotation = cachedTransform.rotation;

            // Configure ship with player ID and target
            if (ship.TryGetComponent(out ShipInstance shipInstance))
            {
                shipInstance.SetPlayerId(playerId);
                shipInstance.SetTarget(cachedTransform);
            }

            // Ensure PooledShip component exists
            if (!ship.TryGetComponent(out PooledShip _))
            {
                PooledShip pooled = ship.AddComponent<PooledShip>();
                pooled.Initialize(this);
            }

            activeShipCount++;
        }

        private GameObject CreateShip()
        {
            GameObject ship = Instantiate(shipPrefab);
            PooledShip pooled = ship.AddComponent<PooledShip>();
            pooled.Initialize(this);
            return ship;
        }

        public void ReturnShipToPool(GameObject ship)
        {
            if (ship == null || !ship.activeInHierarchy) return;
    
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
