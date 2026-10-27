using System.Collections.Generic;
using UnityEngine;

namespace Warmask.Planet
{
    /// <summary>
    /// Screen-space "jump to the nearest planet in this direction" picking for the
    /// keyboard and gamepad cursors, so aiming never means free-flying a pointer.
    /// </summary>
    public static class CursorSnapping
    {
        private const float MinDot = 0.7f; // within ~45 degrees of the pressed direction

        public static bool TrySnap(Camera camera, Vector2 from, Vector2 direction, out Vector2 target)
        {
            target = default;
            if (!camera)
                return false;

            var candidates = new List<Vector2>();
            foreach (PlanetInstance planet in Object.FindObjectsByType<PlanetInstance>(FindObjectsInactive.Exclude))
            {
                Vector3 screenPoint = camera.WorldToScreenPoint(planet.transform.position);
                if (screenPoint.z >= 0f)
                    candidates.Add(screenPoint);
            }

            return TryPick(from, direction, candidates, out target);
        }

        /// <summary>Nearest candidate inside the cone, ignoring the one already under the cursor.</summary>
        public static bool TryPick(Vector2 from, Vector2 direction, IReadOnlyList<Vector2> candidates,
            out Vector2 target)
        {
            target = default;
            if (direction.sqrMagnitude < 0.0001f)
                return false;

            Vector2 dir = direction.normalized;
            float bestDistance = float.MaxValue;
            bool found = false;

            for (int i = 0; i < candidates.Count; i++)
            {
                Vector2 delta = candidates[i] - from;
                float distance = delta.magnitude;
                if (distance < 1f || distance >= bestDistance)
                    continue;

                if (Vector2.Dot(delta / distance, dir) < MinDot)
                    continue;

                bestDistance = distance;
                target = candidates[i];
                found = true;
            }

            return found;
        }
    }
}
