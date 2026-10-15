using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GraphSimulator
{
    // The view objects are authored in SampleScene. This component never creates GameObjects.
    public sealed class GraphEditorRuntime : MonoBehaviour
    {
        [SerializeField] private GraphDocument document = new GraphDocument();
        private readonly HashSet<int> selectedNodes = new HashSet<int>();
        private readonly List<string> undo = new List<string>();
        private readonly List<string> redo = new List<string>();
        private GraphCanvasGraphic surface;
        private GraphCanvasLabels labels;
        private Button[] sceneButtons;
        private readonly Dictionary<Button, float> buttonClickTimes = new Dictionary<Button, float>();
        private readonly Dictionary<Button, float> queuedClickTimes = new Dictionary<Button, float>();
        private Text statsLabel, statusLabel, inspectorSummary;
        private InputField keyInput, nameInput, weightInput;
        private Slider sizeSlider, thicknessSlider;
        private int selectedEdge = -1, edgeStart = -1;
        private bool edgeTool, draggingNodes, panning, dragRecorded;
        private bool guiPointerActive;
        private PointerEventData.InputButton guiPointerButton;
        private Vector2 lastPointer, pan;
        private float zoom = 1f;
        private bool previousRunInBackground;
        private bool inputInitialized;
        private string status = "Double-click empty canvas to add a node.";
        private readonly Color[] palette = { new Color(.22f,.55f,.92f), new Color(.24f,.72f,.57f), new Color(.97f,.63f,.28f), new Color(.83f,.38f,.49f), new Color(.64f,.48f,.85f), new Color(.88f,.89f,.94f) };

        public GraphDocument Document { get { return document; } }
        public float Zoom { get { return zoom; } }
        public Vector2 Pan { get { return pan; } }
        public int SelectedEdge { get { return selectedEdge; } }
        public int EdgeStart { get { return edgeStart; } }
        public bool IsNodeSelected(int id) { return selectedNodes.Contains(id); }

        private void Start()
        {
            previousRunInBackground = Application.runInBackground;
            Application.runInBackground = true;
            inputInitialized = true;
            var canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null) { Debug.LogError("Graph Canvas is missing from the scene."); return; }
            Transform root = canvas.transform;
            surface = root.Find("CanvasSurface")?.GetComponent<GraphCanvasGraphic>();
            labels = root.Find("CanvasSurface/LabelsOverlay")?.GetComponent<GraphCanvasLabels>();
            if (surface == null) { Debug.LogError("Graph CanvasSurface is missing from the scene."); return; }
            string[] modes = { "GeneralTreeButton", "BinaryTreeButton", "BstButton", "UndirectedButton", "DirectedButton", "WeightedButton" };
            for (int i = 0; i < modes.Length; i++) { int index = i; Bind(root, "TopBar/" + modes[i], () => ChangeKind((GraphKind)index)); }
            Bind(root, "TopBar/ClearButton", ClearDocument);
            Bind(root, "LeftPanel/EdgeToolButton", () => { edgeTool = !edgeTool; edgeStart = -1; status = edgeTool ? "Choose source and destination nodes." : "Select mode."; Refresh(); });
            Bind(root, "LeftPanel/UndoButton", Undo);
            Bind(root, "LeftPanel/RedoButton", Redo);
            Bind(root, "LeftPanel/SaveButton", Save);
            Bind(root, "LeftPanel/LoadButton", Load);
            keyInput = root.Find("LeftPanel/KeyInput")?.GetComponent<InputField>();
            nameInput = root.Find("RightPanel/NameInput")?.GetComponent<InputField>();
            weightInput = root.Find("RightPanel/WeightInput")?.GetComponent<InputField>();
            sizeSlider = root.Find("RightPanel/SizeSlider")?.GetComponent<Slider>();
            thicknessSlider = root.Find("RightPanel/ThicknessSlider")?.GetComponent<Slider>();
            statsLabel = root.Find("BottomBar/StatsText")?.GetComponent<Text>();
            statusLabel = root.Find("BottomBar/StatusText")?.GetComponent<Text>();
            inspectorSummary = root.Find("RightPanel/InspectorSummary")?.GetComponent<Text>();
            if (keyInput != null) keyInput.SetTextWithoutNotify("0");
            if (nameInput != null) nameInput.onEndEdit.AddListener(ChangeName);
            if (weightInput != null) weightInput.onEndEdit.AddListener(ChangeWeight);
            if (sizeSlider != null) { sizeSlider.minValue = 32; sizeSlider.maxValue = 120; sizeSlider.onValueChanged.AddListener(ChangeSize); }
            if (thicknessSlider != null) { thicknessSlider.minValue = 1; thicknessSlider.maxValue = 12; thicknessSlider.onValueChanged.AddListener(ChangeThickness); }
            for (int i = 0; i < palette.Length; i++) { int index = i; Bind(root, "RightPanel/ColorButton" + i, () => ChangeColor(palette[index])); }
            sceneButtons = root.GetComponentsInChildren<Button>(true);
            foreach (var sceneButton in sceneButtons)
            {
                Button captured = sceneButton;
                captured.onClick.AddListener(() => buttonClickTimes[captured] = Time.realtimeSinceStartup);
            }
            Refresh();
        }

        private void OnDestroy()
        {
            if (!inputInitialized) return;
            Application.runInBackground = previousRunInBackground;
        }

        private static void Bind(Transform root, string path, UnityEngine.Events.UnityAction action)
        {
            var button = root.Find(path)?.GetComponent<Button>();
            if (button != null) button.onClick.AddListener(action);
        }

        private void Update()
        {
            if (Keyboard.current == null) return;
            var keyboard = Keyboard.current;
            if (keyboard.ctrlKey.isPressed && keyboard.zKey.wasPressedThisFrame) Undo();
            else if (keyboard.ctrlKey.isPressed && keyboard.yKey.wasPressedThisFrame) Redo();
            else if (keyboard.deleteKey.wasPressedThisFrame && !EditingInput()) DeleteSelected();
            else if (keyboard.escapeKey.wasPressedThisFrame) { edgeTool = false; ResetSelection(); Refresh(); }
        }

        private void OnGUI()
        {
            if (!Application.isPlaying || sceneButtons == null || surface == null) return;
            var current = Event.current;
            if (current == null) return;
            var screenPoint = new Vector2(current.mousePosition.x, Screen.height - current.mousePosition.y);
            bool overCanvas = RectTransformUtility.RectangleContainsScreenPoint(surface.rectTransform, screenPoint);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(surface.rectTransform, screenPoint, null, out Vector2 local);
            if (current.type == EventType.MouseDown && overCanvas && (current.button == 0 || current.button == 1 || current.button == 2))
            {
                guiPointerActive = true;
                guiPointerButton = current.button == 1 ? PointerEventData.InputButton.Right :
                    current.button == 2 ? PointerEventData.InputButton.Middle : PointerEventData.InputButton.Left;
                CanvasPointerDown(local, PointerData(guiPointerButton, current.clickCount), current.shift);
                return;
            }
            if (current.type == EventType.MouseDrag && guiPointerActive)
            {
                CanvasDrag(local, PointerData(guiPointerButton, current.clickCount));
                return;
            }
            if (current.type == EventType.MouseUp && guiPointerActive &&
                (current.button == 0 && guiPointerButton == PointerEventData.InputButton.Left ||
                 current.button == 1 && guiPointerButton == PointerEventData.InputButton.Right ||
                 current.button == 2 && guiPointerButton == PointerEventData.InputButton.Middle))
            {
                CanvasEndDrag(PointerData(guiPointerButton, current.clickCount));
                guiPointerActive = false;
                return;
            }
            if (current.type == EventType.ScrollWheel && overCanvas)
            {
                CanvasScroll(local, -current.delta.y);
                return;
            }
            if (current.type != EventType.MouseUp || current.button != 0) return;
            for (int i = sceneButtons.Length - 1; i >= 0; i--)
            {
                Button button = sceneButtons[i];
                if (!button.isActiveAndEnabled || !button.interactable) continue;
                var rect = button.GetComponent<RectTransform>();
                if (rect == null || !RectTransformUtility.RectangleContainsScreenPoint(rect, screenPoint)) continue;
                float now = Time.realtimeSinceStartup;
                if (queuedClickTimes.TryGetValue(button, out float queued) && now - queued < .05f) return;
                queuedClickTimes[button] = now;
                StartCoroutine(DispatchClickIfNeeded(button, now));
                return;
            }
        }

        private static PointerEventData PointerData(PointerEventData.InputButton button, int clickCount)
        {
            return new PointerEventData(EventSystem.current) { button = button, clickCount = Mathf.Max(1, clickCount) };
        }

        private IEnumerator DispatchClickIfNeeded(Button button, float observedAt)
        {
            yield return null;
            if (button == null || !button.isActiveAndEnabled || !button.interactable) yield break;
            if (buttonClickTimes.TryGetValue(button, out float handledAt) && handledAt >= observedAt) yield break;
            button.onClick.Invoke();
        }

        private static bool EditingInput()
        {
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            return selected != null && selected.GetComponent<InputField>() != null;
        }

        public void CanvasPointerDown(Vector2 local, PointerEventData e, bool guiShift = false)
        {
            if (e.button == PointerEventData.InputButton.Right || e.button == PointerEventData.InputButton.Middle)
            {
                panning = true;
                lastPointer = local;
                return;
            }
            if (e.button != PointerEventData.InputButton.Left) return;
            int nodeId = HitNode(local);
            if (nodeId >= 0)
            {
                if (edgeTool) SelectEdgeEndpoint(nodeId);
                else
                {
                    bool shift = guiShift || Keyboard.current != null && Keyboard.current.shiftKey.isPressed;
                    if (shift) { if (!selectedNodes.Add(nodeId)) selectedNodes.Remove(nodeId); }
                    else if (!selectedNodes.Contains(nodeId)) { selectedNodes.Clear(); selectedNodes.Add(nodeId); }
                    selectedEdge = -1; draggingNodes = selectedNodes.Contains(nodeId); dragRecorded = false; lastPointer = local;
                }
            }
            else
            {
                int hitEdge = HitEdge(local);
                if (hitEdge >= 0) { selectedNodes.Clear(); selectedEdge = hitEdge; edgeStart = -1; }
                else if (e.clickCount >= 2) AddNode((local - pan) / zoom);
                else ResetSelection();
            }
            Refresh();
        }

        public void CanvasBeginDrag(Vector2 local, PointerEventData e) { lastPointer = local; }
        public void CanvasDrag(Vector2 local, PointerEventData e)
        {
            if (panning) { pan += local - lastPointer; lastPointer = local; Refresh(); return; }
            if (!draggingNodes || e.button != PointerEventData.InputButton.Left) return;
            Vector2 delta = (local - lastPointer) / zoom;
            if (delta.sqrMagnitude < .0001f) return;
            if (!dragRecorded) { Record(); dragRecorded = true; }
            foreach (int id in selectedNodes) document.FindNode(id).position += delta;
            lastPointer = local; Refresh();
        }
        public void CanvasEndDrag(PointerEventData e) { panning = draggingNodes = false; }
        public void CanvasScroll(Vector2 local, float delta)
        {
            Vector2 world = (local - pan) / zoom;
            zoom = Mathf.Clamp(zoom * (delta > 0 ? 1.1f : .9f), .35f, 2.5f);
            pan = local - world * zoom;
            Refresh();
        }

        private int HitNode(Vector2 local)
        {
            for (int i = document.nodes.Count - 1; i >= 0; i--)
            {
                var node = document.nodes[i];
                if ((node.position * zoom + pan - local).sqrMagnitude <= Mathf.Pow(node.size * zoom * .5f, 2)) return node.id;
            }
            return -1;
        }
        private int HitEdge(Vector2 local)
        {
            foreach (var edge in document.edges)
            {
                var a = document.FindNode(edge.from); var b = document.FindNode(edge.to);
                if (a == null || b == null) continue;
                Vector2 start = a.position * zoom + pan, line = (b.position - a.position) * zoom;
                if (line.sqrMagnitude < 1) continue;
                float t = Mathf.Clamp01(Vector2.Dot(local - start, line) / line.sqrMagnitude);
                if ((local - start - line * t).sqrMagnitude < 64) return edge.id;
            }
            return -1;
        }

        private void SelectEdgeEndpoint(int id)
        {
            if (edgeStart < 0) { edgeStart = id; status = "Choose destination node."; Refresh(); return; }
            if (edgeStart == id) { edgeStart = -1; Refresh(); return; }
            Record();
            if (!document.TryAddEdge(edgeStart, id, out string reason)) { undo.RemoveAt(undo.Count - 1); status = reason; }
            else status = "Edge created.";
            edgeStart = -1; Refresh();
        }

        private void AddNode(Vector2 position)
        {
            int key = 0;
            if (document.kind == GraphKind.BinarySearchTree && !int.TryParse(keyInput != null ? keyInput.text : "", out key)) { status = "Enter an integer BST key."; Refresh(); return; }
            Record();
            var node = document.AddNode(position, key);
            if (node == null) { undo.RemoveAt(undo.Count - 1); status = "Duplicate BST key."; Refresh(); return; }
            selectedNodes.Clear(); selectedNodes.Add(node.id); selectedEdge = -1;
            if (document.kind == GraphKind.BinarySearchTree && keyInput != null) keyInput.SetTextWithoutNotify((key + 1).ToString());
            status = "Node #" + node.id + " created."; Refresh();
        }
        public void AddNodeFromButton() { AddNode(-pan / zoom); }
        private void DeleteSelected()
        {
            if (selectedNodes.Count == 0 && selectedEdge < 0) return;
            if (document.kind == GraphKind.BinarySearchTree && selectedNodes.Count == 0) { status = "BST edges are managed by key order."; Refresh(); return; }
            Record();
            if (selectedNodes.Count > 0) document.RemoveNodes(selectedNodes);
            else document.edges.RemoveAll(edge => edge.id == selectedEdge);
            ResetSelection(); status = "Selection deleted."; Refresh();
        }
        private void ChangeKind(GraphKind kind)
        {
            if (document.kind == kind) return;
            Record(); document = new GraphDocument { kind = kind };
            ResetSelection(); status = kind + " document. Undo restores the previous document."; Refresh();
        }
        private void ClearDocument()
        {
            if (document.nodes.Count == 0 && document.edges.Count == 0) return;
            Record(); document = new GraphDocument { kind = document.kind };
            ResetSelection(); status = "Document cleared."; Refresh();
        }
        private void ResetSelection() { selectedNodes.Clear(); selectedEdge = edgeStart = -1; }
        private int FirstNode() { foreach (int id in selectedNodes) return id; return -1; }
        private void Record()
        {
            undo.Add(JsonUtility.ToJson(document));
            if (undo.Count > 100) undo.RemoveAt(0);
            redo.Clear();
        }
        private void Undo()
        {
            if (undo.Count == 0) return;
            redo.Add(JsonUtility.ToJson(document)); document = JsonUtility.FromJson<GraphDocument>(undo[undo.Count - 1]); undo.RemoveAt(undo.Count - 1);
            ResetSelection(); status = "Undo"; Refresh();
        }
        private void Redo()
        {
            if (redo.Count == 0) return;
            undo.Add(JsonUtility.ToJson(document)); document = JsonUtility.FromJson<GraphDocument>(redo[redo.Count - 1]); redo.RemoveAt(redo.Count - 1);
            ResetSelection(); status = "Redo"; Refresh();
        }
        private string SavePath { get { return Path.Combine(Application.persistentDataPath, "graph-document.json"); } }
        private void Save()
        {
            try { File.WriteAllText(SavePath, JsonUtility.ToJson(document, true)); status = "Saved: " + SavePath; }
            catch (Exception ex) { status = "Save failed: " + ex.Message; }
            Refresh();
        }
        private void Load()
        {
            try
            {
                if (!File.Exists(SavePath)) { status = "No saved document."; Refresh(); return; }
                string reason = "Invalid document.";
                var loaded = JsonUtility.FromJson<GraphDocument>(File.ReadAllText(SavePath));
                if (loaded == null || !loaded.Validate(out reason)) { status = "Load failed: " + reason; Refresh(); return; }
                Record(); document = loaded; ResetSelection(); status = "Document loaded.";
            }
            catch (Exception ex) { status = "Load failed: " + ex.Message; }
            Refresh();
        }

        private void ChangeName(string value)
        {
            if (selectedNodes.Count != 1) return;
            var node = document.FindNode(FirstNode());
            if (node == null || node.name == value) return;
            Record(); node.name = value; Refresh();
        }
        private void ChangeWeight(string value)
        {
            var edge = document.FindEdge(selectedEdge);
            if (edge == null || !float.TryParse(value, out float weight) || float.IsNaN(weight) || float.IsInfinity(weight)) return;
            Record(); edge.weight = weight; Refresh();
        }
        private void ChangeSize(float value)
        {
            if (selectedNodes.Count != 1) return;
            var node = document.FindNode(FirstNode());
            if (node == null || Mathf.Abs(node.size - value) < .1f) return;
            Record(); node.size = value; Refresh();
        }
        private void ChangeThickness(float value)
        {
            var edge = document.FindEdge(selectedEdge);
            if (edge == null || Mathf.Abs(edge.thickness - value) < .1f) return;
            Record(); edge.thickness = value; Refresh();
        }
        private void ChangeColor(Color color)
        {
            if (selectedNodes.Count == 0 && selectedEdge < 0) return;
            Record();
            if (selectedNodes.Count > 0) foreach (int id in selectedNodes) document.FindNode(id).color = color;
            else document.FindEdge(selectedEdge).color = color;
            Refresh();
        }
        private void Refresh()
        {
            if (surface != null) surface.SetVerticesDirty();
            if (labels != null) labels.SetVerticesDirty();
            if (statsLabel != null) statsLabel.text = string.Format("Nodes {0}    Edges {1}    Zoom {2:0}%", document.nodes.Count, document.edges.Count, zoom * 100);
            if (statusLabel != null) statusLabel.text = status;
            if (inspectorSummary != null)
            {
                if (selectedNodes.Count > 1) inspectorSummary.text = selectedNodes.Count + " nodes selected";
                else if (selectedNodes.Count == 1) { var node = document.FindNode(FirstNode()); inspectorSummary.text = "Node #" + node.id + (document.kind == GraphKind.BinarySearchTree ? "  Key " + node.key : ""); }
                else if (selectedEdge >= 0) { var edge = document.FindEdge(selectedEdge); inspectorSummary.text = edge == null ? "Select a node or edge" : "Edge #" + edge.id + "   " + edge.from + " > " + edge.to; }
                else inspectorSummary.text = "Select a node or edge";
            }
            if (nameInput != null) nameInput.SetTextWithoutNotify(selectedNodes.Count == 1 ? document.FindNode(FirstNode()).name : "");
            if (weightInput != null) weightInput.SetTextWithoutNotify(selectedEdge >= 0 ? document.FindEdge(selectedEdge).weight.ToString() : "");
            if (sizeSlider != null && selectedNodes.Count == 1) sizeSlider.SetValueWithoutNotify(document.FindNode(FirstNode()).size);
            if (thicknessSlider != null && selectedEdge >= 0) thicknessSlider.SetValueWithoutNotify(document.FindEdge(selectedEdge).thickness);
        }
    }
}
