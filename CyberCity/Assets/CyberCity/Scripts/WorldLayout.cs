using UnityEngine;

namespace CyberCity
{
    public static class WorldLayout
    {
        public const int PixelWidth = 1024, PixelHeight = 576, Ppu = 32;
        public const float Width = 32, Height = 18, SpawnX = 493f / 32, SpawnY = -352f / 32;
        public static Vector2 Spawn => new Vector2(SpawnX, SpawnY);
        public static Vector2 Center => new Vector2(Width / 2, -Height / 2);
        public static Vector2 FromPixel(float x, float y) => new Vector2(x / Ppu, -y / Ppu);
        public static Vector2 Clamp(Vector2 p) => new Vector2(Mathf.Clamp(p.x, .5f, Width - .5f), Mathf.Clamp(p.y, -Height + .5f, -.5f));
    }
}
