using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

internal static class SpecialShopEternalRewardCellGenerator
{
    private const string CellPrefabPath =
        "Assets/Igrodelnya2.0/SpecialShop/CardTemplates/Active/SpecialShopEternalRewardCell.prefab";
    private const string EternalCardPath =
        "Assets/Igrodelnya2.0/SpecialShop/CardTemplates/Active/SpecialShopCard_EternalPack.prefab";
    private const string FontPath =
        "Assets/Igrodelnya2.0/Fonts/RussoOne-Regular Cyrillic SDF.asset";

    [InitializeOnLoadMethod]
    private static void ScheduleInitialCreation()
    {
        EditorApplication.delayCall += CreateIfMissing;
    }

    [MenuItem("Tools/Special Shop/Create/Assign Eternal Reward Cell Template")]
    public static void CreateOrAssign()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(CellPrefabPath) == null)
            CreateCellPrefab();

        AssignToEternalCard();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[SpecialShop] Eternal reward visual template is ready: {CellPrefabPath}");
    }

    private static void CreateIfMissing()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            AssetDatabase.LoadAssetAtPath<GameObject>(CellPrefabPath) != null)
            return;

        CreateOrAssign();
    }

    private static void CreateCellPrefab()
    {
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        Sprite uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        GameObject root = new(
            "SpecialShopEternalRewardCell",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button),
            typeof(LayoutElement),
            typeof(CanvasGroup),
            typeof(SpecialShopEternalRewardCellView));

        try
        {
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(150f, 150f);

            Image background = root.GetComponent<Image>();
            background.sprite = uiSprite;
            background.type = Image.Type.Sliced;
            background.color = new Color(0.08f, 0.72f, 0.95f, 1f);

            Button button = root.GetComponent<Button>();
            button.targetGraphic = background;

            LayoutElement layout = root.GetComponent<LayoutElement>();
            layout.minWidth = 115f;
            layout.preferredWidth = 150f;
            layout.flexibleWidth = 0f;

            Image rewardIcon = CreateImage(
                root.transform,
                "Icon",
                new Vector2(0.18f, 0.29f),
                new Vector2(0.82f, 0.91f));
            rewardIcon.color = new Color(1f, 1f, 1f, 0.35f);

            TextMeshProUGUI amount = CreateText(
                root.transform,
                "Amount",
                "x15",
                font,
                25f,
                TextAlignmentOptions.TopRight,
                new Vector2(0.52f, 0.68f),
                new Vector2(0.97f, 0.97f));

            Image priceCurrencyIcon = CreateImage(
                root.transform,
                "PriceCurrencyIcon",
                new Vector2(0.13f, 0.045f),
                new Vector2(0.36f, 0.265f));
            priceCurrencyIcon.color = new Color(1f, 1f, 1f, 0.35f);

            TextMeshProUGUI price = CreateText(
                root.transform,
                "Price",
                "FREE",
                font,
                22f,
                TextAlignmentOptions.Center,
                new Vector2(0.37f, 0.02f),
                new Vector2(0.97f, 0.28f));
            price.color = new Color(0.92f, 1f, 0.28f, 1f);

            TextMeshProUGUI next = CreateText(
                root.transform,
                "Next",
                "NEXT",
                font,
                16f,
                TextAlignmentOptions.TopLeft,
                new Vector2(0.03f, 0.70f),
                new Vector2(0.40f, 0.97f));
            next.color = new Color(0.88f, 0.92f, 1f, 1f);

            SpecialShopEternalRewardCellView view = root.GetComponent<SpecialShopEternalRewardCellView>();
            SerializedObject serializedView = new(view);
            Set(serializedView, "_background", background);
            Set(serializedView, "_rewardIcon", rewardIcon);
            Set(serializedView, "_amountText", amount);
            Set(serializedView, "_priceText", price);
            Set(serializedView, "_priceCurrencyIcon", priceCurrencyIcon);
            Set(serializedView, "_nextText", next);
            Set(serializedView, "_button", button);
            serializedView.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, CellPrefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void AssignToEternalCard()
    {
        GameObject cellPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CellPrefabPath);
        SpecialShopEternalRewardCellView cellView =
            cellPrefab != null ? cellPrefab.GetComponent<SpecialShopEternalRewardCellView>() : null;
        if (cellView == null)
            throw new InvalidOperationException($"Reward cell view is missing in {CellPrefabPath}");

        GameObject cardRoot = PrefabUtility.LoadPrefabContents(EternalCardPath);
        try
        {
            SpecialShopEternalTrackView track =
                cardRoot.GetComponentInChildren<SpecialShopEternalTrackView>(true);
            if (track == null)
                throw new InvalidOperationException($"Eternal track is missing in {EternalCardPath}");

            SerializedObject serializedTrack = new(track);
            Set(serializedTrack, "_cellTemplate", cellView);
            serializedTrack.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(cardRoot, EternalCardPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(cardRoot);
        }
    }

    private static Image CreateImage(
        Transform parent,
        string name,
        Vector2 anchorMin,
        Vector2 anchorMax)
    {
        GameObject child = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        child.transform.SetParent(parent, false);
        RectTransform rect = child.GetComponent<RectTransform>();
        Stretch(rect, anchorMin, anchorMax);

        Image image = child.GetComponent<Image>();
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }

    private static TextMeshProUGUI CreateText(
        Transform parent,
        string name,
        string value,
        TMP_FontAsset font,
        float size,
        TextAlignmentOptions alignment,
        Vector2 anchorMin,
        Vector2 anchorMax)
    {
        GameObject child = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        child.transform.SetParent(parent, false);
        RectTransform rect = child.GetComponent<RectTransform>();
        Stretch(rect, anchorMin, anchorMax);

        TextMeshProUGUI text = child.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.font = font;
        text.fontSize = size;
        text.enableAutoSizing = true;
        text.fontSizeMin = Mathf.Max(10f, size * 0.55f);
        text.fontSizeMax = size;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;
        text.enableWordWrapping = false;
        text.outlineWidth = 0.18f;
        text.outlineColor = new Color32(20, 20, 20, 255);
        return text;
    }

    private static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void Set(SerializedObject serializedObject, string fieldName, UnityEngine.Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(fieldName);
        if (property == null)
            throw new MissingFieldException(serializedObject.targetObject.GetType().Name, fieldName);
        property.objectReferenceValue = value;
    }
}
