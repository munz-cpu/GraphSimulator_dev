using UnityEngine;
using UnityEngine.UI;

namespace GraphSimulator
{
    [ExecuteAlways]
    public sealed class GraphCanvasLabels : MaskableGraphic
    {
        private GraphEditorRuntime editor;
        [SerializeField] private Font font;
        public override Texture mainTexture { get { return font != null ? font.material.mainTexture : base.mainTexture; } }

        protected override void OnEnable()
        {
            base.OnEnable();
            raycastTarget = false;
            editor = FindAnyObjectByType<GraphEditorRuntime>();
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (editor == null) editor = FindAnyObjectByType<GraphEditorRuntime>();
            if (editor == null || editor.Document == null || font == null) return;
            foreach (var edge in editor.Document.edges)
            {
                if (editor.Document.kind != GraphKind.WeightedGraph) break;
                var a = editor.Document.FindNode(edge.from);
                var b = editor.Document.FindNode(edge.to);
                if (a != null && b != null) AddText(vh, edge.weight.ToString("0.##"), (a.position + b.position) * editor.Zoom * .5f + editor.Pan + Vector2.up * 16, 13, new Color(.25f, .28f, .32f));
            }
            foreach (var node in editor.Document.nodes)
            {
                float brightness = node.color.r * .2126f + node.color.g * .7152f + node.color.b * .0722f;
                Color ink = brightness > .58f ? new Color(.13f, .15f, .18f) : Color.white;
                AddText(vh, node.name, node.position * editor.Zoom + editor.Pan, Mathf.Max(10, Mathf.RoundToInt(12 * editor.Zoom)), ink);
            }
        }

        private void AddText(VertexHelper vh, string value, Vector2 center, int size, Color ink)
        {
            if (string.IsNullOrEmpty(value)) return;
            font.RequestCharactersInTexture(value, size);
            var settings = new TextGenerationSettings
            {
                font = font, fontSize = size, color = ink, textAnchor = TextAnchor.MiddleCenter,
                generationExtents = new Vector2(220, 80), pivot = new Vector2(.5f, .5f),
                horizontalOverflow = HorizontalWrapMode.Wrap, verticalOverflow = VerticalWrapMode.Truncate,
                scaleFactor = 1, richText = false, lineSpacing = 1
            };
            var generator = new TextGenerator(value.Length * 4);
            if (!generator.Populate(value, settings)) return;
            var verts = generator.verts;
            for (int i = 0; i + 3 < verts.Count - 4; i += 4)
            {
                var quad = new UIVertex[4];
                for (int j = 0; j < 4; j++) { quad[j] = verts[i + j]; quad[j].position += (Vector3)center; }
                vh.AddUIVertexQuad(quad);
            }
        }
    }
}
