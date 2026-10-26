using UnityEngine;

namespace Warmask.Core
{
    /// <summary>
    /// Keeps the label camera (layer UI, post-processing off) projecting exactly like the
    /// gameplay camera, so world-space planet HUD follows zoom and pan and stays on its planet.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class UiCameraFollower : MonoBehaviour
    {
        [SerializeField] private Camera source;

        public void SetSource(Camera camera) => source = camera;

        private void LateUpdate()
        {
            if (!source)
                return;

            Camera target = GetComponent<Camera>();
            transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            target.orthographic = source.orthographic;
            target.orthographicSize = source.orthographicSize;
        }
    }
}
