# Graph Studio

Open `Assets/Scenes/SampleScene.unity`. The interface is made of Canvas, panel, button, input, slider, and text objects placed in the scene. Frame the Canvas in Scene view to see the complete layout. The Game view renders those same objects. The `Graph Editor` controller handles interactions and never creates GameObjects at runtime. Nodes and edges are data rendered by the existing `CanvasSurface` object.

- Double-click empty canvas space or press **Add Node**. For BST, enter an integer key first. Existing BST keys cannot be edited; insertion creates the parent edge automatically.
- Drag a node to move it. Shift-click nodes to select several, then drag or choose a color in the right panel.
- Press **Edge Tool**, then click the source and destination nodes. Trees use source as parent. Directed edges show an arrow. Weighted graph edges have an editable weight.
- Click an edge or node to inspect it. Delete removes the selection. BST node deletion rebuilds the remaining links while preserving node IDs, labels, colors, sizes, and positions.
- Scroll over the canvas to zoom. Drag with the middle mouse button to pan. Ctrl+Z and Ctrl+Y undo and redo. **Clear All** empties the current mode.
- **Save JSON** and **Load JSON** use `Application.persistentDataPath/graph-document.json`. The saved format includes IDs, positions, colors, sizes, edge endpoints, weights, and thicknesses.

Changing the structure type opens an empty document. Undo restores the previous mode and content.
