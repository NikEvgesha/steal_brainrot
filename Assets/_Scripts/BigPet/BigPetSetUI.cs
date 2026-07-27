using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class BigPetSetUI : MonoBehaviour
{
    [SerializeField] private GameObject _uiPanel;
    [SerializeField] private BigPetSetSlot _slotPrefab;
    [SerializeField] private Transform _slotParent;
    [SerializeField] private bool _remoteMode;
    [SerializeField, Min(0.25f)] private float _closeDistanceBuffer = 1.5f;
    [SerializeField, Min(0.02f)] private float _distanceCheckInterval = 0.1f;
    [Header("Element filters")]
    [SerializeField] private Sprite _normalElementIcon;
    [SerializeField] private Sprite _goldElementIcon;
    [SerializeField] private Sprite _diamondElementIcon;
    [SerializeField] private Sprite _electricElementIcon;
    [SerializeField] private Sprite _fireElementIcon;
    [SerializeField] private Sprite _lockIcon;
    [SerializeField] private Sprite _buttonGradientSprite;

    private List<BigPetSetSlot> _slots = new();
    private readonly List<ElementFilterButton> _elementButtons = new();
    private Collider _interactionCollider;
    private float _nextDistanceCheckTime;
    private TMP_Text _incomeBonusText;
    private TMP_Text _titleText;
    private ScrollRect _scrollRect;
    private LocalizationManager _subscribedLocalizationManager;
    private Transform _categoryBar;
    private Transform _elementBar;
    private Button _normalTab;
    private Button _upgradedTab;
    private TMP_Text _normalTabText;
    private TMP_Text _upgradedTabText;
    private int _basePetCount;
    private int _maxAvailableVariantIndex;
    private int _activeVariantIndex;
    private bool _showUpgraded;
    private ElementType _selectedElement = ElementType.Gold;

    private static readonly ElementType[] ProgressionElements =
    {
        ElementType.NoElement,
        ElementType.Gold,
        ElementType.Diamond,
        ElementType.Electric,
        ElementType.Fire
    };

    private sealed class ElementFilterButton
    {
        public ElementType element;
        public Button button;
        public Image icon;
        public Image lockIcon;
        public Outline outline;
    }

    [HideInInspector]
    public UnityEvent<int> PetSlotClicked = new();
    [HideInInspector]
    public UnityEvent<int> ActiveChanged = new();

    public bool IsOpen => _uiPanel != null && _uiPanel.activeSelf;

    private void Awake()
    {
        ConfigureWindowVisuals();
    }

    private void OnEnable()
    {
        BigPetPoint.LocalLevelChanged -= OnBigPetLevelChanged;
        BigPetPoint.LocalLevelChanged += OnBigPetLevelChanged;
        LocalizationManager.OnInstanceReady -= OnLocalizationManagerReady;
        LocalizationManager.OnInstanceReady += OnLocalizationManagerReady;
        LocalizationUtils.OnFallbackLanguageChanged -= OnLanguageChanged;
        LocalizationUtils.OnFallbackLanguageChanged += OnLanguageChanged;
        SubscribeToLocalizationManager(LocalizationManager.Instance);
    }

    private void OnDisable()
    {
        BigPetPoint.LocalLevelChanged -= OnBigPetLevelChanged;
        LocalizationManager.OnInstanceReady -= OnLocalizationManagerReady;
        LocalizationUtils.OnFallbackLanguageChanged -= OnLanguageChanged;
        UnsubscribeFromLocalizationManager();
    }

    private void Update()
    {
        if (_remoteMode || !IsOpen || _interactionCollider == null || G.Player == null)
            return;
        if (Time.unscaledTime < _nextDistanceCheckTime)
            return;

        _nextDistanceCheckTime = Time.unscaledTime + Mathf.Max(0.02f, _distanceCheckInterval);
        Vector3 playerPosition = G.Player.transform.position;
        Vector3 closestPoint = _interactionCollider.ClosestPoint(playerPosition);
        float closeDistance = Mathf.Max(0.1f, _closeDistanceBuffer);
        if ((playerPosition - closestPoint).sqrMagnitude > closeDistance * closeDistance)
            OpenUI(false);
    }

    public void InitUI(IReadOnlyList<Brainrot> petList)
    {
        ClearSlots();

        if (petList == null || _slotParent == null || _slotPrefab == null)
            return;

        _basePetCount = petList.Count;
        for (int elementIndex = 0; elementIndex < ProgressionElements.Length; elementIndex++)
        {
            ElementType element = ProgressionElements[elementIndex];
            for (int petIndex = 0; petIndex < petList.Count; petIndex++)
            {
                Brainrot pet = petList[petIndex];
                if (pet == null)
                    continue;

                int variantIndex = elementIndex * petList.Count + petIndex;
                BigPetSetSlot slot = Instantiate(_slotPrefab, _slotParent);
                slot.Init(this, pet, variantIndex, element);
                _slots.Add(slot);
            }
        }

        EnsureFilterControls();
        EnsureIncomeBonusBadge();
        RefreshIncomeBonusBadge();
        RefreshFilterState();
        RefreshSlotVisibility();
        RebuildGrid();
    }

    private void ClearSlots()
    {
        for (int i = 0; i < _slots.Count; i++)
        {
            var slot = _slots[i];
            if (slot == null)
                continue;

            slot.gameObject.SetActive(false);
            Destroy(slot.gameObject);
        }

        _slots.Clear();
    }

    public void SetMaxAvailablePet(int idx)
    {
        _maxAvailableVariantIndex = Mathf.Max(0, idx);
        RefreshFilterState();
        RefreshSlotVisibility();
        RebuildGrid();
    }

    private void RebuildGrid()
    {
        if (_slotParent != null && _slotParent.TryGetComponent<AdaptiveGridSpawner>(out var grid))
        {
            var parentRect = _slotParent as RectTransform;
            float width = parentRect != null && parentRect.parent is RectTransform viewport
                ? viewport.rect.width
                : Screen.width;
            int columns = width >= 620f ? 3 : 2;
            grid.ConfigureFitItemCount(
                0,
                new Vector2(10f, 10f),
                new RectOffset(10, 10, 10, 10),
                new Vector2(126f, 150f),
                new Vector2(210f, 230f),
                columns);
            grid.Rebuild();
        }

        if (_slotParent is RectTransform rectTransform)
            LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
    }


    //public void UpdateUI(Dictionary<Food, int> stock)
    //{
    //    foreach (BigPetSetSlot slot in _slots)
    //    {
    //        if (stock.ContainsKey(slot.Food))
    //        {
    //            int amount = stock[slot.Food];
    //            slot.SetAmount(amount);
    //            slot.SetAvailability(amount > 0);
    //        }
    //    }
    //}


    public void OnPetClicked(int variantIndex)
    {
        if (_remoteMode || variantIndex < 0 || variantIndex > _maxAvailableVariantIndex)
            return;

        PetSlotClicked?.Invoke(variantIndex);
    }


    public void OpenUI(bool open)
    {
        if (_remoteMode)
        {
            if (_uiPanel != null)
                _uiPanel.SetActive(false);
            return;
        }
        if (_uiPanel == null)
            return;

        _uiPanel.SetActive(open);
        if (open)
        {
            ConfigureWindowVisuals();
            SelectFilterForVariant(_activeVariantIndex);
            _uiPanel.transform.SetAsLastSibling();
            CanvasGroup canvasGroup = _uiPanel.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = _uiPanel.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;

            EnsureIncomeBonusBadge();
            RefreshIncomeBonusBadge();
            _nextDistanceCheckTime = Time.unscaledTime + Mathf.Max(0.02f, _distanceCheckInterval);
            RebuildGrid();
            Canvas.ForceUpdateCanvases();
        }
    }

    public void SetWorldTargeted(bool targeted)
    {
        if (_remoteMode)
            return;

        // Losing the world ray is expected as soon as the player moves the cursor
        // onto the screen-space menu. Keep the menu pinned until its explicit close
        // button calls OpenUI(false), otherwise the opening click races the close.
        if (targeted)
            OpenUI(true);
    }

    public void ConfigureWorldInteraction(Collider interactionCollider, float openDistance)
    {
        _interactionCollider = interactionCollider;
    }

    public void ChangeActivePet(int variantIndex)
    {
        _activeVariantIndex = Mathf.Max(0, variantIndex);
        ActiveChanged?.Invoke(_activeVariantIndex);
    }

    public void SetRemoteMode(bool remote)
    {
        _remoteMode = remote;
        if (_remoteMode && _uiPanel != null)
            _uiPanel.SetActive(false);
    }

    private void OnBigPetLevelChanged(int _)
    {
        RefreshIncomeBonusBadge();
    }

    private void OnLocalizationManagerReady(LocalizationManager manager)
    {
        SubscribeToLocalizationManager(manager);
    }

    private void SubscribeToLocalizationManager(LocalizationManager manager)
    {
        if (manager == null || manager == _subscribedLocalizationManager)
            return;

        UnsubscribeFromLocalizationManager();
        _subscribedLocalizationManager = manager;
        _subscribedLocalizationManager.OnLanguageChanged += OnLanguageChanged;
    }

    private void UnsubscribeFromLocalizationManager()
    {
        if (_subscribedLocalizationManager == null)
            return;

        _subscribedLocalizationManager.OnLanguageChanged -= OnLanguageChanged;
        _subscribedLocalizationManager = null;
    }

    private void OnLanguageChanged(string _)
    {
        RefreshIncomeBonusBadge();
        RefreshLocalizedText();
    }

    private void EnsureFilterControls()
    {
        if (_uiPanel == null || _remoteMode)
            return;

        Sprite textureSprite = ResolveTextureSprite(_uiPanel.GetComponent<Image>());
        if (_categoryBar == null)
        {
            _categoryBar = CreateBar("BigPetCategoryBar", new Vector2(0.035f, 0.755f), new Vector2(0.965f, 0.85f), textureSprite);
            _normalTab = CreateTextButton(_categoryBar, "NormalTab", new Color(0.04f, 0.72f, 0.86f, 1f), out _normalTabText);
            _upgradedTab = CreateTextButton(_categoryBar, "UpgradedTab", BlockyUITheme.BlueHeader, out _upgradedTabText);
            _normalTab.onClick.AddListener(() => SelectCategory(false));
            _upgradedTab.onClick.AddListener(() => SelectCategory(true));
        }

        if (_elementBar == null)
        {
            _elementBar = CreateBar("BigPetElementBar", new Vector2(0.035f, 0.625f), new Vector2(0.965f, 0.735f), textureSprite);
            _elementButtons.Clear();
            for (int i = 0; i < ProgressionElements.Length; i++)
            {
                ElementType element = ProgressionElements[i];
                ElementFilterButton binding = CreateElementButton(_elementBar, element, ResolveElementIcon(element), textureSprite);
                _elementButtons.Add(binding);
            }
        }

        _categoryBar.SetAsLastSibling();
        _elementBar.SetAsLastSibling();
    }

    private Transform CreateBar(string name, Vector2 anchorMin, Vector2 anchorMax, Sprite textureSprite)
    {
        Transform existing = _uiPanel.transform.Find(name);
        if (existing != null)
            return existing;

        GameObject barObject = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(HorizontalLayoutGroup));
        barObject.transform.SetParent(_uiPanel.transform, false);
        SetRect(barObject.transform as RectTransform, anchorMin, anchorMax);

        Image image = barObject.GetComponent<Image>();
        StyleImage(image, new Color(0.16f, 0.075f, 0.025f, 0.96f), textureSprite, false);
        EnsureOutline(barObject, new Vector2(3f, -3f), BlockyUITheme.BlackStroke);

        HorizontalLayoutGroup layout = barObject.GetComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(7, 7, 7, 7);
        layout.spacing = 7f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;
        return barObject.transform;
    }

    private Button CreateTextButton(Transform parent, string name, Color color, out TMP_Text label)
    {
        GameObject buttonObject = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button),
            typeof(LayoutElement));
        buttonObject.transform.SetParent(parent, false);
        Image image = buttonObject.GetComponent<Image>();
        StyleImage(image, color, ResolveTextureSprite(_uiPanel.GetComponent<Image>()), true);
        EnsureOutline(buttonObject, new Vector2(3f, -3f), BlockyUITheme.BlackStroke);

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.navigation = new Navigation { mode = Navigation.Mode.None };

        AddButtonGradient(buttonObject.transform);
        GameObject textObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(buttonObject.transform, false);
        SetRect(textObject.transform as RectTransform, Vector2.zero, Vector2.one);
        label = textObject.GetComponent<TMP_Text>();
        TmpUiTextFactory.ApplyDefaults(label);
        label.fontStyle = FontStyles.Bold;
        label.fontSize = 30f;
        label.enableAutoSizing = true;
        label.fontSizeMin = 15f;
        label.fontSizeMax = 32f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.outlineColor = BlockyUITheme.BlackStroke;
        label.outlineWidth = 0.18f;
        label.raycastTarget = false;
        return button;
    }

    private ElementFilterButton CreateElementButton(
        Transform parent,
        ElementType element,
        Sprite iconSprite,
        Sprite textureSprite)
    {
        GameObject buttonObject = new GameObject(
            "Element_" + element,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button),
            typeof(LayoutElement),
            typeof(Outline));
        buttonObject.transform.SetParent(parent, false);

        Image background = buttonObject.GetComponent<Image>();
        StyleImage(background, GetElementButtonColor(element), textureSprite, true);
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = background;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.onClick.AddListener(() => SelectElement(element));

        AddButtonGradient(buttonObject.transform);
        GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        iconObject.transform.SetParent(buttonObject.transform, false);
        SetRect(iconObject.transform as RectTransform, new Vector2(0.16f, 0.14f), new Vector2(0.84f, 0.86f));
        Image icon = iconObject.GetComponent<Image>();
        icon.sprite = iconSprite;
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        GameObject lockObject = new GameObject("Lock", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        lockObject.transform.SetParent(buttonObject.transform, false);
        SetRect(lockObject.transform as RectTransform, new Vector2(0.46f, 0.04f), new Vector2(0.96f, 0.54f));
        Image lockImage = lockObject.GetComponent<Image>();
        lockImage.sprite = _lockIcon;
        lockImage.preserveAspect = true;
        lockImage.color = Color.white;
        lockImage.raycastTarget = false;

        return new ElementFilterButton
        {
            element = element,
            button = button,
            icon = icon,
            lockIcon = lockImage,
            outline = buttonObject.GetComponent<Outline>()
        };
    }

    private void AddButtonGradient(Transform parent)
    {
        if (parent == null || _buttonGradientSprite == null)
            return;

        GameObject gradientObject = new GameObject("Gradient", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        gradientObject.transform.SetParent(parent, false);
        gradientObject.transform.SetAsFirstSibling();
        SetRect(gradientObject.transform as RectTransform, Vector2.zero, Vector2.one);
        Image gradient = gradientObject.GetComponent<Image>();
        gradient.sprite = _buttonGradientSprite;
        gradient.type = Image.Type.Sliced;
        gradient.color = new Color(1f, 1f, 1f, 0.72f);
        gradient.raycastTarget = false;
    }

    private void SelectCategory(bool upgraded)
    {
        _showUpgraded = upgraded;
        if (!_showUpgraded)
        {
            _selectedElement = ElementType.NoElement;
        }
        else if (_selectedElement == ElementType.NoElement)
        {
            _selectedElement = FindBestUnlockedElement();
        }
        RefreshFilterState();
        RefreshSlotVisibility();
        RebuildGrid();
        if (_scrollRect != null)
            _scrollRect.verticalNormalizedPosition = 1f;
    }

    private void SelectElement(ElementType element)
    {
        if (!IsElementUnlocked(element))
        {
            return;
        }

        _showUpgraded = element != ElementType.NoElement;
        _selectedElement = element;
        RefreshFilterState();
        RefreshSlotVisibility();
        RebuildGrid();
        if (_scrollRect != null)
            _scrollRect.verticalNormalizedPosition = 1f;
    }

    private void SelectFilterForVariant(int variantIndex)
    {
        if (_basePetCount <= 0)
            return;

        int elementIndex = Mathf.Clamp(variantIndex / _basePetCount, 0, ProgressionElements.Length - 1);
        _selectedElement = ProgressionElements[elementIndex];
        _showUpgraded = _selectedElement != ElementType.NoElement;
        RefreshFilterState();
        RefreshSlotVisibility();
    }

    private void RefreshFilterState()
    {
        if (_uiPanel == null)
            return;

        Sprite textureSprite = ResolveTextureSprite(_uiPanel.GetComponent<Image>());
        StyleTab(_normalTab, !_showUpgraded, new Color(0.04f, 0.72f, 0.86f, 1f), textureSprite);
        StyleTab(_upgradedTab, _showUpgraded, BlockyUITheme.BlueHeader, textureSprite);

        for (int i = 0; i < _elementButtons.Count; i++)
        {
            ElementFilterButton binding = _elementButtons[i];
            if (binding == null || binding.button == null)
                continue;

            bool unlocked = IsElementUnlocked(binding.element);
            bool selected = binding.element == (_showUpgraded ? _selectedElement : ElementType.NoElement);
            Image background = binding.button.targetGraphic as Image;
            StyleImage(
                background,
                selected ? Color.Lerp(GetElementButtonColor(binding.element), Color.white, 0.24f) : GetElementButtonColor(binding.element),
                textureSprite,
                true);
            if (binding.icon != null)
                binding.icon.color = unlocked ? Color.white : new Color(0.26f, 0.26f, 0.26f, 0.78f);
            if (binding.lockIcon != null)
            {
                binding.lockIcon.gameObject.SetActive(!unlocked);
                binding.lockIcon.transform.SetAsLastSibling();
            }
            if (binding.outline != null)
            {
                binding.outline.effectColor = selected ? Color.white : BlockyUITheme.BlackStroke;
                binding.outline.effectDistance = selected ? new Vector2(4f, -4f) : new Vector2(2f, -2f);
                binding.outline.useGraphicAlpha = true;
            }
        }
    }

    private static void StyleTab(Button button, bool selected, Color baseColor, Sprite textureSprite)
    {
        if (button == null)
            return;

        Image image = button.targetGraphic as Image;
        StyleImage(image, selected ? Color.Lerp(baseColor, Color.white, 0.16f) : Color.Lerp(baseColor, Color.black, 0.24f), textureSprite, true);
        Outline outline = button.GetComponent<Outline>();
        if (outline != null)
        {
            outline.effectColor = selected ? Color.white : BlockyUITheme.BlackStroke;
            outline.effectDistance = selected ? new Vector2(4f, -4f) : new Vector2(2f, -2f);
        }
    }

    private void RefreshSlotVisibility()
    {
        for (int i = 0; i < _slots.Count; i++)
        {
            BigPetSetSlot slot = _slots[i];
            if (slot == null)
                continue;

            bool elementMatches = _showUpgraded
                ? slot.Element == _selectedElement && slot.Element != ElementType.NoElement
                : slot.Element == ElementType.NoElement;
            slot.gameObject.SetActive(slot.VariantIndex <= _maxAvailableVariantIndex && elementMatches);
        }
    }

    private bool IsElementUnlocked(ElementType element)
    {
        if (element == ElementType.NoElement)
            return true;
        if (_basePetCount <= 0)
            return false;

        int elementIndex = System.Array.IndexOf(ProgressionElements, element);
        if (elementIndex < 0)
            return false;
        return _maxAvailableVariantIndex >= elementIndex * _basePetCount;
    }

    private ElementType FindBestUnlockedElement()
    {
        for (int i = ProgressionElements.Length - 1; i >= 1; i--)
        {
            if (IsElementUnlocked(ProgressionElements[i]))
                return ProgressionElements[i];
        }

        return ElementType.Gold;
    }

    private Sprite ResolveElementIcon(ElementType element)
    {
        return element switch
        {
            ElementType.Gold => _goldElementIcon,
            ElementType.Diamond => _diamondElementIcon,
            ElementType.Electric => _electricElementIcon,
            ElementType.Fire => _fireElementIcon,
            _ => _normalElementIcon
        };
    }

    public static Color GetElementButtonColor(ElementType element)
    {
        return element switch
        {
            ElementType.Gold => new Color(0.94f, 0.66f, 0.04f, 1f),
            ElementType.Diamond => new Color(0.12f, 0.72f, 0.88f, 1f),
            ElementType.Electric => new Color(0.48f, 0.18f, 0.78f, 1f),
            ElementType.Fire => new Color(0.86f, 0.16f, 0.08f, 1f),
            _ => new Color(0.86f, 0.72f, 0.18f, 1f)
        };
    }

    private void ConfigureWindowVisuals()
    {
        if (_remoteMode || _uiPanel == null)
            return;

        var panelRect = _uiPanel.transform as RectTransform;
        if (panelRect != null)
        {
            bool portrait = Screen.height > Screen.width;
            panelRect.anchorMin = portrait ? new Vector2(0.04f, 0.035f) : new Vector2(0.63f, 0.045f);
            panelRect.anchorMax = portrait ? new Vector2(0.96f, 0.965f) : new Vector2(0.975f, 0.955f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = Vector2.zero;
        }

        var panelImage = _uiPanel.GetComponent<Image>();
        Sprite textureSprite = ResolveTextureSprite(panelImage);
        StyleImage(panelImage, BlockyUITheme.BrownBody, textureSprite, true);
        EnsureOutline(_uiPanel, new Vector2(5f, -5f), BlockyUITheme.BlackStroke);
        EnsureShadow(_uiPanel, new Vector2(0f, -5f), new Color(0f, 0f, 0f, 0.42f));

        Transform body = _uiPanel.transform.Find("Body") ?? _uiPanel.transform.Find("Image");
        if (body != null)
        {
            body.name = "Body";
            SetRect(body as RectTransform, new Vector2(0.02f, 0.025f), new Vector2(0.98f, 0.865f));
            StyleImage(body.GetComponent<Image>(), BlockyUITheme.DarkBrownPanel, textureSprite, false);
            body.SetAsFirstSibling();
        }

        Transform header = _uiPanel.transform.Find("Header") ?? _uiPanel.transform.Find("Image (1)");
        if (header != null)
        {
            header.name = "Header";
            SetRect(header as RectTransform, new Vector2(0f, 0.87f), Vector2.one);
            StyleImage(header.GetComponent<Image>(), BlockyUITheme.GreenHeader, textureSprite, false);
            EnsureOutline(header.gameObject, new Vector2(4f, -4f), BlockyUITheme.BlackStroke);
            header.SetSiblingIndex(Mathf.Min(1, header.parent.childCount - 1));
        }

        Transform title = _uiPanel.transform.Find("Title") ?? _uiPanel.transform.Find("Text (Legacy)");
        if (title != null)
        {
            title.name = "Title";
            SetRect(title as RectTransform, new Vector2(0.025f, 0.888f), new Vector2(0.29f, 0.978f));
            _titleText = title.GetComponent<TMP_Text>();
            if (_titleText != null)
            {
                TmpUiTextFactory.ApplyDefaults(_titleText);
                _titleText.fontStyle = FontStyles.Bold;
                _titleText.fontSize = 32f;
                _titleText.enableAutoSizing = true;
                _titleText.fontSizeMin = 16f;
                _titleText.fontSizeMax = 34f;
                _titleText.alignment = TextAlignmentOptions.Center;
                _titleText.color = BlockyUITheme.YellowAccent;
                _titleText.outlineColor = BlockyUITheme.BlackStroke;
                _titleText.outlineWidth = Mathf.Max(_titleText.outlineWidth, 0.16f);
                _titleText.raycastTarget = false;
            }
            title.SetAsLastSibling();
        }

        _scrollRect = _uiPanel.GetComponentInChildren<ScrollRect>(true);
        if (_scrollRect != null)
        {
            SetRect(_scrollRect.transform as RectTransform, new Vector2(0.04f, 0.045f), new Vector2(0.96f, 0.605f));
            StyleImage(
                _scrollRect.GetComponent<Image>(),
                new Color(BlockyUITheme.DarkBrownPanel.r, BlockyUITheme.DarkBrownPanel.g, BlockyUITheme.DarkBrownPanel.b, 0.96f),
                textureSprite,
                false);
            EnsureOutline(_scrollRect.gameObject, new Vector2(3f, -3f), BlockyUITheme.BlackStroke);
            _scrollRect.horizontal = false;
            _scrollRect.vertical = true;
        }

        Button closeButton = FindDirectChildButton(_uiPanel.transform);
        if (closeButton != null)
        {
            closeButton.gameObject.name = "CloseButton";
            SetRect(closeButton.transform as RectTransform, new Vector2(0.835f, 0.883f), new Vector2(0.975f, 0.985f));
            var closeImage = closeButton.GetComponent<Image>();
            if (closeImage == null)
                closeImage = closeButton.gameObject.AddComponent<Image>();
            closeImage.enabled = true;
            StyleImage(closeImage, BlockyUITheme.RedHeader, textureSprite, true);
            EnsureOutline(closeButton.gameObject, new Vector2(4f, -4f), BlockyUITheme.BlackStroke);

            var closeVisual = closeButton.transform.Find("Image")?.GetComponent<Image>();
            if (closeVisual != null)
            {
                StyleImage(closeVisual, BlockyUITheme.RedHeader, textureSprite, true);
                EnsureOutline(closeVisual.gameObject, new Vector2(3f, -3f), BlockyUITheme.BlackStroke);
                closeButton.targetGraphic = closeVisual;
            }
            else
            {
                closeButton.targetGraphic = closeImage;
            }

            closeButton.transition = Selectable.Transition.ColorTint;
            closeButton.navigation = new Navigation { mode = Navigation.Mode.None };
            closeButton.transform.SetAsLastSibling();
        }

        EnsureFilterControls();
        EnsureIncomeBonusBadge();
        RefreshFilterState();
        RefreshLocalizedText();
    }

    private void RefreshLocalizedText()
    {
        if (_titleText != null)
            _titleText.text = LocalizationUtils.T("UI/BigPet/Header", "BIG");
        if (_normalTabText != null)
            _normalTabText.text = LocalizationUtils.T("UI/BigPet/Normal", "Normal");
        if (_upgradedTabText != null)
            _upgradedTabText.text = LocalizationUtils.T("UI/BigPet/Upgraded", "Upgraded");

        for (int i = 0; i < _slots.Count; i++)
        {
            if (_slots[i] != null)
                _slots[i].RefreshLocalizedText();
        }
    }

    private void EnsureIncomeBonusBadge()
    {
        if (_remoteMode || _uiPanel == null)
            return;

        Transform existing = _uiPanel.transform.Find("IncomeBonusBadge");
        if (existing != null)
        {
            _incomeBonusText = existing.GetComponentInChildren<TMP_Text>(true);
            ConfigureIncomeBonusBadge(existing.gameObject);
            return;
        }

        var badgeObject = new GameObject(
            "IncomeBonusBadge",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Outline),
            typeof(Shadow));
        badgeObject.transform.SetParent(_uiPanel.transform, false);
        badgeObject.transform.SetAsLastSibling();

        ConfigureIncomeBonusBadge(badgeObject);

        var textObject = new GameObject("Value", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(badgeObject.transform, false);
        var textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(10f, 4f);
        textRect.offsetMax = new Vector2(-10f, -4f);

        _incomeBonusText = textObject.GetComponent<TMP_Text>();
        TmpUiTextFactory.ApplyDefaults(_incomeBonusText);
        TMP_Text fontSource = _uiPanel.GetComponentInChildren<TMP_Text>(true);
        if (fontSource != null && fontSource.font != null)
            _incomeBonusText.font = fontSource.font;
        _incomeBonusText.fontSize = 44;
        _incomeBonusText.fontStyle = FontStyles.Bold;
        _incomeBonusText.alignment = TextAlignmentOptions.Center;
        _incomeBonusText.color = BlockyUITheme.YellowAccent;
        _incomeBonusText.raycastTarget = false;
        _incomeBonusText.enableAutoSizing = true;
        _incomeBonusText.fontSizeMin = 22;
        _incomeBonusText.fontSizeMax = 48;
        _incomeBonusText.outlineColor = BlockyUITheme.BlackStroke;
        _incomeBonusText.outlineWidth = 0.16f;
    }

    private void ConfigureIncomeBonusBadge(GameObject badgeObject)
    {
        if (badgeObject == null || _uiPanel == null)
            return;

        SetRect(
            badgeObject.transform as RectTransform,
            new Vector2(0.31f, 0.89f),
            new Vector2(0.81f, 0.978f));
        badgeObject.transform.SetAsLastSibling();

        var panelImage = _uiPanel.GetComponent<Image>();
        var badgeImage = badgeObject.GetComponent<Image>();
        StyleImage(
            badgeImage,
            new Color(0.10f, 0.055f, 0.015f, 0.97f),
            ResolveTextureSprite(panelImage),
            false);
        EnsureOutline(badgeObject, new Vector2(4f, -4f), BlockyUITheme.BlackStroke);
        EnsureShadow(badgeObject, new Vector2(0f, -4f), new Color(0f, 0f, 0f, 0.4f));
    }

    private static Sprite ResolveTextureSprite(Image source)
    {
        if (source != null && source.sprite != null)
            return source.sprite;
        return null;
    }

    private static void StyleImage(Image image, Color color, Sprite sprite, bool raycastTarget)
    {
        if (image == null)
            return;

        if (sprite != null)
            image.sprite = sprite;
        image.type = image.sprite != null ? Image.Type.Tiled : Image.Type.Simple;
        image.pixelsPerUnitMultiplier = 1f;
        image.color = color;
        image.raycastTarget = raycastTarget;
    }

    private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
    {
        if (rect == null)
            return;

        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
    }

    private static Button FindDirectChildButton(Transform root)
    {
        if (root == null)
            return null;

        for (int i = 0; i < root.childCount; i++)
        {
            var button = root.GetChild(i).GetComponent<Button>();
            if (button != null)
                return button;
        }

        return null;
    }

    private static void EnsureOutline(GameObject target, Vector2 distance, Color color)
    {
        if (target == null)
            return;

        var outline = target.GetComponent<Outline>();
        if (outline == null)
            outline = target.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = distance;
        outline.useGraphicAlpha = true;
    }

    private static void EnsureShadow(GameObject target, Vector2 distance, Color color)
    {
        if (target == null)
            return;

        Shadow shadow = null;
        var shadows = target.GetComponents<Shadow>();
        for (int i = 0; i < shadows.Length; i++)
        {
            if (shadows[i] != null && !(shadows[i] is Outline))
            {
                shadow = shadows[i];
                break;
            }
        }

        if (shadow == null)
            shadow = target.AddComponent<Shadow>();
        shadow.effectColor = color;
        shadow.effectDistance = distance;
        shadow.useGraphicAlpha = true;
    }

    private void RefreshIncomeBonusBadge()
    {
        if (_incomeBonusText == null || _remoteMode)
            return;

        int level = G.Save != null && G.Save.IsReady ? Mathf.Max(1, G.Save.LoadBigPetLvl()) : 1;
        int bonusPercent = level * 10;
        _incomeBonusText.text = LocalizationUtils.Format(
            "UI/Income/BigPetBadge",
            "Farm income: +{0}%",
            bonusPercent);
    }
}
