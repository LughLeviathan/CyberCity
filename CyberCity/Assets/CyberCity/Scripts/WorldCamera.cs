using UnityEngine;

namespace CyberCity
{
    // PixelPerfectCamera owns the aspect-preserving viewport and Point upscale.
    [RequireComponent(typeof(Camera))]
    public sealed class WorldCamera : MonoBehaviour
    {
        void LateUpdate()
        {
            transform.position = new Vector3(WorldLayout.Center.x, WorldLayout.Center.y, -10);
        }
    }
}
