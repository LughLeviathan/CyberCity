using UnityEngine;

namespace CyberCity
{
    public sealed class Citizen : MonoBehaviour
    {
        public string citizenName, role, area;
        public Color accent = Color.cyan;
        public int unlockAfter;
        public Sprite[] idleFrames;
        SpriteRenderer sprite;
        void Awake() { sprite = GetComponent<SpriteRenderer>(); }
        void Update()
        {
            if (idleFrames != null && idleFrames.Length > 0)
                sprite.sprite = idleFrames[(int)(Time.time * 2) % idleFrames.Length];
            sprite.sortingOrder = Mathf.RoundToInt(-transform.position.y * 32) + 1;
        }
    }
}
