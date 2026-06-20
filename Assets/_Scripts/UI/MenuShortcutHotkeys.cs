using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MenuShortcutHotkeys : MonoBehaviour
{
    [SerializeField] private Button inventoryButton;
    [SerializeField] private Button shopButton;
    [SerializeField] private Button albumButton;
    [SerializeField] private InventoryUI inventoryScreen;
    [SerializeField] private SpecialShop shopScreen;
    [SerializeField] private AlbumScreenController albumScreen;
    [SerializeField] private KeyCode inventoryKey = KeyCode.Tab;
    [SerializeField] private KeyCode shopKey = KeyCode.B;
    [SerializeField] private KeyCode albumKey = KeyCode.None;
    [SerializeField] private bool styleMenuButtons = true;
    [SerializeField] private Vector2 menuButtonSize = new Vector2(98f, 98f);
    [SerializeField] private float menuButtonSpacing = 10f;
    [SerializeField] private Vector2 menuContainerOffset = new Vector2(14f, -125f);
    [SerializeField] private float menuIconSize = 68f;
    [SerializeField] private Sprite inventoryIconSprite;
    [SerializeField] private Sprite shopIconSprite;
    [SerializeField] private Sprite albumIconSprite;

    private enum MenuIcon
    {
        Inventory,
        Shop,
        Album
    }

    private static readonly Dictionary<string, Sprite> SpriteCache = new();

    private void Start()
    {
        ResolveButtons();
        EnsureInventoryButton();
        EnsureShopButton();
        EnsureAlbumButton();

        if (styleMenuButtons)
            StyleMenuButtons();

        RegisterAlbumButton();
    }

    private void Update()
    {
        if (ShouldIgnoreInput())
            return;

        TryInvoke(inventoryKey, inventoryButton);
        TryInvoke(shopKey, shopButton);
        TryInvoke(albumKey, albumButton);
    }

    private void ResolveButtons()
    {
        if (inventoryButton == null)
        {
            var inventory = transform.Find("InventoryButton");
            if (inventory != null)
                inventoryButton = inventory.GetComponent<Button>();
        }

        if (shopButton == null)
        {
            var shop = transform.Find("ShopButton");
            if (shop != null)
                shopButton = shop.GetComponent<Button>();
        }

        if (albumButton == null)
        {
            var album = transform.Find("AlbumButton");
            if (album != null)
                albumButton = album.GetComponent<Button>();
        }
    }

    private void EnsureInventoryButton()
    {
        if (inventoryButton == null)
            inventoryButton = CreateMenuButton("InventoryButton");

        if (inventoryButton == null)
            return;

        if (inventoryButton.onClick.GetPersistentEventCount() > 0)
            return;

        inventoryButton.onClick.RemoveListener(ToggleInventory);
        inventoryButton.onClick.AddListener(ToggleInventory);
    }

    private void EnsureShopButton()
    {
        if (shopButton == null)
            shopButton = CreateMenuButton("ShopButton");

        if (shopButton == null)
            return;

        if (shopButton.onClick.GetPersistentEventCount() > 0)
            return;

        shopButton.onClick.RemoveListener(ToggleShop);
        shopButton.onClick.AddListener(ToggleShop);
    }

    private void EnsureAlbumButton()
    {
        if (albumButton == null)
            albumButton = CreateMenuButton("AlbumButton");

        if (albumButton == null)
            return;

        albumButton.onClick.RemoveListener(ToggleAlbum);
        albumButton.onClick.AddListener(ToggleAlbum);
    }

    private Button CreateMenuButton(string buttonName)
    {
        var buttonObject = new GameObject(buttonName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonObject.transform.SetParent(transform, false);

        var button = buttonObject.GetComponent<Button>();
        button.targetGraphic = buttonObject.GetComponent<Image>();

        return button;
    }

    private void ToggleInventory()
    {
        var inventory = ResolveInventoryScreen();
        if (inventory == null)
        {
            Debug.LogWarning("[MenuShortcutHotkeys] InventoryUI was not found.");
            return;
        }

        inventory._ToggleOpen();
    }

    private void ToggleShop()
    {
        var shop = ResolveShopScreen();
        if (shop == null)
        {
            Debug.LogWarning("[MenuShortcutHotkeys] SpecialShop was not found.");
            return;
        }

        shop.ToggleOpen();
    }

    private void ToggleAlbum()
    {
        var album = ResolveAlbumScreen();
        if (album == null)
        {
            Debug.LogWarning("[MenuShortcutHotkeys] AlbumScreenController was not found.");
            return;
        }

        album.Toggle();
    }

    private void RegisterAlbumButton()
    {
        if (albumButton == null)
            return;

        var album = ResolveAlbumScreen();
        if (album == null)
            return;

        var badge = EnsureMentionBadge(albumButton.transform);
        album.RegisterExternalOpenButton(albumButton, badge);
    }

    private InventoryUI ResolveInventoryScreen()
    {
        if (inventoryScreen != null)
            return inventoryScreen;

        var canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
            inventoryScreen = canvas.GetComponentInChildren<InventoryUI>(true);

        if (inventoryScreen == null)
            inventoryScreen = transform.root.GetComponentInChildren<InventoryUI>(true);

        if (inventoryScreen == null)
            inventoryScreen = FindFirstObjectByType<InventoryUI>(FindObjectsInactive.Include);

        return inventoryScreen;
    }

    private SpecialShop ResolveShopScreen()
    {
        if (shopScreen != null)
            return shopScreen;

        if (G.SpecialShop != null)
            shopScreen = G.SpecialShop;

        var canvas = GetComponentInParent<Canvas>();
        if (shopScreen == null && canvas != null)
            shopScreen = canvas.GetComponentInChildren<SpecialShop>(true);

        if (shopScreen == null)
            shopScreen = transform.root.GetComponentInChildren<SpecialShop>(true);

        if (shopScreen == null)
            shopScreen = FindFirstObjectByType<SpecialShop>(FindObjectsInactive.Include);

        return shopScreen;
    }

    private AlbumScreenController ResolveAlbumScreen()
    {
        if (albumScreen != null)
            return albumScreen;

        var canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
            albumScreen = canvas.GetComponentInChildren<AlbumScreenController>(true);

        if (albumScreen == null)
            albumScreen = transform.root.GetComponentInChildren<AlbumScreenController>(true);

        if (albumScreen == null)
            albumScreen = FindFirstObjectByType<AlbumScreenController>(FindObjectsInactive.Include);

        return albumScreen;
    }

    private void StyleMenuButtons()
    {
        var layout = GetComponent<VerticalLayoutGroup>();
        if (layout != null)
        {
            layout.spacing = menuButtonSpacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
        }

        var rect = transform as RectTransform;
        if (rect != null)
        {
            float height = menuButtonSize.y * 3f + menuButtonSpacing * 2f;
            rect.sizeDelta = new Vector2(menuButtonSize.x, height);
            rect.anchoredPosition = menuContainerOffset;
        }

        StyleButton(inventoryButton, MenuIcon.Inventory, new Color(0.18f, 0.67f, 0.74f, 1f));
        StyleButton(shopButton, MenuIcon.Shop, new Color(1f, 0.66f, 0.18f, 1f));
        StyleButton(albumButton, MenuIcon.Album, new Color(0.54f, 0.38f, 0.9f, 1f));
    }

    private void StyleButton(Button button, MenuIcon icon, Color backgroundColor)
    {
        if (button == null)
            return;

        var rect = button.transform as RectTransform;
        if (rect != null)
        {
            rect.sizeDelta = menuButtonSize;
            rect.localScale = Vector3.one;
        }

        var layoutElement = GetOrAdd<LayoutElement>(button.gameObject);
        layoutElement.minWidth = menuButtonSize.x;
        layoutElement.minHeight = menuButtonSize.y;
        layoutElement.preferredWidth = menuButtonSize.x;
        layoutElement.preferredHeight = menuButtonSize.y;
        layoutElement.flexibleWidth = 0f;
        layoutElement.flexibleHeight = 0f;

        var background = button.targetGraphic as Image;
        if (background == null)
            background = GetOrAdd<Image>(button.gameObject);

        background.sprite = GetButtonBackgroundSprite();
        background.type = Image.Type.Sliced;
        background.pixelsPerUnitMultiplier = 2f;
        background.color = backgroundColor;
        background.raycastTarget = true;
        button.targetGraphic = background;

        var shadow = GetOrAdd<Shadow>(button.gameObject);
        shadow.effectColor = new Color(0f, 0f, 0f, 0.35f);
        shadow.effectDistance = new Vector2(0f, -3f);

        var outline = GetOrAdd<Outline>(button.gameObject);
        outline.effectColor = new Color(1f, 1f, 1f, 0.32f);
        outline.effectDistance = new Vector2(2f, -2f);

        ConfigureButtonColors(button);
        HideTextChildren(button.transform);
        SetIcon(button.transform, icon, menuIconSize, GetConfiguredIcon(icon));
    }

    private static GameObject EnsureMentionBadge(Transform buttonTransform)
    {
        if (buttonTransform == null)
            return null;

        Transform badgeTransform = buttonTransform.Find("AlbumIconMention");
        if (badgeTransform == null)
        {
            var badgeObject = new GameObject("AlbumIconMention", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            badgeObject.transform.SetParent(buttonTransform, false);
            badgeTransform = badgeObject.transform;
        }

        var rect = badgeTransform as RectTransform;
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(-12f, -12f);
        rect.sizeDelta = new Vector2(24f, 24f);
        rect.SetAsLastSibling();

        var image = GetOrAdd<Image>(badgeTransform.gameObject);
        image.sprite = GetMentionBadgeSprite();
        image.type = Image.Type.Simple;
        image.color = new Color(1f, 0.08f, 0.08f, 1f);
        image.raycastTarget = false;

        var outline = GetOrAdd<Outline>(badgeTransform.gameObject);
        outline.effectColor = new Color(1f, 1f, 1f, 0.9f);
        outline.effectDistance = new Vector2(2f, -2f);

        badgeTransform.gameObject.SetActive(false);
        return badgeTransform.gameObject;
    }

    private Sprite GetConfiguredIcon(MenuIcon icon)
    {
        return icon switch
        {
            MenuIcon.Inventory => inventoryIconSprite,
            MenuIcon.Shop => shopIconSprite,
            MenuIcon.Album => albumIconSprite,
            _ => null
        };
    }

    private static void ConfigureButtonColors(Button button)
    {
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
        colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.55f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        button.transition = Selectable.Transition.ColorTint;
    }

    private static void HideTextChildren(Transform root)
    {
        foreach (var text in root.GetComponentsInChildren<Text>(true))
            text.gameObject.SetActive(false);

        foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            text.gameObject.SetActive(false);
    }

    private static void SetIcon(Transform buttonTransform, MenuIcon icon, float iconSize, Sprite configuredSprite)
    {
        Transform iconTransform = buttonTransform.Find("Icon");
        if (iconTransform == null)
        {
            var iconObject = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconObject.transform.SetParent(buttonTransform, false);
            iconTransform = iconObject.transform;
        }

        var iconImage = iconTransform.GetComponent<Image>();
        if (iconImage == null)
            iconImage = iconTransform.gameObject.AddComponent<Image>();

        var rect = iconTransform as RectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(iconSize, iconSize);
        rect.SetAsLastSibling();

        iconImage.sprite = configuredSprite != null ? configuredSprite : GetIconSprite(icon);
        iconImage.type = Image.Type.Simple;
        iconImage.preserveAspect = true;
        iconImage.color = Color.white;
        iconImage.raycastTarget = false;

        var shadow = GetOrAdd<Shadow>(iconImage.gameObject);
        shadow.effectColor = new Color(0f, 0f, 0f, 0.38f);
        shadow.effectDistance = new Vector2(0f, -3f);
    }

    private static T GetOrAdd<T>(GameObject gameObject) where T : Component
    {
        if (!gameObject.TryGetComponent<T>(out var component))
            component = gameObject.AddComponent<T>();

        return component;
    }

    private static Sprite GetButtonBackgroundSprite()
    {
        const string key = "button_bg";
        if (SpriteCache.TryGetValue(key, out var cached))
            return cached;

        var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Color clear = new Color(1f, 1f, 1f, 0f);
        Color fill = Color.white;
        Color border = new Color(1f, 1f, 1f, 0.92f);

        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
            {
                bool inside = IsInsideRoundedRect(x, y, 64, 64, 12);
                bool inner = IsInsideRoundedRect(x, y, 64, 64, 8) && x > 4 && y > 4 && x < 59 && y < 59;
                texture.SetPixel(x, y, inside ? (inner ? fill : border) : clear);
            }
        }

        texture.Apply();
        texture.hideFlags = HideFlags.DontSave;

        cached = Sprite.Create(texture, new Rect(0f, 0f, 64f, 64f), new Vector2(0.5f, 0.5f), 64f, 0, SpriteMeshType.FullRect, new Vector4(14f, 14f, 14f, 14f));
        cached.hideFlags = HideFlags.DontSave;
        SpriteCache[key] = cached;
        return cached;
    }

    private static Sprite GetMentionBadgeSprite()
    {
        const string key = "mention_badge";
        if (SpriteCache.TryGetValue(key, out var cached))
            return cached;

        var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Color clear = new Color(1f, 1f, 1f, 0f);
        Color fill = Color.white;
        var center = new Vector2(15.5f, 15.5f);
        const float radius = 14.5f;

        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                texture.SetPixel(x, y, distance <= radius ? fill : clear);
            }
        }

        texture.Apply();
        texture.hideFlags = HideFlags.DontSave;

        cached = Sprite.Create(texture, new Rect(0f, 0f, 32f, 32f), new Vector2(0.5f, 0.5f), 32f);
        cached.hideFlags = HideFlags.DontSave;
        SpriteCache[key] = cached;
        return cached;
    }

    private static Sprite GetIconSprite(MenuIcon icon)
    {
        string key = "icon_" + icon;
        if (SpriteCache.TryGetValue(key, out var cached))
            return cached;

        var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Point;

        Clear(texture);

        switch (icon)
        {
            case MenuIcon.Inventory:
                DrawInventoryIcon(texture);
                break;
            case MenuIcon.Shop:
                DrawShopIcon(texture);
                break;
            case MenuIcon.Album:
                DrawAlbumIcon(texture);
                break;
        }

        texture.Apply();
        texture.hideFlags = HideFlags.DontSave;

        cached = Sprite.Create(texture, new Rect(0f, 0f, 64f, 64f), new Vector2(0.5f, 0.5f), 64f);
        cached.hideFlags = HideFlags.DontSave;
        SpriteCache[key] = cached;
        return cached;
    }

    private static bool IsInsideRoundedRect(int x, int y, int width, int height, int radius)
    {
        int left = radius;
        int right = width - radius - 1;
        int bottom = radius;
        int top = height - radius - 1;

        if (x >= left && x <= right) return true;
        if (y >= bottom && y <= top) return true;

        int cx = x < left ? left : right;
        int cy = y < bottom ? bottom : top;
        int dx = x - cx;
        int dy = y - cy;
        return dx * dx + dy * dy <= radius * radius;
    }

    private static void Clear(Texture2D texture)
    {
        Color clear = new Color(1f, 1f, 1f, 0f);
        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
                texture.SetPixel(x, y, clear);
        }
    }

    private static void DrawInventoryIcon(Texture2D texture)
    {
        Color c = Color.white;
        DrawRoundedRect(texture, 13, 22, 51, 52, 6, c);
        DrawRect(texture, 20, 18, 44, 27, c);
        DrawRect(texture, 24, 13, 29, 24, c);
        DrawRect(texture, 35, 13, 40, 24, c);
        DrawRect(texture, 28, 13, 36, 18, c);
        DrawRect(texture, 18, 32, 46, 36, new Color(0f, 0f, 0f, 0.2f));
    }

    private static void DrawShopIcon(Texture2D texture)
    {
        Color c = Color.white;
        DrawRect(texture, 15, 28, 49, 51, c);
        DrawRect(texture, 19, 34, 28, 51, new Color(0f, 0f, 0f, 0.18f));
        DrawRect(texture, 34, 34, 45, 51, new Color(0f, 0f, 0f, 0.18f));
        DrawRect(texture, 12, 20, 52, 29, c);
        DrawRect(texture, 15, 13, 23, 24, c);
        DrawRect(texture, 28, 13, 36, 24, c);
        DrawRect(texture, 41, 13, 49, 24, c);
        DrawRect(texture, 22, 13, 29, 24, new Color(0f, 0f, 0f, 0.18f));
        DrawRect(texture, 36, 13, 43, 24, new Color(0f, 0f, 0f, 0.18f));
    }

    private static void DrawAlbumIcon(Texture2D texture)
    {
        Color c = Color.white;
        DrawRoundedRect(texture, 16, 10, 48, 55, 5, c);
        DrawRect(texture, 18, 12, 24, 53, new Color(0f, 0f, 0f, 0.18f));
        DrawRect(texture, 28, 20, 43, 24, new Color(0f, 0f, 0f, 0.22f));
        DrawRect(texture, 28, 30, 43, 34, new Color(0f, 0f, 0f, 0.22f));
        DrawRect(texture, 28, 40, 39, 44, new Color(0f, 0f, 0f, 0.22f));
        DrawRect(texture, 20, 14, 22, 51, c);
    }

    private static void DrawRect(Texture2D texture, int minX, int minY, int maxX, int maxY, Color color)
    {
        for (int y = Mathf.Max(0, minY); y < Mathf.Min(texture.height, maxY); y++)
        {
            for (int x = Mathf.Max(0, minX); x < Mathf.Min(texture.width, maxX); x++)
                texture.SetPixel(x, y, color);
        }
    }

    private static void DrawRoundedRect(Texture2D texture, int minX, int minY, int maxX, int maxY, int radius, Color color)
    {
        int width = maxX - minX;
        int height = maxY - minY;
        for (int y = minY; y < maxY; y++)
        {
            for (int x = minX; x < maxX; x++)
            {
                if (IsInsideRoundedRect(x - minX, y - minY, width, height, radius))
                    texture.SetPixel(x, y, color);
            }
        }
    }

    private static void TryInvoke(KeyCode key, Button button)
    {
        if (key == KeyCode.None || button == null || !button.interactable || !button.gameObject.activeInHierarchy)
            return;

        if (Input.GetKeyDown(key))
            button.onClick.Invoke();
    }

    private static bool ShouldIgnoreInput()
    {
        if (G.Control != null && G.Control.UseTouchControl)
            return true;

        GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (selected == null)
            return false;

        return selected.GetComponentInParent<TMP_InputField>() != null || selected.GetComponentInParent<InputField>() != null;
    }
}
