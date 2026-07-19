using System.Collections.Generic;
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
    private Text _incomeBonusText;
    private LocalizationManager _subscribedLocalizationManager;

    [HideInInspector]
    public UnityEvent<Brainrot> PetSlotClicked;
    [HideInInspector]
    public UnityEvent<Brainrot> ActiveChanged;

    public bool IsOpen => _uiPanel != null && _uiPanel.activeSelf;

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
            grid.Rebuild();

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
    }

    private void EnsureIncomeBonusBadge()
    {
        if (_remoteMode || _uiPanel == null)
            return;

        Transform existing = _uiPanel.transform.Find("IncomeBonusBadge");
        if (existing != null)
        {
            _incomeBonusText = existing.GetComponentInChildren<Text>(true);
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

        var badgeRect = badgeObject.GetComponent<RectTransform>();
        badgeRect.anchorMin = new Vector2(0.29f, 0.89f);
        badgeRect.anchorMax = new Vector2(0.82f, 0.985f);
        badgeRect.offsetMin = Vector2.zero;
        badgeRect.offsetMax = Vector2.zero;

        var panelImage = _uiPanel.GetComponent<Image>();
        var badgeImage = badgeObject.GetComponent<Image>();
        if (panelImage != null)
            badgeImage.sprite = panelImage.sprite;
        badgeImage.type = badgeImage.sprite != null ? Image.Type.Tiled : Image.Type.Simple;
        badgeImage.color = new Color(0.11f, 0.075f, 0.02f, 0.96f);
        badgeImage.raycastTarget = false;

        var outline = badgeObject.GetComponent<Outline>();
        outline.effectColor = BlockyUITheme.BlackStroke;
        outline.effectDistance = new Vector2(4f, -4f);
        outline.useGraphicAlpha = true;

        Shadow shadow = null;
        var shadows = badgeObject.GetComponents<Shadow>();
        for (int i = 0; i < shadows.Length; i++)
        {
            if (shadows[i] != null && !(shadows[i] is Outline))
            {
                shadow = shadows[i];
                break;
            }
        }
        if (shadow == null)
            shadow = badgeObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.4f);
        shadow.effectDistance = new Vector2(0f, -4f);
        shadow.useGraphicAlpha = true;

        var textObject = new GameObject("Value", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline));
        textObject.transform.SetParent(badgeObject.transform, false);
        var textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(10f, 4f);
        textRect.offsetMax = new Vector2(-10f, -4f);

        _incomeBonusText = textObject.GetComponent<Text>();
        Text fontSource = _uiPanel.GetComponentInChildren<Text>(true);
        _incomeBonusText.font = fontSource != null ? fontSource.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _incomeBonusText.fontSize = 44;
        _incomeBonusText.fontStyle = FontStyle.Bold;
        _incomeBonusText.alignment = TextAnchor.MiddleCenter;
        _incomeBonusText.color = BlockyUITheme.YellowAccent;
        _incomeBonusText.raycastTarget = false;
        _incomeBonusText.resizeTextForBestFit = true;
        _incomeBonusText.resizeTextMinSize = 22;
        _incomeBonusText.resizeTextMaxSize = 48;

        var textOutline = textObject.GetComponent<Outline>();
        textOutline.effectColor = BlockyUITheme.BlackStroke;
        textOutline.effectDistance = new Vector2(3f, -3f);
        textOutline.useGraphicAlpha = true;
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
