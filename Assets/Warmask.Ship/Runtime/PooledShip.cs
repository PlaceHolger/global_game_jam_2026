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
                Vector3 directionAway = (transform.position - shipyardTransform.position).normalized;
                if (directionAway != Vector3.zero)
                {
                    transform.rotation = Quaternion.LookRotation(directionAway);
                }
            }
        }

        public void ReturnToPool()
        {
            // Schiff-spezifische Aufräumarbeiten wurden bereits in 
            // ShipInstance.HandleDeath erledigt
            ownerPool?.ReturnShipToPool(gameObject);
        }
    }
}