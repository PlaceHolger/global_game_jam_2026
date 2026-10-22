using UnityEngine;

namespace Warmask.Core
{
    public interface IShipOwner
    {
        void UnregisterShip(object ship);
        EntityId OwnerId { get; }
    }
}