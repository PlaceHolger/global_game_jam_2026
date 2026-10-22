using UnityEngine;

namespace Warmask.Planet
{
    public interface IPointerSource
    {
        Globals.ePlayer Player { get; }
        Vector2 ScreenPosition { get; }
        bool ConfirmPressed { get; }
        bool CancelPressed { get; }
        bool SendAllHeld { get; }
        bool TryGetWorldPoint(Camera camera, out Vector3 worldPoint);
    }
}
