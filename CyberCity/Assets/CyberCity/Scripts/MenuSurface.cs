using UnityEngine;
using UnityEngine.UI;

namespace CyberCity
{
    // Geometry keeps corners/strokes crisp at any CanvasScaler scale, without sprite assets.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class MenuSurface : MaskableGraphic
    {
        public float radius = 16, strokeWidth;
        public Color stroke;
        const int Segments = 10;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = rectTransform.rect;
            float edge = Mathf.Min(strokeWidth, Mathf.Min(r.width, r.height) * .5f);
            if (r.width <= 0 || r.height <= 0) return;
            float outerRadius = Mathf.Min(radius, Mathf.Min(r.width, r.height) * .5f);
            var inner = new Rect(r.x + edge, r.y + edge, r.width - edge * 2, r.height - edge * 2);
            float innerRadius = Mathf.Max(0, outerRadius - edge);
            vh.AddVert(inner.center, color, Vector2.zero);
            int count = 4 * (Segments + 1);
            for (int i = 0; i < count; i++)
                vh.AddVert(Perimeter(inner, innerRadius, i), color, Vector2.zero);
            for (int i = 0; i < count; i++) vh.AddTriangle(0, i + 1, (i + 1) % count + 1);
            if (edge <= 0) return;
            int start = vh.currentVertCount;
            for (int i = 0; i < count; i++)
            {
                vh.AddVert(Perimeter(r, outerRadius, i), stroke, Vector2.zero);
                vh.AddVert(Perimeter(inner, innerRadius, i), stroke, Vector2.zero);
            }
            for (int i = 0; i < count; i++)
            {
                int a = start + i * 2, b = start + ((i + 1) % count) * 2;
                vh.AddTriangle(a, b, a + 1); vh.AddTriangle(a + 1, b, b + 1);
            }
        }

        static Vector2 Perimeter(Rect r, float radius, int index)
        {
            int corner = index / (Segments + 1), step = index % (Segments + 1);
            Vector2 center = new Vector2(corner == 0 || corner == 3 ? r.xMax - radius : r.xMin + radius,
                corner < 2 ? r.yMax - radius : r.yMin + radius);
            float angle = (corner * 90f + step * 90f / Segments) * Mathf.Deg2Rad;
            return center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }
    }
}
