using UnityEngine;

namespace Warmask.Ship
{
    public interface IShipPool
    {
        void ReturnShipToPool(GameObject ship);
    }
}