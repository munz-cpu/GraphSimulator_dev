using GraphSimulator;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

internal static class ApplyLightTheme
{
    private static Color Rgb(byte r, byte g, byte b) => new Color32(r, g, b, 255);

    [MenuItem("Tools/Graph Editor/Apply Light Theme")]
    private static void Apply()
    {
        Scene scene = SceneManager.GetActiveScene();
        var canvasObject = GameObject.Find("Canvas");
        var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/SeoulNamsanB.ttf");
        if (canvasObject == null || font == null)
        {
            Debug.LogError("Graph theme: Canvas or SeoulNamsanB.ttf is missing.");
            return;
        }

        int textCount = 0;
        foreach (var label in canvasObject.GetComponentsInChildren<Text>(true))
        {
            Undo.RecordObject(label, "Apply graph font and text color");
            label.font = font;
            label.color = TextColor(label);
            label.fontSize = TextSize(label);
            if (label.transform.parent != null && label.transform.parent.GetComponent<InputField>() != null)
            {
                var rect = label.rectTransform;
                Undo.RecordObject(rect, "Fit graph input text");
                rect.sizeDelta = new Vector2(rect.sizeDelta.x, -4);
                EditorUtility.SetDirty(rect);
            }
            EditorUtility.SetDirty(label);
            textCount++;
        }

        int imageCount = 0;
        foreach (var image in canvasObject.GetComponentsInChildren<Image>(true))
        {
            if (!TryImageColor(image, out Color color)) continue;
            Undo.RecordObject(image, "Apply graph UI color");
            image.color = color;
            if (IsPanelImage(image))
            {
                image.sprite = null;
                image.type = Image.Type.Simple;
            }
            EditorUtility.SetDirty(image);
            imageCount++;
        }

        foreach (var button in canvasObject.GetComponentsInChildren<Button>(true))
        {
            if (button.name.StartsWith("ColorButton")) continue;
            Undo.RecordObject(button, "Apply graph button states");
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Rgb(240, 242, 244);
            colors.pressedColor = Rgb(220, 223, 227);
            colors.selectedColor = Rgb(238, 240, 242);
            colors.disabledColor = new Color(.72f, .74f, .77f, .6f);
            button.colors = colors;
            EditorUtility.SetDirty(button);
        }

        var nodeLabels = canvasObject.GetComponentInChildren<GraphCanvasLabels>(true);
        if (nodeLabels != null)
        {
            var serialized = new SerializedObject(nodeLabels);
            serialized.FindProperty("font").objectReferenceValue = font;
            serialized.ApplyModifiedProperties();
            nodeLabels.SetVerticesDirty();
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        var sceneView = SceneView.lastActiveSceneView;
        if (sceneView != null)
        {
            var canvasRect = canvasObject.GetComponent<RectTransform>();
            var corners = new Vector3[4];
            canvasRect.GetWorldCorners(corners);
            var bounds = new Bounds((corners[0] + corners[2]) * .5f, corners[2] - corners[0]);
            sceneView.in2DMode = true;
            sceneView.pivot = bounds.center;
            var aspect = sceneView.camera != null ? sceneView.camera.aspect : 16f / 9f;
            sceneView.size = Mathf.Max(bounds.extents.y, bounds.extents.x / aspect) * 1.18f;
            sceneView.Repaint();
        }
        Debug.Log($"Graph light theme applied: {textCount} text objects, {imageCount} images, node labels use {font.name}.");
    }

    private static Color TextColor(Text label)
    {
        if (label.name == "Placeholder") return Rgb(111, 119, 128);
        if (label.name == "StatsText" || label.name == "StatusText" ||
            label.name == "ToolsHint" || label.name == "PanHint") return Rgb(53, 60, 68);
        if (label.name == "TitleText" || label.name.EndsWith("Heading")) return Rgb(22, 26, 31);
        if (label.transform.parent != null && label.transform.parent.GetComponent<Button>() != null)
            return Rgb(26, 31, 37);
        return Rgb(36, 42, 49);
    }

    private static int TextSize(Text label)
    {
        if (label.name == "TitleText") return 22;
        if (label.name.EndsWith("Heading")) return 20;
        if (label.name == "ToolsHint" || label.name == "PanHint" ||
            label.name == "StatsText" || label.name == "StatusText") return 14;
        if (label.name == "Placeholder") return 15;
        if (label.transform.parent != null && label.transform.parent.GetComponent<Button>() != null) return 16;
        return 16;
    }

    private static bool IsPanelImage(Image image)
    {
        string name = image.name;
        return name == "TopBar" || name == "BottomBar" || name == "LeftPanel" ||
            name == "RightPanel" || name == "Background" && image.transform.parent != null &&
            image.transform.parent.name == "Canvas";
    }

    private static bool TryImageColor(Image image, out Color color)
    {
        string name = image.name;
        Transform parent = image.transform.parent;
        color = Color.white;
        if (name == "Background" && parent != null && parent.name == "Canvas")
            color = Rgb(242, 244, 246);
        else if (name == "TopBar" || name == "BottomBar")
            color = Rgb(230, 233, 236);
        else if (name == "LeftPanel" || name == "RightPanel")
            color = Rgb(248, 249, 250);
        else if (image.GetComponent<Button>() is Button button)
        {
            if (button.name.StartsWith("ColorButton")) return false;
            color = button.name == "ClearButton" ? Rgb(216, 220, 224) : Rgb(226, 229, 232);
        }
        else if (image.GetComponent<InputField>() != null)
            color = Color.white;
        else if (name == "Background" && parent != null && parent.GetComponent<Slider>() != null)
            color = Rgb(212, 216, 220);
        else if (name == "Fill")
            color = Rgb(127, 135, 143);
        else if (name == "Handle")
            color = Rgb(87, 95, 103);
        else return false;
        return true;
    }
}
