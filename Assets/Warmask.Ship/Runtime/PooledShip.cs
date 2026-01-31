using UnityEngine;

namespace Warmask.Ship
{
    public class PooledShip : MonoBehaviour
    {
        private IShipPool ownerPool;

        public void Initialize(IShipPool pool, Transform shipyardTransform)
        {
            ownerPool = pool;
            if (shipyardTransform != null)
            {
                // Richtung vom Shipyard weg berechnen
                Vector3 directionAway = (transform.position - shipyardTransform.position).normalized;
                
                // Schiff in diese Richtung ausrichten
                if (directionAway != Vector3.zero)
                {
                    transform.rotation = Quaternion.LookRotation(directionAway);
                }
            }
        }

        public void ReturnToPool()
        {
            ownerPool.ReturnShipToPool(gameObject);
        }
    }
}