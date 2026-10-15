using UnityEngine;

namespace CyberCity
{
    // PixelPerfectCamera owns the projection. Keep the city centered in the HUD's left region.
    [RequireComponent(typeof(Camera))]
    public sealed class WorldCamera : MonoBehaviour
    {
        Camera view;
        void Awake() { view = GetComponent<Camera>(); }
        void LateUpdate()
        {
            float halfWidth = view.orthographicSize * view.aspect;
            transform.position = new Vector3(14 + halfWidth * .22f, -11, -10);
        }
    }
}
