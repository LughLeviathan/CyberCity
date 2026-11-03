using UnityEngine;
using UnityEngine.UI;

namespace CyberCity
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class MenuGradient : MaskableGraphic
    {
        public bool radial;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var r = rectTransform.rect;
            if (radial)
            {
                const int rings = 20, segments = 64;
                for (int ring = 0; ring <= rings; ring++)
                {
                    float t = ring / (float)rings;
                    Color c = color; c.a *= (1 - t) * (1 - t);
                    for (int i = 0; i < segments; i++)
                    {
                        float angle = i * Mathf.PI * 2 / segments;
                        vh.AddVert(r.center + new Vector2(Mathf.Cos(angle) * r.width * .5f, Mathf.Sin(angle) * r.height * .5f) * t, c, Vector2.zero);
                    }
                }
                for (int ring = 0; ring < rings; ring++)
                    for (int i = 0; i < segments; i++)
                    {
                        int a = ring * segments + i, b = ring * segments + (i + 1) % segments;
                        vh.AddTriangle(a, b, a + segments); vh.AddTriangle(b, b + segments, a + segments);
                    }
            }
            else
            {
                float[] stops = { 0, .62f, 1 }; float[] alpha = { 1, .96f, 0 };
                for (int i = 0; i < stops.Length; i++)
                {
                    Color c = color; c.a *= alpha[i]; float x = r.xMin + r.width * stops[i];
                    vh.AddVert(new Vector2(x, r.yMin), c, Vector2.zero); vh.AddVert(new Vector2(x, r.yMax), c, Vector2.zero);
                    if (i > 0) { int a = (i - 1) * 2; vh.AddTriangle(a, a + 1, a + 2); vh.AddTriangle(a + 1, a + 3, a + 2); }
                }
            }
        }
    }
}
