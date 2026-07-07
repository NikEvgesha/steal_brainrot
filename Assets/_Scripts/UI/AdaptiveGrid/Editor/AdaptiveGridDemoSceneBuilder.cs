#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class AdaptiveGridDemoSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/AdaptiveGridDemo.unity";
    private const string PrefabFolder = "Assets/_Prefabs/UI/AdaptiveGrid";
    private const string ItemPrefabPath = PrefabFolder + "/AdaptiveGridDemoItem.prefab";

    [MenuItem("Tools/UI/Build Adaptive Grid Demo Scene")]
    public static void BuildDemoScene()
    {
        EnsureFolder("Assets/Scenes");
        EnsureFolder(PrefabFolder);

        var itemPrefab = CreateOrUpdateItemPrefab();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        CreateCamera();
        var canvas = CreateCanvas();
        CreateEventSystem();

        CreateText(
            "Title",
            canvas.transform,
            "AdaptiveGridSpawner demo",
            new Vector2(0.05f, 0.9f),
            new Vector2(0.95f, 0.98f),
            38,
            TextAnchor.MiddleCenter,
            Color.white);

        CreateText(
            "Description",
            canvas.transform,
            "Left: fixed cell size calculates capacity. Right: target item count adapts cell size.",
            new Vector2(0.05f, 0.845f),
            new Vector2(0.95f, 0.9f),
            22,
            TextAnchor.MiddleCenter,
            new Color(0.9f, 0.95f, 1f, 1f));

        var fixedPanel = CreatePanel(
            "FixedSizeExample",
            canvas.transform,
            new Vector2(0.045f, 0.08f),
            new Vector2(0.485f, 0.82f),
            new Color(0.08f, 0.12f, 0.16f, 0.94f));

        CreateText(
            "FixedTitle",
            fixedPanel.transform,
            "FixedCellSize",
            new Vector2(0.04f, 0.88f),
            new Vector2(0.96f, 0.98f),
            28,
            TextAnchor.MiddleCenter,
            Color.white);

        var fixedGridObject = CreatePanel(
            "FixedGrid",
            fixedPanel.transform,
            new Vector2(0.06f, 0.07f),
            new Vector2(0.94f, 0.84f),
            new Color(0.02f, 0.025f, 0.03f, 0.78f));
        var fixedGrid = fixedGridObject.gameObject.AddComponent<AdaptiveGridSpawner>();
        fixedGrid.SetItemPrefab(itemPrefab);
        fixedGrid.ConfigureFixed(
            new Vector2(104f, 104f),
            new Vector2(10f, 10f),
            new RectOffset(14, 14, 14, 14),
            true);
        fixedGrid.EnsureSpawnedItemCount(36);
        LabelItems(fixedGridObject.transform, "F");
        fixedGrid.Rebuild();

        var dynamicPanel = CreatePanel(
            "FitCountExample",
            canvas.transform,
            new Vector2(0.515f, 0.08f),
            new Vector2(0.955f, 0.82f),
            new Color(0.12f, 0.08f, 0.16f, 0.94f));

        CreateText(
            "DynamicTitle",
            dynamicPanel.transform,
            "FitItemCount",
            new Vector2(0.04f, 0.88f),
            new Vector2(0.96f, 0.98f),
            28,
            TextAnchor.MiddleCenter,
            Color.white);

        var dynamicGridObject = CreatePanel(
            "FitCountGrid",
            dynamicPanel.transform,
            new Vector2(0.06f, 0.07f),
            new Vector2(0.94f, 0.84f),
            new Color(0.025f, 0.02f, 0.03f, 0.78f));
        var dynamicGrid = dynamicGridObject.gameObject.AddComponent<AdaptiveGridSpawner>();
        dynamicGrid.SetItemPrefab(itemPrefab);
        dynamicGrid.ConfigureFitItemCount(
            18,
            new Vector2(10f, 10f),
            new RectOffset(14, 14, 14, 14),
            new Vector2(54f, 54f),
            new Vector2(150f, 150f));
        dynamicGrid.ConfigurePercentLayout(
            new Vector2(1.2f, 1.6f),
            new Vector4(1.2f, 1.2f, 1.6f, 1.6f));
        dynamicGrid.EnsureSpawnedItemCount(18);
        LabelItems(dynamicGridObject.transform, "D");
        dynamicGrid.Rebuild();

        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[AdaptiveGridDemo] Scene created: " + ScenePath);
    }

    private static GameObject CreateOrUpdateItemPrefab()
    {
        var root = new GameObject("AdaptiveGridDemoItem", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rect = root.transform as RectTransform;
        rect.sizeDelta = new Vector2(120f, 120f);

        var image = root.GetComponent<Image>();
        image.color = new Color(0.08f, 0.52f, 0.95f, 1f);
        image.raycastTarget = false;

        var outline = root.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(3f, -3f);

        CreateText(
            "Label",
            root.transform,
            "1",
            Vector2.zero,
            Vector2.one,
            28,
            TextAnchor.MiddleCenter,
            Color.white);

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, ItemPrefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static Canvas CreateCanvas()
    {
        var canvasObject = new GameObject("AdaptiveGridDemoCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var background = canvasObject.AddComponent<Image>();
        background.color = new Color(0.025f, 0.04f, 0.055f, 1f);
        background.raycastTarget = false;

        var rect = canvasObject.transform as RectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return canvas;
    }

    private static void CreateCamera()
    {
        var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        cameraObject.transform.position = new Vector3(0f, 0f, -10f);

        var camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.025f, 0.04f, 0.055f, 1f);
        camera.orthographic = true;
        camera.orthographicSize = 5f;

        if (UnityEditorInternal.InternalEditorUtility.tags != null)
            cameraObject.tag = "MainCamera";
    }

    private static void CreateEventSystem()
    {
        var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        eventSystem.transform.position = Vector3.zero;
    }

    private static Image CreatePanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Color color)
    {
        var panel = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(parent, false);

        var rect = panel.transform as RectTransform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var image = panel.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;

        var outline = panel.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(3f, -3f);
        return image;
    }

    private static Text CreateText(
        string name,
        Transform parent,
        string value,
        Vector2 anchorMin,
        Vector2 anchorMax,
        int fontSize,
        TextAnchor alignment,
        Color color)
    {
        var textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textObject.transform.SetParent(parent, false);

        var rect = textObject.transform as RectTransform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var text = textObject.GetComponent<Text>();
        text.text = value;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.fontStyle = FontStyle.Bold;
        text.alignment = alignment;
        text.color = color;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 12;
        text.resizeTextMaxSize = fontSize;
        text.raycastTarget = false;

        var outline = textObject.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(2f, -2f);
        return text;
    }

    private static void LabelItems(Transform gridRoot, string prefix)
    {
        var index = 1;
        for (var i = 0; i < gridRoot.childCount; i++)
        {
            var child = gridRoot.GetChild(i);
            var label = child.GetComponentInChildren<Text>(true);
            if (label != null)
                label.text = prefix + index;

            var image = child.GetComponent<Image>();
            if (image != null)
            {
                var hue = Mathf.Repeat(index * 0.075f, 1f);
                image.color = Color.HSVToRGB(hue, 0.76f, 0.96f);
            }

            index++;
        }
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        var parent = Path.GetDirectoryName(path);
        var name = Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent))
        {
            parent = parent.Replace("\\", "/");
            EnsureFolder(parent.Replace("\\", "/"));
        }
        AssetDatabase.CreateFolder(parent, name);
    }
}
#endif
