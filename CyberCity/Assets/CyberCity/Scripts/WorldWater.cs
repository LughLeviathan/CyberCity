using UnityEngine;

namespace CyberCity
{
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class WorldWater : MonoBehaviour
    {
        public Sprite[] frames;
        SpriteRenderer image;
        float elapsed;
        int frame;
        void Awake() { image = GetComponent<SpriteRenderer>(); }
        void Update()
        {
            if (frames == null || frames.Length == 0) return;
            elapsed += Time.deltaTime;
            float duration = frame == 0 ? .1f : .22f;
            if (elapsed < duration) return;
            elapsed -= duration; frame = (frame + 1) % frames.Length; image.sprite = frames[frame];
        }
    }
}
