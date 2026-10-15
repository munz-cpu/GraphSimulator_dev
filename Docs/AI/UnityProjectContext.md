# Unity project context

- Analyzed: 2026-10-15. This folder is not a Git checkout, so no commit is available.
- Unity 6000.6.3f1, URP 17.6 with 2D renderer. Input System 1.19 is active (`activeInputHandler: 1`). uGUI 2.6 and Unity Test Framework 1.8 are installed.
- Startup scene: `Assets/Scenes/SampleScene.unity`, the sole enabled build scene. Existing scene and settings assets are preserved.
- First-party feature code: `Assets/GraphEditor/GraphDocument.cs` owns serializable graph data and structural rules; `Assets/GraphEditor/GraphEditorRuntime.cs` handles scene UI events, undo, and JSON persistence; `Assets/GraphEditor/GraphCanvasGraphic.cs` draws node and edge shapes on CanvasSurface, while `GraphCanvasLabels.cs` draws their text on its authored LabelsOverlay child. Canvas, panels, buttons, input fields, sliders, and labels are authored GameObjects in `SampleScene`. No GameObject is created by runtime scripts.
- No custom runtime asmdef or existing test assembly was found. The 2D template's `Assets/Welcome` is separate template content.
- Unity Pipeline MCP is available and has been used for console, compilation, Play Mode, and Game view inspection.
- A saved graph is written to `Application.persistentDataPath/graph-document.json` when the user presses JSON 저장. Mode changes begin a new document and can be undone.
- Current scope excludes traversal, search, sorting, and shortest path visualizations.
