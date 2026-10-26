using UnityEngine;

namespace Warmask.Core
{
    public class MapBounds : MonoBehaviour
    {
        [SerializeField] private Vector2 center;
        [SerializeField] private Vector2 size = new Vector2(44f, 22f);
        [SerializeField, Min(0.1f)] private float minOrthoSize = 4.5f;
        [SerializeField, Min(0f)] private float padding = 0.5f;

        public Vector2 Center => center;
        public Vector2 Size => size;
        public float MinOrthoSize => minOrthoSize;
        public float Padding => padding;

        public void Configure(Vector2 newCenter, Vector2 newSize, float newMinOrthoSize, float newPadding)
        {
            center = newCenter;
            size = newSize;
            minOrthoSize = newMinOrthoSize;
            padding = newPadding;
        }
    }
}
