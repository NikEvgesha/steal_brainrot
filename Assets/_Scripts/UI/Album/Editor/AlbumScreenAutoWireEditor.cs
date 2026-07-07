#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class AlbumScreenAutoWireEditor
{
    [MenuItem("Tools/Album/Build Missing Layout For Selected AlbumScreen")]
    private static void BuildMissingLayoutForSelectedAlbumScreen()
    {
        var selection = Selection.activeGameObject;
        if (selection == null)
        {
            Debug.LogWarning("[AlbumAutoWire] Select AlbumScreen object in hierarchy first.");
            return;
        }

        var controller = selection.GetComponent<AlbumScreenController>();
        if (controller == null)
        {
            Debug.LogWarning("[AlbumAutoWire] Selected object has no AlbumScreenController.");
            return;
        }

        var root = controller.transform;
        var panelRoot = FindByName(root, "panelRoot");
        if (panelRoot == null)
            panelRoot = EnsureChild(root, "panelRoot");

        var header = EnsureChild(panelRoot, "Header");
        EnsureTabButton(header, "EggsTabButton", "EggsTabText", "EggsTabMention");
        EnsureTabButton(header, "AnimalsTabButton", "AnimalsTabText", "AnimalsTabMention");
        EnsureSimpleButton(header, "CloseButton", "CloseButtonText", "X");
        EnsureBadge(header, "AlbumIconMention");

        EnsureChild(panelRoot, "cardsRoot");
        EnsureChild(panelRoot, "rareTabsRoot");

        var infoPanel = EnsureChild(panelRoot, "InfoPanel");
        EnsureImageObject(infoPanel, "InfoIcon");
        EnsureTextObject(infoPanel, "InfoTitle", "Title");
        EnsureTextObject(infoPanel, "InfoDescription", "Description");
        EnsureTextObject(infoPanel, "InfoIncome", "Income");
        EnsureTextObject(infoPanel, "InfoSources", "Sources");
        var hatchSection = EnsureChild(infoPanel, "EggHatchSection");
        var hatchIconsRoot = EnsureChild(hatchSection, "EggHatchIconsRoot");
        var hatchTemplate = EnsureImageObject(hatchIconsRoot, "EggHatchIconTemplate");
        hatchTemplate.gameObject.SetActive(false);
        EnsureOverlayChild(infoPanel, "InfoLockedOverlay", new Color(0f, 0f, 0f, 0.55f));
        EnsureTextObject(infoPanel, "InfoLockedText", "???");

        var rewardBlock = EnsureChild(infoPanel, "RewardBlock");
        EnsureSimpleButton(rewardBlock, "RewardButton", "RewardButtonText", "Claim");
        EnsureBadge(rewardBlock, "RewardMentionBadge");

        AutoWireSelectedAlbumScreen();
        Debug.Log("[AlbumAutoWire] Missing layout created and references auto-wired.");
    }

    [MenuItem("Tools/Album/Auto Wire Selected AlbumScreen")]
    private static void AutoWireSelectedAlbumScreen()
    {
        var selection = Selection.activeGameObject;
        if (selection == null)
        {
            Debug.LogWarning("[AlbumAutoWire] Select AlbumScreen object in hierarchy first.");
            return;
        }

        var controller = selection.GetComponent<AlbumScreenController>();
        if (controller == null)
        {
            Debug.LogWarning("[AlbumAutoWire] Selected object has no AlbumScreenController.");
            return;
        }

        Undo.RecordObject(controller, "Auto Wire AlbumScreenController");
        AutoWireController(controller);
        EditorUtility.SetDirty(controller);

        AutoWireNestedViews(selection.transform);

        Debug.Log("[AlbumAutoWire] Done. Re-open inspector to verify links.");
    }

    [MenuItem("Tools/Album/Validate Selected AlbumScreen")]
    private static void ValidateSelectedAlbumScreen()
    {
        var selection = Selection.activeGameObject;
        if (selection == null)
        {
            Debug.LogWarning("[AlbumAutoWire] Select AlbumScreen object in hierarchy first.");
            return;
        }

        var controller = selection.GetComponent<AlbumScreenController>();
        if (controller == null)
        {
            Debug.LogWarning("[AlbumAutoWire] Selected object has no AlbumScreenController.");
            return;
        }

        var so = new SerializedObject(controller);
        var missing = new List<string>();

        CheckRequiredObjectRef(so, "panelRoot", missing);
        CheckRequiredObjectRef(so, "cardsRoot", missing);
        CheckRequiredObjectRef(so, "cardsAdaptiveGrid", missing);
        CheckRequiredObjectRef(so, "cardPrefab", missing);
        CheckRequiredObjectRef(so, "rareTabsRoot", missing);
        CheckRequiredObjectRef(so, "rareTabPrefab", missing);
        CheckRequiredObjectRef(so, "eggsTabButton", missing);
        CheckRequiredObjectRef(so, "animalsTabButton", missing);
        CheckRequiredObjectRef(so, "closeButton", missing);
        CheckRequiredObjectRef(so, "eggsTabText", missing);
        CheckRequiredObjectRef(so, "animalsTabText", missing);
        CheckRequiredObjectRef(so, "eggsTabMention", missing);
        CheckRequiredObjectRef(so, "animalsTabMention", missing);
        CheckRequiredObjectRef(so, "albumIconMention", missing);
        CheckRequiredObjectRef(so, "infoIcon", missing);
        CheckRequiredObjectRef(so, "infoTitle", missing);
        CheckRequiredObjectRef(so, "infoDescription", missing);
        CheckRequiredObjectRef(so, "infoIncome", missing);
        CheckRequiredObjectRef(so, "infoSources", missing);
        CheckRequiredObjectRef(so, "infoLockedOverlay", missing);
        CheckRequiredObjectRef(so, "infoLockedText", missing);
        CheckRequiredObjectRef(so, "rewardButton", missing);
        CheckRequiredObjectRef(so, "rewardButtonText", missing);
        CheckRequiredObjectRef(so, "rewardMentionBadge", missing);

        if (missing.Count == 0)
        {
            Debug.Log("[AlbumAutoWire] Validation passed. Required references are assigned.");
            return;
        }

        Debug.LogWarning("[AlbumAutoWire] Missing refs:\n- " + string.Join("\n- ", missing));
    }

    private static void AutoWireController(AlbumScreenController controller)
    {
        var root = controller.transform;
        var so = new SerializedObject(controller);

        var panelRoot = FindByName(root, "panelRoot");
        if (panelRoot == null)
            panelRoot = controller.transform;
        SetRef(so, "panelRoot", panelRoot != null ? panelRoot.gameObject : null);

        var cardsContainer = FindByName(root, "cards");
        var cardsRoot = cardsContainer != null ? cardsContainer : FindByName(root, "cardsRoot");
        SetRef(so, "cardsRoot", cardsRoot as RectTransform);

        var cardsAdaptiveGrid = cardsRoot != null ? cardsRoot.GetComponent<AdaptiveGridSpawner>() : null;
        if (cardsAdaptiveGrid == null && cardsRoot != null)
            cardsAdaptiveGrid = cardsRoot.GetComponentInChildren<AdaptiveGridSpawner>(true);
        SetRef(so, "cardsAdaptiveGrid", cardsAdaptiveGrid);

        var cardsDynamicGrid = cardsAdaptiveGrid == null && cardsRoot != null ? cardsRoot.GetComponent<DynamicGridSpawner>() : null;
        if (cardsDynamicGrid == null && cardsAdaptiveGrid == null && cardsRoot != null)
            cardsDynamicGrid = cardsRoot.GetComponentInChildren<DynamicGridSpawner>(true);
        SetRef(so, "cardsDynamicGrid", cardsDynamicGrid);

        var cardPrefab = FindComponentByName<AlbumEntryView>(root, "AlbumEntryView");
        if (cardPrefab != null)
            SetRef(so, "cardPrefab", cardPrefab);

        var rareTabsRoot = FindByName(root, "rareTabsRoot");
        SetRef(so, "rareTabsRoot", rareTabsRoot as RectTransform);
        var rareTabPrefab = FindComponentByName<AlbumRareTabView>(root, "AlbumRareTabView");
        if (rareTabPrefab != null)
            SetRef(so, "rareTabPrefab", rareTabPrefab);

        SetRef(so, "eggsTabButton", FindComponentByName<Button>(root, "EggsTabButton"));
        SetRef(so, "animalsTabButton", FindComponentByName<Button>(root, "AnimalsTabButton"));
        SetRef(so, "closeButton", FindComponentByName<Button>(root, "CloseButton"));
        SetRef(so, "eggsTabText", FindComponentByName<TMP_Text>(root, "EggsTabText"));
        SetRef(so, "animalsTabText", FindComponentByName<TMP_Text>(root, "AnimalsTabText"));
        SetRef(so, "eggsTabBackground", ResolveTopTabBackground(root, "EggsTabButton"));
        SetRef(so, "animalsTabBackground", ResolveTopTabBackground(root, "AnimalsTabButton"));
        SetRef(so, "eggsTabSelectedFrame", ResolveTopTabSelectedFrame(root, "EggsTabButton"));
        SetRef(so, "animalsTabSelectedFrame", ResolveTopTabSelectedFrame(root, "AnimalsTabButton"));
        SetRef(so, "eggsTabMention", FindByName(root, "EggsTabMention")?.gameObject);
        SetRef(so, "animalsTabMention", FindByName(root, "AnimalsTabMention")?.gameObject);
        SetRef(so, "albumIconMention", FindByName(root, "AlbumIconMention")?.gameObject);
        SetRef(so, "cardsSectionTitle",
            FindComponentByName<TMP_Text>(root, "CardName") ??
            FindComponentByName<TMP_Text>(root, "CardsTitle") ??
            FindComponentByName<TMP_Text>(root, "SectionTitle"));

        SetRef(so, "infoIcon", FindComponentByName<Image>(root, "InfoIcon"));
        SetRef(so, "infoTitle", FindComponentByName<TMP_Text>(root, "InfoTitle"));
        SetRef(so, "infoDescription", FindComponentByName<TMP_Text>(root, "InfoDescription"));
        SetRef(so, "infoIncome", FindComponentByName<TMP_Text>(root, "InfoIncome"));
        SetRef(so, "infoSources", FindComponentByName<TMP_Text>(root, "InfoSources"));

        var hatchSection = FindByName(root, "EggHatchSection") ?? FindByName(root, "HatchSection");
        var hatchIconsRoot = FindByName(root, "EggHatchIconsRoot") ??
                             FindByName(root, "HatchIconsRoot") ??
                             FindByName(root, "InfoHatchIcons");
        Image hatchTemplate = null;
        if (hatchIconsRoot != null)
        {
            hatchTemplate = FindComponentByName<Image>(hatchIconsRoot, "EggHatchIconTemplate") ??
                            FindComponentByName<Image>(hatchIconsRoot, "HatchIconTemplate");
        }
        SetRef(so, "eggHatchSection", hatchSection != null ? hatchSection.gameObject : null);
        SetRef(so, "eggHatchIconsRoot", hatchIconsRoot as RectTransform);
        SetRef(so, "eggHatchIconTemplate", hatchTemplate);

        SetRef(so, "infoLockedOverlay", FindByName(root, "InfoLockedOverlay")?.gameObject);
        SetRef(so, "infoLockedText", FindComponentByName<TMP_Text>(root, "InfoLockedText"));

        SetRef(so, "rewardButton", FindComponentByName<Button>(root, "RewardButton"));
        SetRef(so, "rewardButtonText", FindComponentByName<TMP_Text>(root, "RewardButtonText"));
        SetRef(so, "rewardMentionBadge", FindByName(root, "RewardMentionBadge")?.gameObject);

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AutoWireNestedViews(Transform root)
    {
        var entryViews = root.GetComponentsInChildren<AlbumEntryView>(true);
        for (var i = 0; i < entryViews.Length; i++)
            AutoWireEntryView(entryViews[i]);

        var rareViews = root.GetComponentsInChildren<AlbumRareTabView>(true);
        for (var i = 0; i < rareViews.Length; i++)
            AutoWireRareTabView(rareViews[i]);
    }

    private static void AutoWireEntryView(AlbumEntryView view)
    {
        var so = new SerializedObject(view);
        var root = view.transform;

        var lockOverlay = FindByName(root, "LockOverlay");
        if (lockOverlay == null)
            lockOverlay = EnsureOverlayChild(root, "LockOverlay", new Color(0f, 0f, 0f, 0.55f));

        var selectedFrame = FindComponentByName<Image>(root, "SelectedFrame");
        if (selectedFrame == null)
            selectedFrame = EnsureFrameChild(root, "SelectedFrame", new Color(1f, 1f, 1f, 0.95f));

        SetRef(so, "button", FindComponentByName<Button>(root, "Button"));
        SetRef(so, "iconImage", FindComponentByName<Image>(root, "Image"));
        SetRef(so, "titleText", FindComponentByName<TMP_Text>(root, "Titl") ?? FindComponentByName<TMP_Text>(root, "Title"));
        SetRef(so, "mentionBadge", FindByName(root, "Mention")?.gameObject);
        SetRef(so, "lockOverlay", lockOverlay != null ? lockOverlay.gameObject : null);
        SetRef(so, "selectedFrame", selectedFrame);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(view);
    }

    private static void AutoWireRareTabView(AlbumRareTabView view)
    {
        var so = new SerializedObject(view);
        var root = view.transform;

        var lockOverlay = FindByName(root, "LockOverlay");
        if (lockOverlay == null)
            lockOverlay = EnsureOverlayChild(root, "LockOverlay", new Color(0f, 0f, 0f, 0.55f));

        var selectedFrame = FindComponentByName<Image>(root, "SelectedFrame");
        if (selectedFrame == null)
            selectedFrame = EnsureFrameChild(root, "SelectedFrame", new Color(1f, 1f, 1f, 0.95f));

        SetRef(so, "button", FindComponentByName<Button>(root, "Button"));
        SetRef(so, "titleText", FindComponentByName<TMP_Text>(root, "Text (TMP)") ?? FindComponentByName<TMP_Text>(root, "Text"));
        SetRef(so, "lockOverlay", lockOverlay != null ? lockOverlay.gameObject : null);
        SetRef(so, "mentionBadge", FindByName(root, "Mention")?.gameObject);
        SetRef(so, "selectedFrame", selectedFrame);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(view);
    }

    private static void CheckRequiredObjectRef(SerializedObject so, string propertyName, List<string> missing)
    {
        var prop = so.FindProperty(propertyName);
        if (prop == null)
        {
            missing.Add(propertyName + " (property not found)");
            return;
        }

        if (prop.objectReferenceValue == null)
            missing.Add(propertyName);
    }

    private static void SetRef(SerializedObject so, string propertyName, UnityEngine.Object value)
    {
        var prop = so.FindProperty(propertyName);
        if (prop != null)
            prop.objectReferenceValue = value;
    }

    private static Transform FindByName(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrWhiteSpace(targetName))
            return null;

        if (NamesMatch(root.name, targetName))
            return root;

        for (var i = 0; i < root.childCount; i++)
        {
            var child = root.GetChild(i);
            var found = FindByName(child, targetName);
            if (found != null)
                return found;
        }

        return null;
    }

    private static T FindComponentByName<T>(Transform root, string objectName) where T : Component
    {
        var tr = FindByName(root, objectName);
        if (tr == null)
            return null;
        return tr.GetComponent<T>();
    }

    private static bool NamesMatch(string current, string expected)
    {
        if (string.IsNullOrWhiteSpace(current) || string.IsNullOrWhiteSpace(expected))
            return false;

        return string.Equals(current.Trim(), expected.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static Transform EnsureOverlayChild(Transform parent, string childName, Color color)
    {
        var existing = FindDirectChildByName(parent, childName);
        if (existing != null)
            return existing;

        var go = new GameObject(childName, typeof(RectTransform), typeof(Image));
        var tr = go.transform;
        tr.SetParent(parent, false);
        StretchToParent(tr as RectTransform);

        var image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;

        return tr;
    }

    private static Image EnsureFrameChild(Transform parent, string childName, Color color)
    {
        var existing = FindDirectChildByName(parent, childName)?.GetComponent<Image>();
        if (existing != null)
            return existing;

        var go = new GameObject(childName, typeof(RectTransform), typeof(Image));
        var tr = go.transform;
        tr.SetParent(parent, false);
        StretchToParent(tr as RectTransform);

        var image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        image.enabled = false;
        return image;
    }

    private static void StretchToParent(RectTransform rectTransform)
    {
        if (rectTransform == null)
            return;

        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        rectTransform.localScale = Vector3.one;
    }

    private static Transform EnsureChild(Transform parent, string childName)
    {
        var existing = FindDirectChildByName(parent, childName);
        if (existing != null)
            return existing;

        var go = new GameObject(childName, typeof(RectTransform));
        var tr = go.transform;
        tr.SetParent(parent, false);
        return tr;
    }

    private static Transform EnsureSimpleButton(Transform parent, string buttonName, string textName, string text)
    {
        var buttonRoot = EnsureChild(parent, buttonName);
        if (buttonRoot.GetComponent<Image>() == null)
            buttonRoot.gameObject.AddComponent<Image>();
        if (buttonRoot.GetComponent<Button>() == null)
            buttonRoot.gameObject.AddComponent<Button>();

        var textRoot = EnsureTextObject(buttonRoot, textName, text);
        textRoot.SetParent(buttonRoot, false);
        return buttonRoot;
    }

    private static void EnsureTabButton(Transform parent, string buttonName, string textName, string mentionName)
    {
        var label = textName.Replace("TabText", string.Empty);
        var buttonRoot = EnsureSimpleButton(parent, buttonName, textName, label);
        var textRoot = EnsureTextObject(buttonRoot, textName, textName.Replace("TabText", string.Empty));
        textRoot.SetParent(buttonRoot, false);
        EnsureBadge(buttonRoot, mentionName);
    }

    private static Transform EnsureImageObject(Transform parent, string objectName)
    {
        var tr = EnsureChild(parent, objectName);
        if (tr.GetComponent<Image>() == null)
            tr.gameObject.AddComponent<Image>();
        return tr;
    }

    private static Transform EnsureTextObject(Transform parent, string objectName, string defaultText)
    {
        var tr = EnsureChild(parent, objectName);
        var text = tr.GetComponent<TextMeshProUGUI>();
        if (text == null)
            text = tr.gameObject.AddComponent<TextMeshProUGUI>();
        if (string.IsNullOrWhiteSpace(text.text))
            text.text = defaultText;
        return tr;
    }

    private static Transform EnsureBadge(Transform parent, string objectName)
    {
        var tr = EnsureChild(parent, objectName);
        var image = tr.GetComponent<Image>();
        if (image == null)
            image = tr.gameObject.AddComponent<Image>();
        image.color = new Color(1f, 0.1f, 0.1f, 1f);
        return tr;
    }

    private static Transform FindDirectChildByName(Transform parent, string childName)
    {
        if (parent == null || string.IsNullOrWhiteSpace(childName))
            return null;

        for (var i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            if (NamesMatch(child.name, childName))
                return child;
        }

        return null;
    }

    private static Image ResolveTopTabBackground(Transform root, string buttonName)
    {
        var buttonRoot = FindByName(root, buttonName);
        if (buttonRoot == null)
            return null;

        var button = buttonRoot.GetComponent<Button>();
        var targetImage = button != null ? button.targetGraphic as Image : null;
        if (targetImage != null && targetImage.sprite != null)
            return targetImage;

        Image candidate = null;
        var images = buttonRoot.GetComponentsInChildren<Image>(true);
        for (var i = 0; i < images.Length; i++)
        {
            var image = images[i];
            if (image == null || image == targetImage)
                continue;
            if (image.sprite == null)
                continue;

            var imageName = image.gameObject.name;
            if (imageName.IndexOf("mention", StringComparison.OrdinalIgnoreCase) >= 0 ||
                imageName.IndexOf("badge", StringComparison.OrdinalIgnoreCase) >= 0 ||
                imageName.IndexOf("text", StringComparison.OrdinalIgnoreCase) >= 0 ||
                imageName.IndexOf("label", StringComparison.OrdinalIgnoreCase) >= 0 ||
                imageName.IndexOf("icon", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                continue;
            }

            if (imageName.IndexOf("bg", StringComparison.OrdinalIgnoreCase) >= 0 ||
                imageName.IndexOf("background", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return image;
            }

            if (candidate == null)
                candidate = image;
        }

        return candidate ?? targetImage;
    }

    private static Image ResolveTopTabSelectedFrame(Transform root, string buttonName)
    {
        var buttonRoot = FindByName(root, buttonName);
        if (buttonRoot == null)
            return null;

        var images = buttonRoot.GetComponentsInChildren<Image>(true);
        for (var i = 0; i < images.Length; i++)
        {
            var image = images[i];
            if (image == null)
                continue;

            var imageName = image.gameObject.name;
            if (imageName.IndexOf("selectedframe", StringComparison.OrdinalIgnoreCase) >= 0 ||
                imageName.IndexOf("activeframe", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return image;
            }
        }

        return null;
    }
}
#endif
