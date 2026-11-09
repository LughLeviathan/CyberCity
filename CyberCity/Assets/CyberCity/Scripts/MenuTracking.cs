using UnityEngine;
using UnityEngine.UI;

namespace CyberCity
{
    [RequireComponent(typeof(Text))]
    public sealed class MenuTracking : BaseMeshEffect
    {
        public float spacing;
        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive()) return;
            var text = (Text)graphic;
            var lines = text.cachedTextGenerator.lines;
            var vertex = new UIVertex();
            int total = vh.currentVertCount / 4;
            for (int line = 0; line < lines.Count; line++)
            {
                int start = lines[line].startCharIdx;
                int end = line + 1 < lines.Count ? lines[line + 1].startCharIdx : total;
                end = Mathf.Min(end, total);
                float alignment = (int)text.alignment % 3 * .5f;
                float offset = (end - start - 1) * spacing * alignment;
                for (int i = start; i < end; i++)
                    for (int v = 0; v < 4; v++)
                    {
                        int index = i * 4 + v; vh.PopulateUIVertex(ref vertex, index);
                        vertex.position.x += (i - start) * spacing - offset; vh.SetUIVertex(vertex, index);
                    }
            }
        }
    }
}
