using UnityEngine;

namespace CyberCity
{
    // PixelPerfectCamera owns the projection; reserve the console and header before fitting the map.
    [RequireComponent(typeof(Camera))]
    public sealed class WorldCamera : MonoBehaviour
    {
        Camera view;
        void Awake() { view = GetComponent<Camera>(); }
        void LateUpdate()
        {
            // URP PixelPerfectCamera resets Camera.rect while fitting the integer pixel scale.
            // Offset the fixed camera instead so that the full map stays in the console's left region.
            view.rect = new Rect(0, 0, 1, 1);
            float halfWidth = view.orthographicSize * view.aspect;
            transform.position = new Vector3(WorldLayout.Center.x + halfWidth * .22f,
                WorldLayout.Center.y + view.orthographicSize * (50f / 900), -10);
        }
    }
}
