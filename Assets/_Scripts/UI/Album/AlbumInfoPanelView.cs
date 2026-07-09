using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class AlbumInfoPanelView : MonoBehaviour
{
    private const float FallbackHatchIconSize = 52f;
    private const float FallbackHatchIconSpacing = 8f;

    private enum EditorPreviewMode
    {
        None,
        Egg,
        Animal
    }

    [System.Serializable]
    private sealed class ModeSlotVisibility
    {
        [SerializeField] private bool showIcon = true;
        [SerializeField] private bool showTitle = true;
        [SerializeField] private bool showDescription = true;
        [SerializeField] private bool showIncome = true;
        [SerializeField] private bool showSources = true;
        [SerializeField] private bool showModeObjects = true;
        [SerializeField] private bool showHatchPreview = true;

        public bool ShowIcon => showIcon;
        public bool ShowTitle => showTitle;
        public bool ShowDescription => showDescription;
        public bool ShowIncome => showIncome;
        public bool ShowSources => showSources;
        public bool ShowModeObjects => showModeObjects;
        public bool ShowHatchPreview => showHatchPreview;
    }

    public readonly struct HatchIconData
    {
        public HatchIconData(Sprite icon, bool unlocked)
        {
            Icon = icon;
            Unlocked = unlocked;
        }

        public Sprite Icon { get; }
        public bool Unlocked { get; }
    }

    [Header("Editor Preview")]
    [SerializeField] private EditorPreviewMode editorPreviewMode;

    [Header("Mode Visibility")]
    [SerializeField] private ModeSlotVisibility eggModeSlots = new();
    [SerializeField] private ModeSlotVisibility animalModeSlots = new();

    [Header("Common")]
    [SerializeField] private Image infoIcon;
    [SerializeField] private Image infoIconFxImage;
    [SerializeField] private bool createInfoIconFxIfMissing = true;
    [SerializeField] private float infoIconFxScale = 1.55f;
    [SerializeField] private Color lockedInfoIconFxColor = Color.black;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private GameObject lockedOverlay;
    [SerializeField] private TMP_Text lockedText;

    [Header("Egg Mode")]
    [SerializeField] private TMP_Text eggDateText;
    [SerializeField] private TMP_Text eggPriceText;
    [SerializeField] private TMP_Text eggSourcesText;
    [SerializeField] private GameObject[] eggOnlyObjects;

    [Header("Animal Mode")]
    [SerializeField] private TMP_Text animalDescriptionText;
    [SerializeField] private TMP_Text animalIncomeText;
    [SerializeField] private TMP_Text animalSourcesText;
    [SerializeField] private GameObject[] animalOnlyObjects;

    [Header("Egg Hatch Preview")]
    [SerializeField] private GameObject eggHatchSection;
    [SerializeField] private Transform eggHatchIconsRoot;
    [SerializeField] private AlbumHatchIconView eggHatchIconPrefab;
    [SerializeField] private Image eggHatchIconTemplate;
    [SerializeField] private int eggHatchMaxIcons = 4;
    [SerializeField] private float eggHatchIconSpacing = FallbackHatchIconSpacing;

    private readonly List<AlbumHatchIconView> _eggHatchIconPool = new();
    private bool _autoCreatedInfoIconFx;
    private static Sprite _defaultInfoIconFxSprite;

    public void ShowLocked(Sprite icon, Color iconColor, string unknownText, ElementType elementType = ElementType.NoElement)
    {
        SetIcon(icon, iconColor, elementType, false);
        SetMode(AlbumEntityType.Egg, false);
        SetText(titleText, unknownText);
        SetText(eggDateText, unknownText);
        SetText(eggPriceText, unknownText);
        SetText(eggSourcesText, unknownText);
        SetText(animalDescriptionText, unknownText);
        SetText(animalIncomeText, unknownText);
        SetText(animalSourcesText, unknownText);
        SetLockedOverlay(true, unknownText);
        SetEggHatchIcons(null, false, Color.white, Color.black);
    }

    public void ShowLockedEgg(
        Sprite icon,
        Color iconColor,
        string unknownText,
        IReadOnlyList<HatchIconData> hatchIcons,
        Color hatchUnlockedColor,
        Color hatchLockedColor,
        ElementType elementType = ElementType.NoElement)
    {
        SetIcon(icon, iconColor, elementType, false);
        SetMode(AlbumEntityType.Egg, false);
        SetText(titleText, unknownText);
        SetText(eggDateText, unknownText);
        SetText(eggPriceText, unknownText);
        SetText(eggSourcesText, string.Empty);
        SetText(animalDescriptionText, unknownText);
        SetText(animalIncomeText, unknownText);
        SetText(animalSourcesText, unknownText);
        SetLockedOverlay(true, unknownText);
        SetEggHatchIcons(hatchIcons, true, hatchUnlockedColor, hatchLockedColor);
    }

    public void ShowEgg(
        Sprite icon,
        Color iconColor,
        string title,
        string dateText,
        string priceText,
        string sourcesText,
        IReadOnlyList<HatchIconData> hatchIcons,
        Color hatchUnlockedColor,
        Color hatchLockedColor,
        ElementType elementType = ElementType.NoElement)
    {
        SetMode(AlbumEntityType.Egg, true);
        SetIcon(icon, iconColor, elementType, true);
        SetText(titleText, title);
        SetText(eggDateText, dateText);
        SetText(eggPriceText, priceText);
        SetText(eggSourcesText, sourcesText);
        SetLockedOverlay(false, string.Empty);
        SetEggHatchIcons(hatchIcons, true, hatchUnlockedColor, hatchLockedColor);
    }

    public void ShowAnimal(
        Sprite icon,
        Color iconColor,
        string title,
        string descriptionText,
        string incomeText,
        string sourcesText,
        ElementType elementType = ElementType.NoElement)
    {
        SetMode(AlbumEntityType.Animal, true);
        SetIcon(icon, iconColor, elementType, true);
        SetText(titleText, title);
        SetText(animalDescriptionText, descriptionText);
        SetText(animalIncomeText, incomeText);
        SetText(animalSourcesText, sourcesText);
        SetLockedOverlay(false, string.Empty);
        SetEggHatchIcons(null, false, Color.white, Color.black);
    }

    public bool CanShowEggHatchPreview(IReadOnlyList<HatchIconData> hatchIcons)
    {
        return hatchIcons != null
               && hatchIcons.Count > 0
               && GetModeSlots(AlbumEntityType.Egg).ShowHatchPreview
               && ResolveEggHatchIconsRoot(true) != null;
    }

    public void ApplyEditorPreview()
    {
        if (Application.isPlaying)
            return;

        switch (editorPreviewMode)
        {
            case EditorPreviewMode.Egg:
                ApplyEditorModePreview(AlbumEntityType.Egg);
                break;
            case EditorPreviewMode.Animal:
                ApplyEditorModePreview(AlbumEntityType.Animal);
                break;
            default:
                ApplyNoEditorPreview();
                break;
        }
    }

    private void SetMode(AlbumEntityType mode, bool unlocked)
    {
        var slots = GetModeSlots(mode);
        var visible = new HashSet<GameObject>();

        SetImageObjectActive(infoIcon, slots.ShowIcon);
        if (!slots.ShowIcon)
            SetInfoIconFxVisible(false);

        if (slots.ShowTitle)
            AddTextObject(visible, titleText);

        if (mode == AlbumEntityType.Egg)
        {
            if (slots.ShowDescription)
                AddTextObject(visible, eggDateText);
            if (slots.ShowIncome)
                AddTextObject(visible, eggPriceText);
            if (slots.ShowSources)
                AddTextObject(visible, eggSourcesText);
        }
        else
        {
            if (slots.ShowDescription)
                AddTextObject(visible, animalDescriptionText);
            if (slots.ShowIncome)
                AddTextObject(visible, animalIncomeText);
            if (slots.ShowSources)
                AddTextObject(visible, animalSourcesText);
        }

        SetTextObjectActive(titleText, visible.Contains(GetGameObject(titleText)));
        SetTextObjectActive(eggDateText, visible.Contains(GetGameObject(eggDateText)));
        SetTextObjectActive(eggPriceText, visible.Contains(GetGameObject(eggPriceText)));
        SetTextObjectActive(eggSourcesText, visible.Contains(GetGameObject(eggSourcesText)));
        SetTextObjectActive(animalDescriptionText, visible.Contains(GetGameObject(animalDescriptionText)));
        SetTextObjectActive(animalIncomeText, visible.Contains(GetGameObject(animalIncomeText)));
        SetTextObjectActive(animalSourcesText, visible.Contains(GetGameObject(animalSourcesText)));

        SetObjectsActive(eggOnlyObjects, unlocked && mode == AlbumEntityType.Egg && slots.ShowModeObjects);
        SetObjectsActive(animalOnlyObjects, unlocked && mode == AlbumEntityType.Animal && slots.ShowModeObjects);
    }

    private ModeSlotVisibility GetModeSlots(AlbumEntityType mode)
    {
        if (eggModeSlots == null)
            eggModeSlots = new ModeSlotVisibility();
        if (animalModeSlots == null)
            animalModeSlots = new ModeSlotVisibility();

        return mode == AlbumEntityType.Egg ? eggModeSlots : animalModeSlots;
    }

    private static void AddTextObject(HashSet<GameObject> visible, TMP_Text text)
    {
        if (text != null)
            visible.Add(text.gameObject);
    }

    private static GameObject GetGameObject(TMP_Text text)
    {
        return text != null ? text.gameObject : null;
    }

    private static void SetTextObjectActive(TMP_Text text, bool active)
    {
        if (text != null)
            text.gameObject.SetActive(active);
    }

    private static void SetImageObjectActive(Image image, bool active)
    {
        if (image != null)
            image.gameObject.SetActive(active);
    }

    private static void SetObjectsActive(IReadOnlyList<GameObject> objects, bool active)
    {
        if (objects == null)
            return;

        for (var i = 0; i < objects.Count; i++)
        {
            if (objects[i] != null)
                objects[i].SetActive(active);
        }
    }

    private void SetIcon(Sprite icon, Color color, ElementType elementType = ElementType.NoElement, bool elementUnlocked = true)
    {
        if (infoIcon == null)
        {
            SetInfoIconFxVisible(false);
            return;
        }

        infoIcon.sprite = icon;
        infoIcon.color = color;
        ApplyInfoIconFx(elementType, elementUnlocked, icon != null);
    }

    private void ApplyInfoIconFx(ElementType elementType, bool unlocked, bool iconVisible)
    {
        if (!iconVisible || !IsElementFxVisible(elementType))
        {
            SetInfoIconFxVisible(false);
            return;
        }

        EnsureInfoIconFxImage();
        if (infoIconFxImage == null)
            return;

        infoIconFxImage.sprite = GetDefaultInfoIconFxSprite();
        infoIconFxImage.enabled = infoIconFxImage.sprite != null;
        infoIconFxImage.preserveAspect = true;
        infoIconFxImage.raycastTarget = false;
        infoIconFxImage.color = unlocked ? GetElementFxColor(elementType) : lockedInfoIconFxColor;
        if (_autoCreatedInfoIconFx)
            PlaceInfoIconFxBehindIcon();
    }

    private void SetInfoIconFxVisible(bool visible)
    {
        if (infoIconFxImage != null)
            infoIconFxImage.enabled = visible;
    }

    private void EnsureInfoIconFxImage()
    {
        if (infoIconFxImage != null || !createInfoIconFxIfMissing || infoIcon == null)
            return;

        var parent = infoIcon.transform.parent as RectTransform;
        if (parent == null)
            parent = transform as RectTransform;
        if (parent == null)
            return;

        var go = new GameObject("InfoIconFx", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        infoIconFxImage = go.GetComponent<Image>();
        _autoCreatedInfoIconFx = true;
        infoIconFxImage.raycastTarget = false;
        infoIconFxImage.enabled = false;
        PlaceInfoIconFxBehindIcon();
    }

    private void PlaceInfoIconFxBehindIcon()
    {
        if (infoIconFxImage == null || infoIcon == null)
            return;

        var fxTransform = infoIconFxImage.rectTransform;
        var iconTransform = infoIcon.rectTransform;
        fxTransform.anchorMin = iconTransform.anchorMin;
        fxTransform.anchorMax = iconTransform.anchorMax;
        fxTransform.pivot = iconTransform.pivot;
        fxTransform.anchoredPosition = iconTransform.anchoredPosition;
        fxTransform.localRotation = iconTransform.localRotation;
        fxTransform.localScale = Vector3.one;

        var size = iconTransform.rect.size;
        if (size.x <= 0f || size.y <= 0f)
            size = iconTransform.sizeDelta;
        if (size.x <= 0f || size.y <= 0f)
            size = new Vector2(96f, 96f);
        fxTransform.sizeDelta = size * Mathf.Max(1f, infoIconFxScale);

        var iconIndex = infoIcon.transform.GetSiblingIndex();
        infoIconFxImage.transform.SetSiblingIndex(Mathf.Max(0, iconIndex));
    }

    private static bool IsElementFxVisible(ElementType elementType)
    {
        return elementType != ElementType.ElementType && elementType != ElementType.NoElement;
    }

    private static Sprite GetDefaultInfoIconFxSprite()
    {
        if (_defaultInfoIconFxSprite != null)
            return _defaultInfoIconFxSprite;

        const int size = 160;
        const float center = (size - 1) * 0.5f;
        const int rayCount = 20;

        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "AlbumInfoIconFx_Runtime",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        var pixels = new Color32[size * size];
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var dx = (x - center) / center;
                var dy = (y - center) / center;
                var radius = Mathf.Sqrt(dx * dx + dy * dy);
                var alpha = 0f;

                if (radius <= 1f)
                {
                    var angle = Mathf.Atan2(dy, dx);
                    var wave = Mathf.Abs(Mathf.Cos(angle * rayCount * 0.5f));
                    var rays = Mathf.Pow(wave, 10f);
                    var core = Mathf.Clamp01(1f - radius * 3.1f) * 0.35f;
                    var fade = Mathf.Pow(Mathf.Clamp01(1f - radius), 1.25f);
                    alpha = Mathf.Clamp01((rays * 0.95f + core) * fade);
                }

                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);

        _defaultInfoIconFxSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            100f);
        _defaultInfoIconFxSprite.name = "AlbumInfoIconFx_RuntimeSprite";
        _defaultInfoIconFxSprite.hideFlags = HideFlags.HideAndDontSave;
        return _defaultInfoIconFxSprite;
    }

    private static Color GetElementFxColor(ElementType elementType)
    {
        switch (elementType)
        {
            case ElementType.Gold:
                return new Color(1f, 0.78f, 0.05f, 0.82f);
            case ElementType.Diamond:
                return new Color(0.15f, 0.9f, 1f, 0.82f);
            case ElementType.Electric:
                return new Color(0.48f, 0.52f, 1f, 0.82f);
            case ElementType.Fire:
                return new Color(1f, 0.32f, 0.05f, 0.82f);
            default:
                return new Color(1f, 1f, 1f, 0.72f);
        }
    }

    private void SetLockedOverlay(bool visible, string text)
    {
        if (lockedOverlay != null)
            lockedOverlay.SetActive(visible);
        if (lockedText != null)
            lockedText.text = text;
    }

    private static void SetText(TMP_Text target, string text)
    {
        if (target != null)
            target.text = text ?? string.Empty;
    }

    private bool SetEggHatchIcons(
        IReadOnlyList<HatchIconData> hatchIcons,
        bool visible,
        Color unlockedColor,
        Color lockedColor)
    {
        visible = visible && GetModeSlots(AlbumEntityType.Egg).ShowHatchPreview;
        var shouldShowSection = visible && hatchIcons != null && hatchIcons.Count > 0;
        var iconsRoot = ResolveEggHatchIconsRoot(visible || _eggHatchIconPool.Count > 0);
        var requiredSlots = shouldShowSection
            ? Mathf.Max(eggHatchMaxIcons > 0 ? eggHatchMaxIcons : 0, hatchIcons.Count)
            : 0;

        if (!EnsureEggHatchIconPool(requiredSlots, iconsRoot))
        {
            if (eggHatchSection != null)
                eggHatchSection.SetActive(false);
            return false;
        }

        if (eggHatchSection != null)
            eggHatchSection.SetActive(shouldShowSection);

        for (var i = 0; i < _eggHatchIconPool.Count; i++)
        {
            var iconView = _eggHatchIconPool[i];
            if (iconView == null)
                continue;

            var show = shouldShowSection && hatchIcons != null && i < hatchIcons.Count;
            iconView.gameObject.SetActive(show);
            if (!show)
                continue;

            var data = hatchIcons[i];
            iconView.SetIcon(data.Icon, data.Unlocked ? unlockedColor : lockedColor);
        }

        if (!TryRebuildHatchGrid(iconsRoot))
            LayoutFallbackHatchIcons(iconsRoot, shouldShowSection ? Mathf.Min(hatchIcons.Count, _eggHatchIconPool.Count) : 0);
        return true;
    }

    private bool EnsureEggHatchIconPool(int requiredSlots, Transform iconsRoot)
    {
        _eggHatchIconPool.RemoveAll(x => x == null);
        if (iconsRoot == null)
            return requiredSlots <= 0;

        if (_eggHatchIconPool.Count == 0 && eggHatchIconsRoot != null)
        {
            for (var i = 0; i < eggHatchIconsRoot.childCount; i++)
            {
                var child = eggHatchIconsRoot.GetChild(i);
                if (eggHatchIconPrefab != null && child == eggHatchIconPrefab.transform)
                    continue;
                if (eggHatchIconTemplate != null && child == eggHatchIconTemplate.transform)
                    continue;

                var childIcon = child.GetComponent<AlbumHatchIconView>();
                if (childIcon == null && child.GetComponent<Image>() != null)
                {
                    childIcon = child.gameObject.AddComponent<AlbumHatchIconView>();
                    childIcon.AutoWire();
                }

                if (childIcon != null)
                    _eggHatchIconPool.Add(childIcon);
            }
        }

        if (eggHatchIconPrefab != null && eggHatchIconPrefab.transform.parent == iconsRoot)
            eggHatchIconPrefab.gameObject.SetActive(false);
        if (eggHatchIconTemplate != null)
            eggHatchIconTemplate.gameObject.SetActive(false);

        if (eggHatchIconPrefab != null)
        {
            while (_eggHatchIconPool.Count < requiredSlots)
            {
                var icon = Instantiate(eggHatchIconPrefab, iconsRoot);
                icon.name = $"EggHatchIcon {_eggHatchIconPool.Count + 1}";
                icon.AutoWire();
                icon.gameObject.SetActive(false);
                _eggHatchIconPool.Add(icon);
            }
        }

        if (eggHatchIconTemplate != null)
        {
            while (_eggHatchIconPool.Count < requiredSlots)
            {
                var iconImage = Instantiate(eggHatchIconTemplate, iconsRoot);
                var icon = iconImage.GetComponent<AlbumHatchIconView>();
                if (icon == null)
                    icon = iconImage.gameObject.AddComponent<AlbumHatchIconView>();
                icon.AutoWire();
                icon.gameObject.SetActive(false);
                _eggHatchIconPool.Add(icon);
            }
        }

        while (_eggHatchIconPool.Count < requiredSlots)
        {
            var iconObject = new GameObject("EggHatchIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(AlbumHatchIconView));
            iconObject.transform.SetParent(iconsRoot, false);
            var icon = iconObject.GetComponent<AlbumHatchIconView>();
            icon.AutoWire();
            icon.gameObject.SetActive(false);
            _eggHatchIconPool.Add(icon);
        }

        return _eggHatchIconPool.Count > 0 || requiredSlots <= 0;
    }

    private Transform ResolveEggHatchIconsRoot(bool allowFallback)
    {
        if (eggHatchIconsRoot != null)
            return eggHatchIconsRoot;

        eggHatchIconsRoot = FindChildTransform(transform, "EggHatchIconsRoot") ??
                            FindChildTransform(transform, "HatchIconsRoot") ??
                            FindChildTransform(transform, "InfoHatchIcons") ??
                            FindChildTransform(transform, "InfoSources");
        if (eggHatchIconsRoot != null)
            return eggHatchIconsRoot;

        if (!allowFallback || eggSourcesText == null)
            return null;

        return eggSourcesText.transform;
    }

    private static Transform FindChildTransform(Transform root, string childName)
    {
        if (root == null || string.IsNullOrWhiteSpace(childName))
            return null;

        if (string.Equals(root.name, childName, System.StringComparison.Ordinal))
            return root;

        for (var i = 0; i < root.childCount; i++)
        {
            var child = root.GetChild(i);
            if (child == null)
                continue;

            if (string.Equals(child.name, childName, System.StringComparison.Ordinal))
                return child;

            var nested = FindChildTransform(child, childName);
            if (nested != null)
                return nested;
        }

        return null;
    }

    private void LayoutFallbackHatchIcons(Transform iconsRoot, int visibleCount)
    {
        if (iconsRoot == null || iconsRoot == eggHatchIconsRoot || visibleCount <= 0)
            return;

        var totalWidth = 0f;
        for (var i = 0; i < _eggHatchIconPool.Count; i++)
        {
            var icon = _eggHatchIconPool[i];
            if (icon == null || !icon.gameObject.activeSelf)
                continue;

            totalWidth += ResolveFallbackHatchIconSize(icon).x;
        }

        var spacing = Mathf.Max(0f, eggHatchIconSpacing);
        totalWidth += Mathf.Max(0, visibleCount - 1) * spacing;
        var currentX = -totalWidth * 0.5f;
        for (var i = 0; i < _eggHatchIconPool.Count; i++)
        {
            var icon = _eggHatchIconPool[i];
            if (icon == null || !icon.gameObject.activeSelf)
                continue;

            if (icon.transform is RectTransform rect)
            {
                var size = ResolveFallbackHatchIconSize(icon);
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = size;
                rect.anchoredPosition = new Vector2(currentX + size.x * 0.5f, 0f);
                currentX += size.x + spacing;
            }
        }
    }

    private static bool TryRebuildHatchGrid(Transform iconsRoot)
    {
        if (iconsRoot == null)
            return false;

        var grid = iconsRoot.GetComponent<AdaptiveGridSpawner>();
        if (grid == null)
            return false;

        grid.Rebuild();
        return true;
    }

    private static Vector2 ResolveFallbackHatchIconSize(AlbumHatchIconView icon)
    {
        if (icon != null && icon.transform is RectTransform rect)
        {
            var size = rect.sizeDelta;
            if (size.x > 0.01f && size.y > 0.01f)
                return size;
        }

        return new Vector2(FallbackHatchIconSize, FallbackHatchIconSize);
    }

    private void ApplyEditorModePreview(AlbumEntityType mode)
    {
        SetMode(mode, true);
        SetLockedOverlay(false, string.Empty);
        SetEggHatchSectionActive(mode == AlbumEntityType.Egg && GetModeSlots(AlbumEntityType.Egg).ShowHatchPreview);

        if (mode == AlbumEntityType.Egg)
        {
            SetText(titleText, "Egg Preview");
            SetText(eggDateText, "Description / date");
            SetText(eggPriceText, "Income / price");
            SetText(eggSourcesText, "Sources");
            return;
        }

        SetText(titleText, "Animal Preview");
        SetText(animalDescriptionText, "Description");
        SetText(animalIncomeText, "Income");
        SetText(animalSourcesText, "Sources");
    }

    private void ApplyNoEditorPreview()
    {
        SetImageObjectActive(infoIcon, false);
        SetTextObjectActive(titleText, false);
        SetTextObjectActive(eggDateText, false);
        SetTextObjectActive(eggPriceText, false);
        SetTextObjectActive(eggSourcesText, false);
        SetTextObjectActive(animalDescriptionText, false);
        SetTextObjectActive(animalIncomeText, false);
        SetTextObjectActive(animalSourcesText, false);
        SetObjectsActive(eggOnlyObjects, false);
        SetObjectsActive(animalOnlyObjects, false);
        SetLockedOverlay(false, string.Empty);
        SetInfoIconFxVisible(false);
        SetEggHatchSectionActive(false);
    }

    private void SetEggHatchSectionActive(bool active)
    {
        if (eggHatchSection != null)
            eggHatchSection.SetActive(active);

        if (eggHatchIconPrefab != null && eggHatchIconPrefab.transform.parent == eggHatchIconsRoot)
            eggHatchIconPrefab.gameObject.SetActive(false);
        if (eggHatchIconTemplate != null)
            eggHatchIconTemplate.gameObject.SetActive(false);
    }
}
