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

    private List<BigPetSetSlot> _slots = new();
    private Collider _interactionCollider;
    private float _interactionOpenDistance = 5f;
    private float _nextDistanceCheckTime;
    private TMP_Text _incomeBonusText;
    private TMP_Text _titleText;
    private ScrollRect _scrollRect;
    private LocalizationManager _subscribedLocalizationManager;

    [HideInInspector]
    public UnityEvent<Brainrot> PetSlotClicked;
    [HideInInspector]
    public UnityEvent<Brainrot> ActiveChanged;

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
        float closeDistance = Mathf.Max(0.25f, _interactionOpenDistance + _closeDistanceBuffer);
        if ((playerPosition - closestPoint).sqrMagnitude > closeDistance * closeDistance)
            OpenUI(false);
    }

    public void InitUI(IReadOnlyList<Brainrot> petList)
    {
        ClearSlots();

        if (petList == null || _slotParent == null || _slotPrefab == null)
            return;

        foreach (var pet in petList)
        {
            if (pet == null)
                continue;

            BigPetSetSlot slot = Instantiate(_slotPrefab, _slotParent);
            slot.Init(this, pet);
            _slots.Add(slot);
        }

        EnsureIncomeBonusBadge();
        RefreshIncomeBonusBadge();
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
        for (int i = 0; i < _slots.Count; i++)
        {
            if (_slots[i] != null)
                _slots[i].gameObject.SetActive(i <= idx);
        }

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
            bool portrait = Screen.height > Screen.width;
            int columns = portrait
                ? width >= 720f ? 3 : 2
                : width >= 560f ? 4 : width >= 390f ? 3 : 2;
            grid.ConfigureFitItemCount(
                0,
                new Vector2(12f, 12f),
                new RectOffset(12, 12, 12, 12),
                new Vector2(92f, 92f),
                new Vector2(158f, 158f),
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


    public void OnPetClicked(Brainrot pet)
    {
        if (_remoteMode || pet == null)
            return;

        PetSlotClicked?.Invoke(pet);
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
        _interactionOpenDistance = Mathf.Max(0.25f, openDistance);
    }

    public void ChangeActivePet(Brainrot pet)
    {
        ActiveChanged?.Invoke(pet);
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

    private void ConfigureWindowVisuals()
    {
        if (_remoteMode || _uiPanel == null)
            return;

        var panelRect = _uiPanel.transform as RectTransform;
        if (panelRect != null)
        {
            bool portrait = Screen.height > Screen.width;
            panelRect.anchorMin = portrait ? new Vector2(0.04f, 0.08f) : new Vector2(0.18f, 0.10f);
            panelRect.anchorMax = portrait ? new Vector2(0.96f, 0.92f) : new Vector2(0.82f, 0.90f);
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
            SetRect(body as RectTransform, new Vector2(0.02f, 0.04f), new Vector2(0.98f, 0.83f));
            StyleImage(body.GetComponent<Image>(), BlockyUITheme.DarkBrownPanel, textureSprite, false);
            body.SetAsFirstSibling();
        }

        Transform header = _uiPanel.transform.Find("Header") ?? _uiPanel.transform.Find("Image (1)");
        if (header != null)
        {
            header.name = "Header";
            SetRect(header as RectTransform, new Vector2(0f, 0.84f), Vector2.one);
            StyleImage(header.GetComponent<Image>(), BlockyUITheme.BlueHeader, textureSprite, false);
            EnsureOutline(header.gameObject, new Vector2(4f, -4f), BlockyUITheme.BlackStroke);
            header.SetSiblingIndex(Mathf.Min(1, header.parent.childCount - 1));
        }

        Transform title = _uiPanel.transform.Find("Title") ?? _uiPanel.transform.Find("Text (Legacy)");
        if (title != null)
        {
            title.name = "Title";
            SetRect(title as RectTransform, new Vector2(0.04f, 0.855f), new Vector2(0.82f, 0.985f));
            _titleText = title.GetComponent<TMP_Text>();
            if (_titleText != null)
            {
                TmpUiTextFactory.ApplyDefaults(_titleText);
                _titleText.fontStyle = FontStyles.Bold;
                _titleText.fontSize = 52f;
                _titleText.enableAutoSizing = true;
                _titleText.fontSizeMin = 28f;
                _titleText.fontSizeMax = 56f;
                _titleText.alignment = TextAlignmentOptions.MidlineLeft;
                _titleText.color = Color.white;
                _titleText.outlineColor = BlockyUITheme.BlackStroke;
                _titleText.outlineWidth = Mathf.Max(_titleText.outlineWidth, 0.16f);
                _titleText.raycastTarget = false;
            }
            title.SetAsLastSibling();
        }

        _scrollRect = _uiPanel.GetComponentInChildren<ScrollRect>(true);
        if (_scrollRect != null)
        {
            SetRect(_scrollRect.transform as RectTransform, new Vector2(0.04f, 0.06f), new Vector2(0.96f, 0.70f));
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
            SetRect(closeButton.transform as RectTransform, new Vector2(0.885f, 0.855f), new Vector2(0.98f, 0.985f));
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

        EnsureIncomeBonusBadge();
        RefreshLocalizedText();
    }

    private void RefreshLocalizedText()
    {
        if (_titleText != null)
            _titleText.text = LocalizationUtils.T("UI/BigPet/ChoosePet", "Choose a big pet");

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
            new Vector2(0.16f, 0.72f),
            new Vector2(0.84f, 0.815f));
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
