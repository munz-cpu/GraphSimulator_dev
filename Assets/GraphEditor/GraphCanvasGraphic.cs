using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GraphSimulator
{
    [ExecuteAlways]
    public sealed class GraphCanvasGraphic : MaskableGraphic, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
    {
        private GraphEditorRuntime editor;

        protected override void OnEnable()
        {
            base.OnEnable();
            editor = FindAnyObjectByType<GraphEditorRuntime>();
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect area = rectTransform.rect;
            AddRect(vh, area, new Color(.075f, .10f, .15f));
            if (editor == null) editor = FindAnyObjectByType<GraphEditorRuntime>();
            if (editor == null || editor.Document == null) return;
            float zoom = editor.Zoom;
            Vector2 pan = editor.Pan;
            float spacing = 40f * zoom;
            if (spacing >= 12f)
            {
                for (float x = area.xMin + Mathf.Repeat(-area.xMin + pan.x, spacing); x < area.xMax; x += spacing)
                    AddRect(vh, new Rect(x, area.yMin, 1, area.height), new Color(.12f, .16f, .21f));
                for (float y = area.yMin + Mathf.Repeat(-area.yMin + pan.y, spacing); y < area.yMax; y += spacing)
                    AddRect(vh, new Rect(area.xMin, y, area.width, 1), new Color(.12f, .16f, .21f));
            }
            foreach (var edge in editor.Document.edges)
            {
                var a = editor.Document.FindNode(edge.from);
                var b = editor.Document.FindNode(edge.to);
                if (a == null || b == null) continue;
                Vector2 start = a.position * zoom + pan;
                Vector2 end = b.position * zoom + pan;
                Vector2 direction = (end - start).normalized;
                start += direction * a.size * zoom * .5f;
                end -= direction * b.size * zoom * .5f;
                AddLine(vh, start, end, edge.thickness * zoom, edge.color);
                if (editor.Document.IsDirected)
                {
                    Vector2 back = end - direction * 13f * zoom;
                    Vector2 side = new Vector2(-direction.y, direction.x) * 7f * zoom;
                    AddLine(vh, end, back + side, edge.thickness * zoom, edge.color);
                    AddLine(vh, end, back - side, edge.thickness * zoom, edge.color);
                }
                if (editor.SelectedEdge == edge.id) AddLine(vh, start, end, 2, new Color(.30f, .70f, .94f));
            }
            foreach (var node in editor.Document.nodes)
            {
                Vector2 center = node.position * zoom + pan;
                float size = node.size * zoom;
                Rect box = new Rect(center.x - size * .5f, center.y - size * .5f, size, size);
                if (!area.Overlaps(box)) continue;
                bool selected = editor.IsNodeSelected(node.id) || editor.EdgeStart == node.id;
                if (selected) AddRect(vh, new Rect(box.x - 3, box.y - 3, box.width + 6, box.height + 6), new Color(.30f, .70f, .94f));
                AddRect(vh, box, node.color);
            }
        }

        private static void AddRect(VertexHelper vh, Rect rect, Color tint)
        {
            int index = vh.currentVertCount;
            var v = UIVertex.simpleVert; v.color = tint;
            v.position = new Vector3(rect.xMin, rect.yMin); vh.AddVert(v);
            v.position = new Vector3(rect.xMin, rect.yMax); vh.AddVert(v);
            v.position = new Vector3(rect.xMax, rect.yMax); vh.AddVert(v);
            v.position = new Vector3(rect.xMax, rect.yMin); vh.AddVert(v);
            vh.AddTriangle(index, index + 1, index + 2);
            vh.AddTriangle(index, index + 2, index + 3);
        }

        private static void AddLine(VertexHelper vh, Vector2 a, Vector2 b, float width, Color color)
        {
            Vector2 direction = b - a;
            if (direction.sqrMagnitude < .01f) return;
            Vector2 side = new Vector2(-direction.y, direction.x).normalized * Mathf.Max(1, width) * .5f;
            int index = vh.currentVertCount;
            var v = UIVertex.simpleVert; v.color = color;
            v.position = a - side; vh.AddVert(v);
            v.position = a + side; vh.AddVert(v);
            v.position = b + side; vh.AddVert(v);
            v.position = b - side; vh.AddVert(v);
            vh.AddTriangle(index, index + 1, index + 2);
            vh.AddTriangle(index, index + 2, index + 3);
        }

        private Vector2 Local(PointerEventData eventData)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, eventData.position, eventData.pressEventCamera, out Vector2 point);
            return point;
        }
        public void OnPointerDown(PointerEventData eventData) { if (Application.isPlaying && editor != null) editor.CanvasPointerDown(Local(eventData), eventData); }
        public void OnBeginDrag(PointerEventData eventData) { if (Application.isPlaying && editor != null) editor.CanvasBeginDrag(Local(eventData), eventData); }
        public void OnDrag(PointerEventData eventData) { if (Application.isPlaying && editor != null) editor.CanvasDrag(Local(eventData), eventData); }
        public void OnEndDrag(PointerEventData eventData) { if (Application.isPlaying && editor != null) editor.CanvasEndDrag(eventData); }
        public void OnScroll(PointerEventData eventData) { if (Application.isPlaying && editor != null) editor.CanvasScroll(Local(eventData), eventData.scrollDelta.y); }
    }
}
