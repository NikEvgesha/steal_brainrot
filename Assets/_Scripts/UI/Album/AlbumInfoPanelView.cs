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
    [SerializeField] private Image eggHatchIconTemplate;
    [SerializeField] private int eggHatchMaxIcons = 4;

    private readonly List<Image> _eggHatchIconPool = new();

    public void ShowLocked(Sprite icon, Color iconColor, string unknownText)
    {
        SetIcon(icon, iconColor);
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

    public void ShowEgg(
        Sprite icon,
        Color iconColor,
        string title,
        string dateText,
        string priceText,
        string sourcesText,
        IReadOnlyList<HatchIconData> hatchIcons,
        Color hatchUnlockedColor,
        Color hatchLockedColor)
    {
        SetMode(AlbumEntityType.Egg, true);
        SetIcon(icon, iconColor);
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
        string sourcesText)
    {
        SetMode(AlbumEntityType.Animal, true);
        SetIcon(icon, iconColor);
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

    private void SetIcon(Sprite icon, Color color)
    {
        if (infoIcon == null)
            return;

        infoIcon.sprite = icon;
        infoIcon.color = color;
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
            var icon = _eggHatchIconPool[i];
            if (icon == null)
                continue;

            var show = shouldShowSection && hatchIcons != null && i < hatchIcons.Count;
            icon.gameObject.SetActive(show);
            if (!show)
                continue;

            var data = hatchIcons[i];
            icon.sprite = data.Icon;
            icon.color = data.Unlocked ? unlockedColor : lockedColor;
        }

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
                if (eggHatchIconTemplate != null && child == eggHatchIconTemplate.transform)
                    continue;

                var childImage = child.GetComponent<Image>();
                if (childImage != null)
                    _eggHatchIconPool.Add(childImage);
            }
        }

        if (eggHatchIconTemplate != null)
            eggHatchIconTemplate.gameObject.SetActive(false);

        if (eggHatchIconTemplate != null)
        {
            while (_eggHatchIconPool.Count < requiredSlots)
            {
                var icon = Instantiate(eggHatchIconTemplate, iconsRoot);
                icon.gameObject.SetActive(false);
                _eggHatchIconPool.Add(icon);
            }
        }

        while (_eggHatchIconPool.Count < requiredSlots)
        {
            var iconObject = new GameObject("EggHatchIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconObject.transform.SetParent(iconsRoot, false);
            var icon = iconObject.GetComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.gameObject.SetActive(false);
            _eggHatchIconPool.Add(icon);
        }

        return _eggHatchIconPool.Count > 0 || requiredSlots <= 0;
    }

    private Transform ResolveEggHatchIconsRoot(bool allowFallback)
    {
        if (eggHatchIconsRoot != null)
            return eggHatchIconsRoot;
        if (!allowFallback || eggSourcesText == null)
            return null;

        return eggSourcesText.transform;
    }

    private void LayoutFallbackHatchIcons(Transform iconsRoot, int visibleCount)
    {
        if (iconsRoot == null || iconsRoot == eggHatchIconsRoot || visibleCount <= 0)
            return;

        var totalWidth = visibleCount * FallbackHatchIconSize + Mathf.Max(0, visibleCount - 1) * FallbackHatchIconSpacing;
        var startX = -totalWidth * 0.5f + FallbackHatchIconSize * 0.5f;

        var visibleIndex = 0;
        for (var i = 0; i < _eggHatchIconPool.Count; i++)
        {
            var icon = _eggHatchIconPool[i];
            if (icon == null || !icon.gameObject.activeSelf)
                continue;

            if (icon.transform is RectTransform rect)
            {
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(FallbackHatchIconSize, FallbackHatchIconSize);
                rect.anchoredPosition = new Vector2(startX + visibleIndex * (FallbackHatchIconSize + FallbackHatchIconSpacing), 0f);
            }

            visibleIndex++;
        }
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
        SetEggHatchSectionActive(false);
    }

    private void SetEggHatchSectionActive(bool active)
    {
        if (eggHatchSection != null)
            eggHatchSection.SetActive(active);

        if (eggHatchIconTemplate != null)
            eggHatchIconTemplate.gameObject.SetActive(false);
    }
}
