namespace Warmask.Core
{
    public interface IShipOwner
    {
        void UnregisterShip(object ship);
        int OwnerId { get; }
    }
}