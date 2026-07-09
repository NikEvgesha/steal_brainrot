using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class BlockyUITheme
{
    public enum Tone
    {
        Green,
        Blue,
        Red,
        Orange,
        Purple,
        Brown,
        Dark,
        Disabled
    }

    private const float DefaultPanelPixelsPerUnit = 2.2f;
    private const string StudPanelResourcePath = "BlockyUI/BlockyStudPanel";
    private const string PlainPanelResourcePath = "BlockyUI/BlockyPlainPanel";
    private static readonly Dictionary<string, Sprite> SpriteCache = new();
    private static readonly HashSet<string> StyledWindows = new(StringComparer.Ordinal);

    public static readonly Color GreenHeader = new Color(0.18f, 0.82f, 0.08f, 1f);
    public static readonly Color BlueHeader = new Color(0.05f, 0.45f, 0.92f, 1f);
    public static readonly Color RedHeader = new Color(0.90f, 0.05f, 0.03f, 1f);
    public static readonly Color PurpleHeader = new Color(0.52f, 0.28f, 0.92f, 1f);
    public static readonly Color OrangeHeader = new Color(1f, 0.53f, 0.06f, 1f);
    public static readonly Color BrownBody = new Color(0.35f, 0.18f, 0.07f, 0.98f);
    public static readonly Color DarkBrownPanel = new Color(0.14f, 0.07f, 0.03f, 0.90f);
    public static readonly Color CurrencyPanel = new Color(0.08f, 0.075f, 0.065f, 0.86f);
    public static readonly Color YellowAccent = new Color(1f, 0.86f, 0.02f, 1f);
    public static readonly Color BlackStroke = new Color(0.015f, 0.012f, 0.01f, 1f);

    public static void StyleWindow(GameObject root, Tone headerTone)
    {
        if (root == null)
            return;

        string key = root.GetInstanceID().ToString();
        bool firstPass = StyledWindows.Add(key);

        if (firstPass)
            EnsureDimOverlay(root);

        StyleLargeImages(root, headerTone);
        StyleAllButtons(root);
        StyleAllText(root);
        EnsureWindowAnimator(root);
    }

    public static void StyleInventoryWindow(GameObject root, Item selectedTab)
    {
        if (root == null)
            return;

        EnsureDimOverlay(root);

        Sprite textureSprite = ResolvePrimaryTextureSprite(root.transform);

        var rootImage = root.GetComponent<Image>();
        if (rootImage != null)
        {
            ApplyTiledTexture(rootImage, BrownBody, textureSprite, true);
            EnsureOutline(root, new Vector2(4f, -4f), BlackStroke);
            EnsureShadow(root, new Vector2(0f, -4f), new Color(0f, 0f, 0f, 0.42f));
        }

        EnsureInventoryHeader(root, textureSprite);
        StyleInventoryText(root);
        StyleInventoryTab(root.transform.Find("Tabs/BrainrotsTabButton")?.GetComponent<Button>(), selectedTab == Item.Brainrot, textureSprite);
        StyleInventoryTab(root.transform.Find("Tabs/EggsTabButton")?.GetComponent<Button>(), selectedTab == Item.Egg, textureSprite);
        StyleInventoryTab(root.transform.Find("Tabs/FoodTabButton")?.GetComponent<Button>(), selectedTab == Item.Food, textureSprite);

        var itemsPanel = root.transform.Find("ItemsPanel");
        if (itemsPanel != null)
        {
            var image = itemsPanel.GetComponent<Image>();
            if (image != null)
            {
                ApplyTiledTexture(image, new Color(0.29f, 0.16f, 0.08f, 0.96f), textureSprite, true);
                EnsureOutline(itemsPanel.gameObject, new Vector2(3f, -3f), BlackStroke);
            }
        }

        var closeButton = root.transform.Find("Exit")?.GetComponent<Button>();
        if (closeButton != null)
        {
            var image = closeButton.targetGraphic as Image;
            if (image == null)
                image = closeButton.GetComponent<Image>();

            if (image != null)
            {
                ApplyTiledTexture(image, RedHeader, textureSprite, true);
                closeButton.targetGraphic = image;
                EnsureOutline(closeButton.gameObject, new Vector2(3f, -3f), BlackStroke);
                EnsureShadow(closeButton.gameObject, new Vector2(0f, -3f), new Color(0f, 0f, 0f, 0.35f));
            }

            EnsureInventoryCloseGlyph(closeButton);
            ApplyInventoryButtonColors(closeButton);
        }

        EnsureWindowAnimator(root);
    }

    public static void StyleShopWindow(GameObject root)
    {
        if (root == null)
            return;

        EnsureDimOverlay(root);

        Sprite textureSprite = ResolvePrimaryTextureSprite(root.transform);
        GameObject panel = root.transform.Find("Panel")?.gameObject ?? root;

        var panelImage = panel.GetComponent<Image>();
        if (panelImage != null)
        {
            ApplyTiledTexture(panelImage, BrownBody, textureSprite, true);
            EnsureOutline(panel, new Vector2(4f, -4f), BlackStroke);
            EnsureShadow(panel, new Vector2(0f, -4f), new Color(0f, 0f, 0f, 0.42f));
        }

        EnsureShopHeader(panel, textureSprite);
        StyleShopScrollView(panel.transform, textureSprite);
        StyleShopText(panel);

        var closeButton = FindWindowCloseButton(panel.transform);
        if (closeButton != null)
        {
            var image = closeButton.targetGraphic as Image;
            if (image == null)
                image = closeButton.GetComponent<Image>();
            if (image == null)
                image = closeButton.GetComponentInChildren<Image>(true);
            if (image == null)
                image = closeButton.gameObject.AddComponent<Image>();

            ApplyTiledTexture(image, RedHeader, textureSprite, true);
            closeButton.targetGraphic = image;
            EnsureOutline(image.gameObject, new Vector2(3f, -3f), BlackStroke);
            EnsureShadow(image.gameObject, new Vector2(0f, -3f), new Color(0f, 0f, 0f, 0.35f));
            EnsureInventoryCloseGlyph(closeButton);
            ApplyInventoryButtonColors(closeButton);
        }

        EnsureWindowAnimator(root);
    }

    public static void StyleMenuButton(Button button, Color backgroundColor)
    {
        if (button == null)
            return;

        ApplyButton(button, backgroundColor);
        var layout = GetOrAdd<LayoutElement>(button.gameObject);
        layout.flexibleWidth = 0f;
        layout.flexibleHeight = 0f;
    }

    public static void StyleTopNavigationButton(Button button)
    {
        if (button == null)
            return;

        var image = button.targetGraphic as Image;
        if (image == null)
            image = button.GetComponent<Image>();

        if (image != null)
        {
            button.targetGraphic = image;
            image.raycastTarget = true;
            image.pixelsPerUnitMultiplier = 1f;
            EnsureOutline(image.gameObject, new Vector2(4f, -4f), BlackStroke);
            EnsureShadow(image.gameObject, new Vector2(0f, -4f), new Color(0f, 0f, 0f, 0.38f));
        }

        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.05f, 1.05f, 1.05f, 1f);
        colors.pressedColor = new Color(0.84f, 0.84f, 0.84f, 1f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.75f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        button.transition = Selectable.Transition.ColorTint;

        foreach (var text in button.GetComponentsInChildren<Text>(true))
            ApplyText(text, Color.white, 42);
        foreach (var text in button.GetComponentsInChildren<TMP_Text>(true))
            ApplyText(text, Color.white, 42);
    }

    public static void StyleCurrencyBadge(GameObject root, CurrencyType type)
    {
        if (root == null)
            return;

        var image = root.GetComponent<Image>();
        if (image != null)
        {
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1f;
            image.color = CurrencyPanel;
            EnsureOutline(root, new Vector2(3f, -3f), new Color(1f, 1f, 1f, 0.92f));
            EnsureShadow(root, new Vector2(0f, -4f), new Color(0f, 0f, 0f, 0.42f));
        }

        foreach (var text in root.GetComponentsInChildren<Text>(true))
        {
            if (!IsCurrencyAmountText(text.transform))
            {
                text.gameObject.SetActive(false);
                continue;
            }

            ApplyText(text, Color.white, 34);
        }

        foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
        {
            if (!IsCurrencyAmountText(text.transform))
            {
                text.gameObject.SetActive(false);
                continue;
            }

            ApplyText(text, Color.white, 34);
        }

        foreach (var icon in root.GetComponentsInChildren<Image>(true))
        {
            if (icon == null || icon.gameObject == root)
                continue;

            if (icon.gameObject.name.IndexOf("icon", StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            EnsureOutline(icon.gameObject, new Vector2(2f, -2f), BlackStroke);
            EnsureShadow(icon.gameObject, new Vector2(0f, -2f), new Color(0f, 0f, 0f, 0.35f));
        }
    }

    private static bool IsCurrencyAmountText(Transform transform)
    {
        while (transform != null)
        {
            if (transform.name.IndexOf("amount", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            transform = transform.parent;
        }

        return false;
    }

    public static void StyleQuickSlot(GameObject root, Image activeFrame = null)
    {
        if (root == null)
            return;

        var image = root.GetComponent<Image>();
        if (image != null)
        {
            ApplyPanel(image, new Color(0.07f, 0.06f, 0.05f, 0.88f), false);
            EnsureOutline(root, new Vector2(3f, -3f), BlackStroke);
        }

        if (activeFrame != null)
        {
            activeFrame.sprite = GetPanelSprite(false);
            activeFrame.type = Image.Type.Sliced;
            activeFrame.color = YellowAccent;
            activeFrame.raycastTarget = false;
        }

        foreach (var text in root.GetComponentsInChildren<Text>(true))
            ApplyText(text, Color.white, 22);
    }

    public static void StyleCard(GameObject root, RareType rareType, bool selected = false)
    {
        if (root == null)
            return;

        var background = ResolveCardBackground(root);
        if (background != null)
        {
            ApplyPanel(background, GetRareColor(rareType), true);
            EnsureOutline(background.gameObject, selected ? new Vector2(4f, -4f) : new Vector2(3f, -3f), selected ? YellowAccent : BlackStroke);
        }

        var button = root.GetComponent<Button>() ?? root.GetComponentInChildren<Button>(true);
        if (button != null)
            ApplyButton(button, GetRareColor(rareType));

        foreach (var text in root.GetComponentsInChildren<Text>(true))
            ApplyText(text, Color.white, 18);
        foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            ApplyText(text, Color.white, 18);
    }

    public static void StyleShopProductCard(GameObject root)
    {
        if (root == null)
            return;

        Sprite textureSprite = ResolvePrimaryTextureSprite(root.transform.root);
        var background = root.GetComponent<Image>();
        if (background != null)
        {
            ApplyTiledTexture(background, new Color(0.24f, 0.13f, 0.06f, 0.96f), textureSprite, true);
            EnsureOutline(root, new Vector2(3f, -3f), BlackStroke);
            EnsureShadow(root, new Vector2(0f, -3f), new Color(0f, 0f, 0f, 0.34f));
        }

        var buyButton = root.transform.Find("BuyButton")?.GetComponent<Button>();
        if (buyButton != null)
        {
            var image = buyButton.targetGraphic as Image;
            if (image == null)
                image = buyButton.GetComponent<Image>();
            if (image == null)
                image = buyButton.gameObject.AddComponent<Image>();

            ApplyTiledTexture(image, GreenHeader, textureSprite, true);
            buyButton.targetGraphic = image;
            EnsureOutline(image.gameObject, new Vector2(3f, -3f), BlackStroke);
            EnsureShadow(image.gameObject, new Vector2(0f, -3f), new Color(0f, 0f, 0f, 0.35f));
            ApplyInventoryButtonColors(buyButton);
        }

        foreach (var text in root.GetComponentsInChildren<Text>(true))
            ApplyText(text, Color.white, 22);
        foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            ApplyText(text, Color.white, 22);
    }

    public static void StyleFoodSlot(GameObject root, bool available)
    {
        if (root == null)
            return;

        var image = root.GetComponent<Image>();
        if (image != null)
            ApplyPanel(image, available ? DarkBrownPanel : new Color(0.18f, 0.18f, 0.18f, 0.86f), true);

        foreach (var button in root.GetComponentsInChildren<Button>(true))
            ApplyButton(button, button.interactable && available ? GreenHeader : new Color(0.42f, 0.42f, 0.42f, 1f));

        foreach (var text in root.GetComponentsInChildren<Text>(true))
            ApplyText(text, Color.white, 20);
    }

    public static void ApplyButton(Button button)
    {
        if (button == null)
            return;

        ApplyButton(button, ResolveButtonColor(button));
    }

    public static void ApplyButton(Button button, Color color)
    {
        if (button == null)
            return;

        var image = button.targetGraphic as Image;
        if (image == null)
            image = button.GetComponent<Image>();
        if (image == null)
            image = button.gameObject.AddComponent<Image>();

        if (IsInvisibleBackdropButton(button))
        {
            if (image != null)
            {
                image.sprite = null;
                image.type = Image.Type.Simple;
                image.color = new Color(0f, 0f, 0f, 0f);
                image.raycastTarget = true;
                button.targetGraphic = image;
            }

            var transparent = button.colors;
            transparent.normalColor = Color.clear;
            transparent.highlightedColor = Color.clear;
            transparent.pressedColor = Color.clear;
            transparent.selectedColor = Color.clear;
            transparent.disabledColor = Color.clear;
            transparent.colorMultiplier = 1f;
            transparent.fadeDuration = 0f;
            button.colors = transparent;
            button.transition = Selectable.Transition.ColorTint;
            return;
        }

        if (image != null)
        {
            ApplyPanel(image, color, true);
            button.targetGraphic = image;
            EnsureOutline(image.gameObject, new Vector2(3f, -3f), BlackStroke);
            EnsureShadow(image.gameObject, new Vector2(0f, -3f), new Color(0f, 0f, 0f, 0.35f));
        }

        if (IsCloseButton(button))
            StyleCloseButtonGlyph(button, image);

        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
        colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(0.48f, 0.48f, 0.48f, 0.78f);
        colors.fadeDuration = 0.08f;
        colors.colorMultiplier = 1f;
        button.colors = colors;
        button.transition = Selectable.Transition.ColorTint;

        if (button.GetComponent<BlockyUIButtonFeedback>() == null)
            button.gameObject.AddComponent<BlockyUIButtonFeedback>();

        foreach (var text in button.GetComponentsInChildren<Text>(true))
            ApplyText(text, Color.white, 24);
        foreach (var text in button.GetComponentsInChildren<TMP_Text>(true))
            ApplyText(text, Color.white, 24);
    }

    public static void ApplyPanel(Image image, Color color, bool studs)
    {
        if (image == null)
            return;

        image.sprite = studs ? GetStudPanelSprite() : GetPanelSprite(false);
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = DefaultPanelPixelsPerUnit;
        image.color = color;
    }

    public static void ApplyText(Text text, Color fallbackColor, int minimumSize)
    {
        if (text == null)
            return;

        text.fontStyle = FontStyle.Bold;
        if (ShouldReplaceTextColor(text.color))
            text.color = fallbackColor;
        text.supportRichText = true;
        text.alignByGeometry = true;
        if (text.fontSize < minimumSize)
            text.fontSize = minimumSize;

        EnsureOutline(text.gameObject, new Vector2(2f, -2f), BlackStroke);
        EnsureShadow(text.gameObject, new Vector2(0f, -2f), new Color(0f, 0f, 0f, 0.35f));
    }

    public static void ApplyText(TMP_Text text, Color fallbackColor, int minimumSize)
    {
        if (text == null)
            return;

        text.fontStyle |= FontStyles.Bold;
        if (ShouldReplaceTextColor(text.color))
            text.color = fallbackColor;
        text.outlineColor = BlackStroke;
        text.outlineWidth = Mathf.Max(text.outlineWidth, 0.16f);
        text.textWrappingMode = TextWrappingModes.Normal;
        if (text.fontSize < minimumSize)
            text.fontSize = minimumSize;
    }

    public static Color GetRareColor(RareType rareType)
    {
        switch (rareType)
        {
            case RareType.Uncommon:
                return new Color(0.20f, 0.78f, 0.10f, 0.96f);
            case RareType.Rare:
                return new Color(0.05f, 0.48f, 0.95f, 0.96f);
            case RareType.Epic:
                return new Color(0.55f, 0.16f, 0.90f, 0.96f);
            case RareType.Legendary:
                return new Color(0.97f, 0.66f, 0.03f, 0.96f);
            case RareType.Mythic:
                return new Color(0.90f, 0.07f, 0.05f, 0.96f);
            default:
                return new Color(0.36f, 0.70f, 0.24f, 0.96f);
        }
    }

    private static void StyleInventoryTab(Button button, bool selected, Sprite textureSprite)
    {
        if (button == null)
            return;

        var image = button.targetGraphic as Image;
        if (image == null)
            image = button.GetComponent<Image>();
        if (image == null)
            image = button.gameObject.AddComponent<Image>();

        Color tabColor = selected ? GreenHeader : new Color(0.32f, 0.18f, 0.09f, 1f);
        ApplyTiledTexture(image, tabColor, textureSprite, true);
        button.targetGraphic = image;
        EnsureOutline(button.gameObject, selected ? new Vector2(4f, -4f) : new Vector2(3f, -3f), BlackStroke);
        EnsureShadow(button.gameObject, new Vector2(0f, -3f), new Color(0f, 0f, 0f, 0.35f));
        ApplyInventoryButtonColors(button);

        foreach (var text in button.GetComponentsInChildren<Text>(true))
            ApplyText(text, Color.white, selected ? 30 : 26);
        foreach (var text in button.GetComponentsInChildren<TMP_Text>(true))
            ApplyText(text, Color.white, selected ? 30 : 26);
    }

    private static void StyleInventoryText(GameObject root)
    {
        var title = root.transform.Find("Title");

        foreach (var text in root.GetComponentsInChildren<Text>(true))
        {
            if (text.GetComponentInParent<InventorySlot>(true) != null)
                continue;

            int minimumSize = title != null && text.transform == title ? 42 : 20;
            ApplyText(text, Color.white, minimumSize);
        }

        foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.GetComponentInParent<InventorySlot>(true) != null)
                continue;

            int minimumSize = title != null && text.transform == title ? 42 : 20;
            ApplyText(text, Color.white, minimumSize);
        }
    }

    private static void EnsureInventoryHeader(GameObject root, Sprite textureSprite)
    {
        var rootRect = root.transform as RectTransform;
        if (rootRect == null)
            return;

        var header = root.transform.Find("InventoryHeader");
        GameObject headerObject;
        if (header == null)
        {
            headerObject = new GameObject("InventoryHeader", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            headerObject.transform.SetParent(root.transform, false);
        }
        else
        {
            headerObject = header.gameObject;
        }

        headerObject.transform.SetSiblingIndex(0);
        var rect = headerObject.transform as RectTransform;
        rect.anchorMin = new Vector2(0f, 0.77f);
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);

        var image = GetOrAdd<Image>(headerObject);
        ApplyTiledTexture(image, GreenHeader, textureSprite, false);
        EnsureOutline(headerObject, new Vector2(2f, -2f), BlackStroke);
    }

    private static void EnsureShopHeader(GameObject panel, Sprite textureSprite)
    {
        var panelRect = panel.transform as RectTransform;
        if (panelRect == null)
            return;

        var header = panel.transform.Find("ShopHeader");
        GameObject headerObject;
        if (header == null)
        {
            headerObject = new GameObject("ShopHeader", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            headerObject.transform.SetParent(panel.transform, false);
        }
        else
        {
            headerObject = header.gameObject;
        }

        headerObject.transform.SetSiblingIndex(0);
        var rect = headerObject.transform as RectTransform;
        rect.anchorMin = new Vector2(0f, 0.88f);
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);

        var image = GetOrAdd<Image>(headerObject);
        ApplyTiledTexture(image, OrangeHeader, textureSprite, false);
        EnsureOutline(headerObject, new Vector2(2f, -2f), BlackStroke);
    }

    private static void StyleShopScrollView(Transform panel, Sprite textureSprite)
    {
        var scroll = panel.Find("Scroll View");
        if (scroll == null)
            return;

        var image = scroll.GetComponent<Image>();
        if (image == null)
            return;

        ApplyTiledTexture(image, new Color(0.18f, 0.09f, 0.04f, 0.72f), textureSprite, true);
        EnsureOutline(scroll.gameObject, new Vector2(3f, -3f), BlackStroke);
    }

    private static void StyleShopText(GameObject panel)
    {
        foreach (var text in panel.GetComponentsInChildren<Text>(true))
        {
            int minimumSize = text.transform.parent == panel.transform ? 54 : 20;
            ApplyText(text, Color.white, minimumSize);
        }

        foreach (var text in panel.GetComponentsInChildren<TMP_Text>(true))
        {
            int minimumSize = text.transform.parent == panel.transform ? 54 : 20;
            ApplyText(text, Color.white, minimumSize);
        }
    }

    private static Button FindWindowCloseButton(Transform root)
    {
        if (root == null)
            return null;

        Button fallback = null;
        var buttons = root.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            var button = buttons[i];
            if (button == null || IsInvisibleBackdropButton(button))
                continue;

            string name = button.gameObject.name.ToLowerInvariant();
            if (name.Contains("exit") || name.Contains("close"))
                return button;

            var rect = button.transform as RectTransform;
            if (rect != null &&
                rect.anchorMin.x >= 0.65f &&
                rect.anchorMin.y >= 0.65f &&
                rect.rect.width <= 180f &&
                rect.rect.height <= 180f)
            {
                if (button.transform.parent == root)
                    return button;

                fallback = button;
            }
        }

        return fallback;
    }

    private static void EnsureInventoryCloseGlyph(Button button)
    {
        if (button == null)
            return;

        var legacyText = button.GetComponentInChildren<Text>(true);
        var tmpText = button.GetComponentInChildren<TMP_Text>(true);

        if (legacyText == null && tmpText == null)
        {
            var textObject = new GameObject("X", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textObject.transform.SetParent(button.transform, false);
            var rect = textObject.transform as RectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            legacyText = textObject.GetComponent<Text>();
        }

        if (legacyText != null)
        {
            legacyText.text = "X";
            legacyText.font = legacyText.font != null ? legacyText.font : ResolveLegacyFont(button.transform);
            legacyText.fontStyle = FontStyle.Bold;
            legacyText.alignment = TextAnchor.MiddleCenter;
            legacyText.color = BlackStroke;
            legacyText.raycastTarget = false;
            legacyText.resizeTextForBestFit = true;
            legacyText.resizeTextMinSize = 24;
            legacyText.resizeTextMaxSize = 56;
        }

        if (tmpText != null)
        {
            tmpText.text = "X";
            tmpText.fontStyle |= FontStyles.Bold;
            tmpText.color = BlackStroke;
            tmpText.alignment = TextAlignmentOptions.Center;
            tmpText.raycastTarget = false;
            tmpText.fontSize = Mathf.Max(tmpText.fontSize, 42f);
            tmpText.outlineWidth = 0f;
        }
    }

    private static Font ResolveLegacyFont(Transform context)
    {
        if (context != null && context.root != null)
        {
            foreach (var text in context.root.GetComponentsInChildren<Text>(true))
            {
                if (text != null && text.font != null)
                    return text.font;
            }
        }

        return Resources.GetBuiltinResource<Font>("Arial.ttf");
    }

    private static void ApplyInventoryButtonColors(Button button)
    {
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
        colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(0.48f, 0.48f, 0.48f, 0.78f);
        colors.fadeDuration = 0.08f;
        colors.colorMultiplier = 1f;
        button.colors = colors;
        button.transition = Selectable.Transition.ColorTint;

        if (button.GetComponent<BlockyUIButtonFeedback>() == null)
            button.gameObject.AddComponent<BlockyUIButtonFeedback>();
    }

    private static void ApplyTiledTexture(Image image, Color color, Sprite textureSprite, bool raycastTarget)
    {
        if (image == null)
            return;

        if (textureSprite != null && !IsPrimaryTextureSprite(image.sprite))
            image.sprite = textureSprite;

        image.color = color;
        image.raycastTarget = raycastTarget;
        image.type = image.sprite != null ? Image.Type.Tiled : Image.Type.Simple;
        image.pixelsPerUnitMultiplier = 1f;
    }

    private static Sprite ResolvePrimaryTextureSprite(Transform root)
    {
        if (root == null)
            return null;

        foreach (var image in root.GetComponentsInChildren<Image>(true))
        {
            if (image != null && IsPrimaryTextureSprite(image.sprite))
                return image.sprite;
        }

        return null;
    }

    private static bool IsPrimaryTextureSprite(Sprite sprite)
    {
        if (sprite == null)
            return false;

        if (string.Equals(sprite.name, "texture", StringComparison.OrdinalIgnoreCase))
            return true;

        return sprite.texture != null &&
               string.Equals(sprite.texture.name, "texture", StringComparison.OrdinalIgnoreCase);
    }

    private static void StyleLargeImages(GameObject root, Tone headerTone)
    {
        var images = root.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            var image = images[i];
            if (image == null || ShouldSkipImage(image))
                continue;

            string lower = image.gameObject.name.ToLowerInvariant();
            if (IsHeaderName(lower))
            {
                ApplyPanel(image, GetToneColor(headerTone), true);
                EnsureOutline(image.gameObject, new Vector2(3f, -3f), BlackStroke);
            }
            else if (IsContentName(lower))
            {
                ApplyPanel(image, DarkBrownPanel, true);
                EnsureOutline(image.gameObject, new Vector2(2f, -2f), BlackStroke);
            }
            else if (IsPanelName(lower))
            {
                ApplyPanel(image, BrownBody, true);
                EnsureOutline(image.gameObject, new Vector2(3f, -3f), BlackStroke);
            }
        }
    }

    private static void StyleAllButtons(GameObject root)
    {
        foreach (var button in root.GetComponentsInChildren<Button>(true))
            ApplyButton(button);
    }

    private static void StyleAllText(GameObject root)
    {
        foreach (var text in root.GetComponentsInChildren<Text>(true))
            ApplyText(text, Color.white, 18);
        foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            ApplyText(text, Color.white, 18);
    }

    private static void EnsureDimOverlay(GameObject root)
    {
        var parent = root.transform.parent as RectTransform;
        var rootRect = root.transform as RectTransform;
        if (parent == null || rootRect == null)
            return;

        if (parent.GetComponent<LayoutGroup>() != null)
            return;

        string overlayName = root.name + "_BlockyDimOverlay";
        if (root.transform.parent.Find(overlayName) != null)
            return;

        var overlayObject = new GameObject("BlockyDimOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        overlayObject.name = overlayName;
        overlayObject.transform.SetParent(parent, false);
        overlayObject.transform.SetSiblingIndex(Mathf.Max(0, root.transform.GetSiblingIndex()));

        var rect = overlayObject.transform as RectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var image = overlayObject.GetComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0.28f);
        image.raycastTarget = false;

        var follower = overlayObject.AddComponent<BlockyDimOverlayFollower>();
        follower.Init(root);
    }

    private static void EnsureWindowAnimator(GameObject root)
    {
        if (root == null || root.GetComponent<BlockyUIButtonFeedback>() != null)
            return;
    }

    private static Image ResolveCardBackground(GameObject root)
    {
        var direct = root.GetComponent<Image>();
        if (direct != null)
            return direct;

        var images = root.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            var image = images[i];
            if (image == null)
                continue;

            string lower = image.gameObject.name.ToLowerInvariant();
            if (IsPanelName(lower) || lower.Contains("background") || lower.Contains("bg"))
                return image;
        }

        return images.Length > 0 ? images[0] : null;
    }

    private static Color ResolveButtonColor(Button button)
    {
        if (button == null || !button.interactable)
            return new Color(0.42f, 0.42f, 0.42f, 1f);

        string text = GetButtonText(button).ToLowerInvariant();
        string name = button.gameObject.name.ToLowerInvariant() + " " + text;

        string trimmedText = text.Trim();
        if (IsCloseButton(button) || name.Contains("cancel") || name.Contains("отмена"))
            return RedHeader;

        if (name.Contains("refresh") || name.Contains("resupply") || name.Contains("обнов"))
            return OrangeHeader;

        if (name.Contains("buy") || name.Contains("sell") || name.Contains("claim") || name.Contains("ok") ||
            name.Contains("куп") || name.Contains("прод") || name.Contains("получ") || name.Contains("подтверд"))
            return GreenHeader;

        if (name.Contains("tab") || name.Contains("page") || name.Contains("egg") || name.Contains("pet") ||
            name.Contains("brainrot") || name.Contains("яй") || name.Contains("питом") || name.Contains("брейн"))
            return BlueHeader;

        return BrownBody;
    }

    private static bool IsInvisibleBackdropButton(Button button)
    {
        if (button == null)
            return false;

        string name = button.gameObject.name.ToLowerInvariant();
        if (!(name.Contains("closearea") || name.Contains("close_area") || name.Contains("backdrop") || name.Contains("clickcatcher")))
            return false;

        return string.IsNullOrWhiteSpace(GetButtonText(button));
    }

    private static bool IsCloseButton(Button button)
    {
        if (button == null)
            return false;

        string text = GetButtonText(button).Trim().ToLowerInvariant();
        string name = button.gameObject.name.ToLowerInvariant();
        if (name.Contains("close") || name.Contains("закры") || text == "x" || text == "×")
            return true;

        if (!string.IsNullOrWhiteSpace(text))
            return false;

        var rect = button.transform as RectTransform;
        if (rect == null)
            return false;

        return name.Contains("button") &&
               rect.anchorMin.x >= 0.65f &&
               rect.anchorMin.y >= 0.65f &&
               rect.rect.width <= 160f &&
               rect.rect.height <= 160f;
    }

    private static void StyleCloseButtonGlyph(Button button, Image background)
    {
        var images = button.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            var image = images[i];
            if (image == null || image == background)
                continue;

            image.color = BlackStroke;
            image.raycastTarget = false;
        }

        foreach (var text in button.GetComponentsInChildren<Text>(true))
            text.color = BlackStroke;
        foreach (var text in button.GetComponentsInChildren<TMP_Text>(true))
            text.color = BlackStroke;
    }

    private static string GetButtonText(Button button)
    {
        var tmp = button.GetComponentInChildren<TMP_Text>(true);
        if (tmp != null)
            return tmp.text ?? string.Empty;

        var legacy = button.GetComponentInChildren<Text>(true);
        return legacy != null ? legacy.text ?? string.Empty : string.Empty;
    }

    private static bool ShouldSkipImage(Image image)
    {
        if (image.type == Image.Type.Filled)
            return true;

        var button = image.GetComponentInParent<Button>(true);
        if (button != null && button.targetGraphic != image)
            return true;

        string lower = image.gameObject.name.ToLowerInvariant();
        if (lower.Contains("icon") || lower.Contains("sprite") || lower.Contains("preview") ||
            lower.Contains("coin") || lower.Contains("gem") || lower.Contains("currency") ||
            lower.Contains("reward") || lower.Contains("pet") || lower.Contains("animal") ||
            lower.Contains("egg") || lower.Contains("food") || lower.Contains("mention") ||
            lower.Contains("badge") || lower.Contains("check") || lower.Contains("lock") ||
            lower.Contains("fill") || lower.Contains("handle") || lower.Contains("mask"))
            return button == null;

        if (button != null)
            return false;

        if (IsPanelName(lower) || IsHeaderName(lower) || IsContentName(lower))
            return false;

        var rect = image.transform as RectTransform;
        if (rect != null && rect.sizeDelta.x > 0f && rect.sizeDelta.y > 0f)
            return rect.sizeDelta.x < 48f || rect.sizeDelta.y < 28f;

        return false;
    }

    private static bool IsHeaderName(string lower)
    {
        return lower.Contains("header") || lower.Contains("titlebar") || lower.Contains("topbar") || lower.Contains("caption");
    }

    private static bool IsContentName(string lower)
    {
        return lower.Contains("content") || lower.Contains("viewport") || lower.Contains("items") || lower.Contains("grid");
    }

    private static bool IsPanelName(string lower)
    {
        return lower.Contains("panel") || lower.Contains("background") || lower == "bg" ||
               lower.Contains("window") || lower.Contains("body") || lower.Contains("container") ||
               lower.Contains("frame") || lower.Contains("root");
    }

    private static Color GetToneColor(Tone tone)
    {
        switch (tone)
        {
            case Tone.Blue:
                return BlueHeader;
            case Tone.Red:
                return RedHeader;
            case Tone.Orange:
                return OrangeHeader;
            case Tone.Purple:
                return PurpleHeader;
            case Tone.Brown:
                return BrownBody;
            case Tone.Dark:
                return DarkBrownPanel;
            case Tone.Disabled:
                return new Color(0.42f, 0.42f, 0.42f, 1f);
            default:
                return GreenHeader;
        }
    }

    private static bool ShouldReplaceTextColor(Color color)
    {
        if (color.a <= 0.01f)
            return false;

        float max = Mathf.Max(color.r, Mathf.Max(color.g, color.b));
        float min = Mathf.Min(color.r, Mathf.Min(color.g, color.b));
        return max < 0.78f || max - min < 0.16f;
    }

    private static void EnsureOutline(GameObject go, Vector2 distance, Color color)
    {
        if (go == null)
            return;

        var outline = GetOrAdd<Outline>(go);
        outline.effectColor = color;
        outline.effectDistance = distance;
        outline.useGraphicAlpha = true;
    }

    private static void EnsureShadow(GameObject go, Vector2 distance, Color color)
    {
        if (go == null)
            return;

        Shadow shadow = null;
        var shadows = go.GetComponents<Shadow>();
        for (int i = 0; i < shadows.Length; i++)
        {
            if (shadows[i] != null && !(shadows[i] is Outline))
            {
                shadow = shadows[i];
                break;
            }
        }

        if (shadow == null)
            shadow = go.AddComponent<Shadow>();
        shadow.effectColor = color;
        shadow.effectDistance = distance;
        shadow.useGraphicAlpha = true;
    }

    private static T GetOrAdd<T>(GameObject gameObject) where T : Component
    {
        if (!gameObject.TryGetComponent<T>(out var component))
            component = gameObject.AddComponent<T>();

        return component;
    }

    private static Sprite GetStudPanelSprite()
    {
        return GetPanelSprite(true);
    }

    private static Sprite GetPanelSprite(bool studs)
    {
        string key = studs ? "panel_studs" : "panel_plain";
        if (SpriteCache.TryGetValue(key, out var cached))
            return cached;

        cached = Resources.Load<Sprite>(studs ? StudPanelResourcePath : PlainPanelResourcePath);
        if (cached == null)
            cached = CreatePanelSprite(studs);

        SpriteCache[key] = cached;
        return cached;
    }

    private static Sprite CreatePanelSprite(bool studs)
    {
        const int size = 64;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Point;

        var clear = new Color(1f, 1f, 1f, 0f);
        var fill = Color.white;
        var shade = new Color(0.68f, 0.68f, 0.68f, 1f);
        var highlight = new Color(1.12f, 1.12f, 1.12f, 1f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool inside = x >= 3 && y >= 3 && x < size - 3 && y < size - 3;
                if (!inside)
                {
                    texture.SetPixel(x, y, clear);
                    continue;
                }

                bool border = x < 7 || y < 7 || x >= size - 7 || y >= size - 7;
                Color pixel = border ? shade : fill;

                if (studs && !border && IsStudPixel(x, y))
                    pixel = IsStudHighlight(x, y) ? highlight : new Color(0.82f, 0.82f, 0.82f, 1f);

                texture.SetPixel(x, y, pixel);
            }
        }

        texture.Apply();
        texture.hideFlags = HideFlags.DontSave;

        var sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 64f, 0, SpriteMeshType.FullRect, new Vector4(10f, 10f, 10f, 10f));
        sprite.hideFlags = HideFlags.DontSave;
        return sprite;
    }

    private static bool IsStudPixel(int x, int y)
    {
        int cellX = x % 16;
        int cellY = y % 16;
        return cellX >= 5 && cellX <= 10 && cellY >= 5 && cellY <= 10;
    }

    private static bool IsStudHighlight(int x, int y)
    {
        int cellX = x % 16;
        int cellY = y % 16;
        return cellX <= 7 || cellY <= 7;
    }
}
