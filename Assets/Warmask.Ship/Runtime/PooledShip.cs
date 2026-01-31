using UnityEngine;

namespace Warmask.Ship
{
    public class PooledShip : MonoBehaviour
    {
        private IShipPool ownerPool;

        public void Initialize(IShipPool pool)
        {
            ownerPool = pool;
        }

        public void ReturnToPool()
        {
            ownerPool.ReturnShipToPool(gameObject);
        }
    }
}