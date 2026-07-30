using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ClaimAllCoinsZone : MonoBehaviour
{
    private const string LegacyUnlockSaveKey = "ClaimAllNoAdsUnlocked";
    private const string UnlockSaveKeyPrefix = "ClaimAllNoAdsUnlocked.";

    [Header("Auto Setup")]
    [SerializeField] private bool autoDiscoverReferences = true;
    [SerializeField] private bool autoDiscoverPopup = true;

    [Header("Zone Identity")]
    [SerializeField] private string zoneId = "zone_1";
    [SerializeField] private bool fallbackToLegacyUnlockKey = true;
    [SerializeField] private int slotIndexOverride = -1;

    [Header("Scene Refs")]
    [SerializeField] private InteractionPanel interactionPanel;
    [SerializeField] private UniversalDecisionPopup decisionPopup;
    [SerializeField] private UniversalDecisionPopup decisionPopupPrefab;
    [SerializeField] private Transform popupRuntimeParent;
    [SerializeField] private GameObject readyIndicator;
    [SerializeField] private AudioSource collectAudio;
    [SerializeField] private RemoteBasesApplier remoteBases;
    [SerializeField] private Collider triggerCollider;
    [SerializeField] private GameObject zoneVisualRoot;

    [Header("Income Sources")]
    [SerializeField] private bool autoDiscoverIncomeSources = true;
    [SerializeField] private Transform incomeSourcesRoot;
    [SerializeField] private List<FieldCell> manualIncomeCells = new();
    [SerializeField] private List<BigPetPoint> manualBigPetPoints = new();

    [Header("Popup Search")]
    [SerializeField] private Transform popupSearchRoot;
    [SerializeField] private bool allowGlobalPopupFallback = true;

    [Header("Claim Settings")]
    [SerializeField] private string rewardedAdId = "ClaimAllCoins";
    [SerializeField] private double rewardedMultiplier = 2d;
    [SerializeField] private bool includeBigPetIncome = true;
    [SerializeField] private float stateRefreshSec = 0.5f;
    [SerializeField] private bool showIndicatorOnlyWhenIncomeAvailable = true;
    [SerializeField] private bool onlyForLocalSlot = true;
    [SerializeField] private bool allowWhenSlotUnknown = true;
    [SerializeField] private bool hideZoneWhenNotLocal = true;
    [SerializeField] private bool disableTriggerWhenNotLocal = true;
    [SerializeField] private float slotResolutionRefreshSec = 0.5f;
    [SerializeField] private bool openPopupOnEnter = true;
    [SerializeField] private bool hidePopupOnExit = true;

    [Header("Permanent Unlock")]
    [SerializeField] private CurrencyType unlockPriceCurrency = CurrencyType.Gems;
    [SerializeField] private double unlockPrice = 499d;
    [SerializeField] private bool autoCollectWhenUnlocked = true;
    [SerializeField] private bool collectImmediatelyOnEnterAfterUnlock = true;
    [SerializeField] private float autoCollectIntervalSec = 1f;

    [Header("Localization")]
    [SerializeField] private string interactionLocalizationKey = "UI/ClaimAll/OpenPopup";
    [SerializeField] private string interactionText = "Claim all";
    [SerializeField] private string popupTitleLocalizationKey = "UI/ClaimAll/PopupTitle";
    [SerializeField] private string popupTitleText = "Collect income";
    [SerializeField] private string popupDescriptionLocalizationKey = "UI/ClaimAll/PopupDescription";
    [SerializeField] private string popupDescriptionText = "Collect income from every pet at once.";
    [SerializeField] private string popupCollectX2LocalizationKey = "UI/ClaimAll/PopupCollectX2Ad";
    [SerializeField] private string popupCollectX2Text = "Collect x2";
    [SerializeField] private string popupBuyForeverLocalizationKey = "UI/ClaimAll/PopupBuyForever";
    [SerializeField] private string popupBuyForeverText = "Auto-collect forever";

    [Header("Popup Presentation")]
    [SerializeField] private bool applyClaimPopupPresentation = true;
    [SerializeField] private Vector2 claimPopupAnchorMin = new Vector2(0.24f, 0.21f);
    [SerializeField] private Vector2 claimPopupAnchorMax = new Vector2(0.76f, 0.77f);
    [SerializeField] private Vector2 claimPopupPosition = Vector2.zero;
    [SerializeField] private Vector2 claimPopupPadding = new Vector2(28f, 24f);
    [SerializeField] private Color claimPopupPanelColor = new Color(0.35f, 0.18f, 0.07f, 0.98f);
    [SerializeField] private Color claimPopupConfirmColor = new Color(0.05f, 0.45f, 0.92f, 1f);
    [SerializeField] private Color claimPopupCancelColor = new Color(0.18f, 0.82f, 0.08f, 1f);
    [SerializeField] private Color claimPopupTextColor = Color.white;
    [SerializeField] private Sprite claimPopupTextureSprite;
    [SerializeField] private Sprite claimPopupGradientSprite;
    [SerializeField] private float claimPopupTitleFontSize = 40f;
    [SerializeField] private float claimPopupDescriptionFontSize = 25f;
    [SerializeField] private float claimPopupButtonFontSize = 25f;

    [Header("Debug")]
    [SerializeField] private bool debugLogs;

    private bool _playerInside;
    private bool _actionInFlight;
    private bool _permanentUnlocked;
    private Coroutine _stateRoutine;
    private Coroutine _autoCollectRoutine;
    private string _resolvedSaveKey;
    private bool _isAvailableForSlot = true;
    private int _resolvedSlotIndex = -1;
    private float _nextSlotRefreshAt;
    private readonly List<FieldCell> _cachedIncomeCells = new();
    private readonly List<BigPetPoint> _cachedBigPetPoints = new();
    private bool _incomeSourcesCached;
    private UniversalDecisionPopup _activePopup;
    private bool _popupOpenedByZone;
    private Canvas _runtimePopupCanvas;

    private void Awake()
    {
        AutoSetupReferences();
        EnsureIncomeSourcesCache(forceRebuild: true);
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
            return;

        AutoSetupReferences();
        EnsureIncomeSourcesCache(forceRebuild: true);
    }

    private void Start()
    {
        _resolvedSaveKey = ResolveUnlockSaveKey();
        _permanentUnlocked = LoadPermanentUnlocked();
        EnsureIncomeSourcesCache(forceRebuild: true);
        RefreshSlotAvailability(forceApply: true);
        RefreshVisualState();
    }

    private void OnEnable()
    {
        SubscribeEvents();
        EnsureIncomeSourcesCache();
        RefreshSlotAvailability(forceApply: true);
        RefreshVisualState();
    }

    private void OnDisable()
    {
        UnsubscribeEvents();
        StopStateRoutine();
        StopAutoCollectRoutine();
        HideInteraction();
    }

    private void Update()
    {
        if (!onlyForLocalSlot)
            return;

        var now = Time.unscaledTime;
        if (now < _nextSlotRefreshAt)
            return;

        _nextSlotRefreshAt = now + Mathf.Max(0.1f, slotResolutionRefreshSec);
        var wasAvailable = _isAvailableForSlot;
        RefreshSlotAvailability();
        if (wasAvailable != _isAvailableForSlot)
            RefreshVisualState();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;
        if (!RefreshSlotAvailability())
            return;

        _playerInside = true;
        StartStateRoutine();
        if (HasCollectibleIncome())
            G.Sound?.Play(GameAudioId.SFX_UI_READY);
        if (_permanentUnlocked)
        {
            StartAutoCollectRoutine();
            if (collectImmediatelyOnEnterAfterUnlock)
                TryCollectWithoutAd("enter");
        }
        else if (openPopupOnEnter)
        {
            ShowOfferPopup();
        }
        RefreshVisualState();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        _playerInside = false;
        StopStateRoutine();
        StopAutoCollectRoutine();
        HidePopupIfOpenedByZone();
        HideInteraction();
        RefreshVisualState();
    }

    private void SubscribeEvents()
    {
        if (interactionPanel != null)
            interactionPanel.InteractionComplete.AddListener(OnInteractionRequested);
    }

    private void UnsubscribeEvents()
    {
        if (interactionPanel != null)
            interactionPanel.InteractionComplete.RemoveListener(OnInteractionRequested);
    }

    private void StartStateRoutine()
    {
        if (_stateRoutine != null)
            return;

        _stateRoutine = StartCoroutine(StateRefreshRoutine());
    }

    private void StopStateRoutine()
    {
        if (_stateRoutine == null)
            return;

        StopCoroutine(_stateRoutine);
        _stateRoutine = null;
    }

    private void StartAutoCollectRoutine()
    {
        if (_autoCollectRoutine != null || !_permanentUnlocked || !autoCollectWhenUnlocked)
            return;

        _autoCollectRoutine = StartCoroutine(AutoCollectRoutine());
    }

    private void StopAutoCollectRoutine()
    {
        if (_autoCollectRoutine == null)
            return;

        StopCoroutine(_autoCollectRoutine);
        _autoCollectRoutine = null;
    }

    [ContextMenu("ClaimAll/Auto Setup References")]
    private void AutoSetupReferences()
    {
        if (!autoDiscoverReferences)
            return;

        if (interactionPanel == null)
            interactionPanel = GetComponentInChildren<InteractionPanel>(true);

        if (collectAudio == null)
        {
            collectAudio = GetComponent<AudioSource>();
            if (collectAudio == null)
                collectAudio = GetComponentInChildren<AudioSource>(true);
        }

        if (readyIndicator == null)
        {
            var marker = FindChildByNameToken(transform, "ready", "income", "indicator");
            if (marker != null)
                readyIndicator = marker.gameObject;
        }

        if (remoteBases == null)
            remoteBases = GetComponentInParent<RemoteBasesApplier>();

        if (triggerCollider == null)
            triggerCollider = GetComponent<Collider>();

        if (zoneVisualRoot == null)
        {
            var visual = FindChildByNameToken(transform, "visual", "mesh", "model");
            if (visual != null)
                zoneVisualRoot = visual.gameObject;
        }

        if (autoDiscoverIncomeSources && incomeSourcesRoot == null)
            incomeSourcesRoot = transform.root;

        if (autoDiscoverPopup && popupSearchRoot == null)
        {
            var canvas = GetComponentInParent<Canvas>(true);
            popupSearchRoot = canvas != null ? canvas.transform : transform.root;
        }

        _incomeSourcesCached = false;
    }

    [ContextMenu("ClaimAll/Rebuild Income Sources Cache")]
    private void RebuildIncomeSourcesCache()
    {
        EnsureIncomeSourcesCache(forceRebuild: true);
    }

    private IEnumerator StateRefreshRoutine()
    {
        var wait = new WaitForSecondsRealtime(Mathf.Max(0.2f, stateRefreshSec));
        while (_playerInside)
        {
            RefreshVisualState();
            yield return wait;
        }

        _stateRoutine = null;
    }

    private IEnumerator AutoCollectRoutine()
    {
        var wait = new WaitForSecondsRealtime(Mathf.Max(0.2f, autoCollectIntervalSec));
        while (_playerInside && _permanentUnlocked && autoCollectWhenUnlocked)
        {
            if (!_actionInFlight)
                TryCollectWithoutAd("auto");
            yield return wait;
        }

        _autoCollectRoutine = null;
    }

    private void OnInteractionRequested()
    {
        if (_actionInFlight || !_playerInside || !RefreshSlotAvailability())
            return;

        if (_permanentUnlocked)
        {
            TryCollectWithoutAd("manual_unlocked");
            return;
        }

        ShowOfferPopup();
    }

    private void ShowOfferPopup()
    {
        var popup = ResolveDecisionPopup();
        if (popup == null)
        {
            if (debugLogs)
                Debug.LogWarning("[ClaimAll] UniversalDecisionPopup was not found. Fallback to ad claim flow.");
            StartCoroutine(ClaimWithAdX2Flow());
            return;
        }

        ApplyPopupPresentation(popup);

        if (popup.IsOpen)
            return;

        var request = new UniversalDecisionPopup.Request
        {
            title = new UniversalDecisionPopup.LocalizedTextPayload(popupTitleLocalizationKey, popupTitleText),
            description = new UniversalDecisionPopup.LocalizedTextPayload(
                popupDescriptionLocalizationKey,
                popupDescriptionText),
            confirm = new UniversalDecisionPopup.LocalizedTextPayload(popupCollectX2LocalizationKey, popupCollectX2Text),
            cancel = new UniversalDecisionPopup.LocalizedTextPayload(
                popupBuyForeverLocalizationKey,
                popupBuyForeverText),
            onConfirm = () =>
            {
                _activePopup = null;
                _popupOpenedByZone = false;
                if (_actionInFlight || !_playerInside)
                    return;
                StartCoroutine(ClaimWithAdX2Flow());
            },
            onCancel = () =>
            {
                if (_actionInFlight || !_playerInside)
                    return;

                if (!TryUnlockForever())
                    return;

                popup.Hide();
                _activePopup = null;
                _popupOpenedByZone = false;
                if (collectImmediatelyOnEnterAfterUnlock)
                    TryCollectWithoutAd("unlock");
                StartAutoCollectRoutine();
                RefreshVisualState();
            },
            closeOnConfirm = true,
            closeOnCancel = false,
            closeButtonActsAsCancel = false
        };

        _activePopup = popup;
        _popupOpenedByZone = true;
        popup.Show(request);
    }

    private IEnumerator ClaimWithAdX2Flow()
    {
        _actionInFlight = true;
        RefreshVisualState();

        if (!RefreshSlotAvailability())
        {
            _actionInFlight = false;
            RefreshVisualState();
            yield break;
        }

        if (!HasCollectibleIncome())
        {
            _actionInFlight = false;
            RefreshVisualState();
            yield break;
        }

        if (G.Ad == null)
        {
            if (debugLogs) Debug.LogWarning("[ClaimAll] Ads manager is missing.");
            _actionInFlight = false;
            RefreshVisualState();
            yield break;
        }

        var done = false;
        var rewarded = false;
        G.Ad.ShowRewardedAd(rewardedAdId, success =>
        {
            rewarded = success;
            done = true;
        });

        while (!done)
            yield return null;

        if (!rewarded)
        {
            if (debugLogs) Debug.Log("[ClaimAll] Rewarded ad was skipped/failed.");
            _actionInFlight = false;
            RefreshVisualState();
            yield break;
        }

        CollectAllIncomeWithBonus(Math.Max(1d, rewardedMultiplier), "ad_x2");
        _actionInFlight = false;
        RefreshVisualState();
    }

    private bool TryUnlockForever()
    {
        if (_permanentUnlocked)
            return true;

        if (unlockPrice <= 0d)
        {
            _permanentUnlocked = true;
            PersistPermanentUnlocked();
            G.Sound?.Play(GameAudioId.SFX_UNLOCK_MAJOR);
            TrackPermanentUnlock("free", 0d);
            return true;
        }

        if (G.Currency == null)
            return false;

        if (!G.Currency.RemoveCurrency(unlockPriceCurrency, unlockPrice))
            return false;

        _permanentUnlocked = true;
        PersistPermanentUnlocked();
        G.Sound?.Play(GameAudioId.SFX_UNLOCK_MAJOR);
        TrackPermanentUnlock(unlockPriceCurrency.ToString().ToLowerInvariant(), unlockPrice);
        if (debugLogs)
            Debug.Log("[ClaimAll] Permanent unlock purchased.");
        return true;
    }

    private void TrackPermanentUnlock(string currencyType, double price)
    {
        GameAnalytics.TrackCritical(AnalyticsEventNames.NoAdsUnlocked, GameAnalytics.Params(
            "unlock_type", "claim_all_forever",
            "zone_id", zoneId,
            "source", "claim_all",
            "currency_type", currencyType,
            "price", price,
            "result", "success"),
            "claim_all_unlock:" + zoneId);
    }

    private void TryCollectWithoutAd(string source)
    {
        if (!_playerInside || _actionInFlight || !RefreshSlotAvailability())
            return;
        if (!HasCollectibleIncome())
            return;

        CollectAllIncomeWithBonus(1d, source);
        RefreshVisualState();
    }

    private double CollectAllIncomeWithBonus(double multiplier, string source)
    {
        var baseCollected = CollectAllIncomeRaw(out int sourceCount, source);
        if (baseCollected <= 0d)
        {
            if (!string.Equals(source, "auto", StringComparison.Ordinal))
                AnalyticsManager.Instance.RecordClaimAll(
                    zoneId, source, 0d, multiplier, 0, "failed", "no_collectible_income");
            return 0d;
        }

        var safeMultiplier = Math.Max(1d, multiplier);
        var bonus = baseCollected * (safeMultiplier - 1d);
        if (bonus > 0d)
            AddBonusCoins(bonus);

        var final = baseCollected + Math.Max(0d, bonus);
        bool played = G.Sound != null && G.Sound.Play(
            safeMultiplier > 1d ? GameAudioId.SFX_CLAIM_ALL_X2 : GameAudioId.SFX_CLAIM_ALL);
        if (G.Sound == null && !played && collectAudio != null)
            collectAudio.Play();

        if (debugLogs)
            Debug.Log($"[ClaimAll] source={source} collected={final.ToString("0", CultureInfo.InvariantCulture)}");

        AnalyticsManager.Instance.RecordClaimAll(
            zoneId, source, final, safeMultiplier, sourceCount);
        return final;
    }

    private void AddBonusCoins(double amount)
    {
        if (amount <= 0d)
            return;

        if (G.Income != null)
        {
            G.Income.TryAddCoins(amount, playAudio: false);
            return;
        }

        if (G.Currency != null)
            G.Currency.AddCurrency(CurrencyType.Coins, amount, playAudio: false);
    }

    private double CollectAllIncomeRaw(out int sourceCount, string collectionMode)
    {
        EnsureIncomeSourcesCache();
        double total = 0d;
        sourceCount = 0;

        using (GameAnalytics.BeginIncomeBatch(collectionMode))
        {
            for (var i = 0; i < _cachedIncomeCells.Count; i++)
            {
                var cell = _cachedIncomeCells[i];
                var brainrot = cell != null ? cell.CurrentBrainrot : null;
                if (brainrot == null)
                    continue;
                double collected = brainrot.CollectIncome(playAudio: false);
                if (collected > 0d)
                    sourceCount++;
                total += collected;
            }

            if (includeBigPetIncome)
            {
                for (var i = 0; i < _cachedBigPetPoints.Count; i++)
                {
                    var bigPet = _cachedBigPetPoints[i];
                    if (bigPet == null)
                        continue;
                    double collected = bigPet.CollectIncome(playAudio: false);
                    if (collected > 0d)
                        sourceCount++;
                    total += collected;
                }
            }
        }

        return Math.Max(0d, total);
    }

    private bool HasCollectibleIncome()
    {
        if (!RefreshSlotAvailability())
            return false;

        EnsureIncomeSourcesCache();

        for (var i = 0; i < _cachedIncomeCells.Count; i++)
        {
            var cell = _cachedIncomeCells[i];
            var brainrot = cell != null ? cell.CurrentBrainrot : null;
            if (brainrot != null && brainrot.HasCollectibleIncome)
                return true;
        }

        if (!includeBigPetIncome)
            return false;

        for (var i = 0; i < _cachedBigPetPoints.Count; i++)
        {
            var bigPet = _cachedBigPetPoints[i];
            if (bigPet != null && bigPet.HasCollectibleIncome)
                return true;
        }

        return false;
    }

    private void EnsureIncomeSourcesCache(bool forceRebuild = false)
    {
        if (_incomeSourcesCached && !forceRebuild)
            return;

        _cachedIncomeCells.Clear();
        _cachedBigPetPoints.Clear();

        var root = incomeSourcesRoot != null ? incomeSourcesRoot : transform.root;
        if (root != null)
        {
            var cells = root.GetComponentsInChildren<FieldCell>(true);
            for (var i = 0; i < cells.Length; i++)
                TryAddIncomeCell(cells[i]);

            var bigPets = root.GetComponentsInChildren<BigPetPoint>(true);
            for (var i = 0; i < bigPets.Length; i++)
                TryAddBigPetPoint(bigPets[i]);
        }

        if (manualIncomeCells != null)
        {
            for (var i = 0; i < manualIncomeCells.Count; i++)
                TryAddIncomeCell(manualIncomeCells[i]);
        }

        if (manualBigPetPoints != null)
        {
            for (var i = 0; i < manualBigPetPoints.Count; i++)
                TryAddBigPetPoint(manualBigPetPoints[i]);
        }

        _incomeSourcesCached = true;
    }

    private void TryAddIncomeCell(FieldCell cell)
    {
        if (cell == null)
            return;
        if (_cachedIncomeCells.Contains(cell))
            return;

        var field = cell.GetComponentInParent<Field>();
        if (field != null && field.IsRemoteMode)
            return;

        _cachedIncomeCells.Add(cell);
    }

    private void TryAddBigPetPoint(BigPetPoint bigPet)
    {
        if (bigPet == null)
            return;
        if (_cachedBigPetPoints.Contains(bigPet))
            return;
        if (bigPet.IsRemoteMode)
            return;

        _cachedBigPetPoints.Add(bigPet);
    }

    private void RefreshVisualState()
    {
        if (!RefreshSlotAvailability())
        {
            HideInteraction();
            if (readyIndicator != null)
                readyIndicator.SetActive(false);
            return;
        }

        var hasIncome = HasCollectibleIncome();
        var showInteraction = !openPopupOnEnter && !_permanentUnlocked && _playerInside && !_actionInFlight;

        if (interactionPanel != null)
        {
            interactionPanel.gameObject.SetActive(showInteraction);
            if (showInteraction)
                interactionPanel.SetInfoLocalized(interactionLocalizationKey, interactionText);
        }

        if (readyIndicator != null)
        {
            if (showIndicatorOnlyWhenIncomeAvailable)
                readyIndicator.SetActive(hasIncome);
            else
                readyIndicator.SetActive(_playerInside);
        }
    }

    private void HideInteraction()
    {
        if (interactionPanel != null)
            interactionPanel.gameObject.SetActive(false);
    }

    private void HidePopupIfOpenedByZone()
    {
        if (!hidePopupOnExit || !_popupOpenedByZone)
            return;

        if (_activePopup != null && _activePopup.IsOpen)
            _activePopup.Hide();

        _activePopup = null;
        _popupOpenedByZone = false;
    }

    private RemoteBasesApplier GetRemoteBases()
    {
        if (remoteBases != null)
            return remoteBases;
        if (!autoDiscoverReferences)
            return null;

        remoteBases = GetComponentInParent<RemoteBasesApplier>();
        if (remoteBases == null)
            remoteBases = FindAnyObjectByType<RemoteBasesApplier>();
        return remoteBases;
    }

    private bool RefreshSlotAvailability(bool forceApply = false)
    {
        var previous = _isAvailableForSlot;
        _isAvailableForSlot = ResolveSlotAvailability();
        if (forceApply || previous != _isAvailableForSlot)
            ApplySlotAvailabilityState();
        return _isAvailableForSlot;
    }

    private bool ResolveSlotAvailability()
    {
        if (!onlyForLocalSlot)
            return true;

        var bases = GetRemoteBases();
        if (bases == null)
            return allowWhenSlotUnknown;

        if (slotIndexOverride >= 0)
            return bases.IsLocalSlotForClient(slotIndexOverride);

        if (_resolvedSlotIndex >= 0)
            return bases.IsLocalSlotForClient(_resolvedSlotIndex);

        if (!bases.TryResolveSlotIndex(transform, out var slotIndex))
            return false;

        _resolvedSlotIndex = slotIndex;
        return bases.IsLocalSlotForClient(slotIndex);
    }

    private void ApplySlotAvailabilityState()
    {
        if (zoneVisualRoot != null && hideZoneWhenNotLocal)
            zoneVisualRoot.SetActive(_isAvailableForSlot);

        if (triggerCollider != null && disableTriggerWhenNotLocal)
            triggerCollider.enabled = _isAvailableForSlot;

        if (_isAvailableForSlot)
            return;

        _playerInside = false;
        StopStateRoutine();
        StopAutoCollectRoutine();
        HideInteraction();

        if (readyIndicator != null)
            readyIndicator.SetActive(false);
    }

    private UniversalDecisionPopup ResolveDecisionPopup()
    {
        if (decisionPopup != null)
            return decisionPopup;

        if (decisionPopupPrefab != null)
        {
            decisionPopup = CreateDecisionPopupInstance();
            return decisionPopup;
        }

        if (!autoDiscoverPopup)
            return null;

        if (popupSearchRoot == null)
        {
            var canvas = GetComponentInParent<Canvas>(true);
            popupSearchRoot = canvas != null ? canvas.transform : transform.root;
        }

        if (popupSearchRoot != null)
            decisionPopup = popupSearchRoot.GetComponentInChildren<UniversalDecisionPopup>(true);

        if (decisionPopup == null && allowGlobalPopupFallback)
        {
            var popups = FindObjectsByType<UniversalDecisionPopup>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (popups != null && popups.Length > 0)
                decisionPopup = popups[0];
        }

        return decisionPopup;
    }

    private UniversalDecisionPopup CreateDecisionPopupInstance()
    {
        var parent = ResolvePopupRuntimeParent();
        var popup = parent != null
            ? Instantiate(decisionPopupPrefab, parent, false)
            : Instantiate(decisionPopupPrefab);

        popup.name = decisionPopupPrefab.name + "_ClaimAllRuntime";

        var rect = popup.transform as RectTransform;
        if (rect != null)
            Stretch(rect, Vector2.zero, Vector2.zero);

        popup.Hide();
        return popup;
    }

    private Transform ResolvePopupRuntimeParent()
    {
        if (popupRuntimeParent != null)
            return popupRuntimeParent;

        return EnsureRuntimePopupCanvas();
    }

    private static Transform FindCanvasUnder(Transform root, bool requireActive)
    {
        if (root == null)
            return null;

        var canvases = root.GetComponentsInChildren<Canvas>(true);
        if (canvases == null)
            return null;

        for (var i = 0; i < canvases.Length; i++)
        {
            if (IsUsablePopupCanvas(canvases[i], requireActive))
                return canvases[i].transform;
        }

        return null;
    }

    private static Transform FindCanvasInParents(Transform root, bool requireActive)
    {
        var current = root;
        while (current != null)
        {
            var canvas = current.GetComponent<Canvas>();
            if (IsUsablePopupCanvas(canvas, requireActive))
                return canvas.transform;
            current = current.parent;
        }

        return null;
    }

    private static Transform FindSceneCanvas(bool requireActive)
    {
        var canvases = Resources.FindObjectsOfTypeAll<Canvas>();
        if (canvases == null)
            return null;

        for (var i = 0; i < canvases.Length; i++)
        {
            if (IsUsablePopupCanvas(canvases[i], requireActive))
                return canvases[i].transform;
        }

        return null;
    }

    private static bool IsUsablePopupCanvas(Canvas canvas, bool requireActive)
    {
        if (canvas == null)
            return false;
        if (canvas.renderMode == RenderMode.WorldSpace)
            return false;
        if (requireActive && !canvas.gameObject.activeInHierarchy)
            return false;
#if UNITY_EDITOR
        if (UnityEditor.EditorUtility.IsPersistent(canvas))
            return false;
#endif
        return canvas.gameObject.scene.IsValid();
    }

    private Transform EnsureRuntimePopupCanvas()
    {
        if (_runtimePopupCanvas != null)
            return _runtimePopupCanvas.transform;

        var canvasObject = new GameObject("ClaimAllPopupCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform.root, false);

        _runtimePopupCanvas = canvasObject.GetComponent<Canvas>();
        _runtimePopupCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _runtimePopupCanvas.overrideSorting = true;
        _runtimePopupCanvas.sortingOrder = 2000;

        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        return _runtimePopupCanvas.transform;
    }

    private void ApplyPopupPresentation(UniversalDecisionPopup popup)
    {
        if (!applyClaimPopupPresentation || popup == null)
            return;

        var popupTransform = popup.transform;
        EnsureDimBackdrop(popupTransform);

        var container = popupTransform.Find("container") as RectTransform;
        if (container != null)
        {
            container.anchorMin = claimPopupAnchorMin;
            container.anchorMax = claimPopupAnchorMax;
            container.pivot = new Vector2(0.5f, 0.5f);
            container.anchoredPosition = claimPopupPosition;
            container.sizeDelta = Vector2.zero;
            container.localScale = Vector3.one;

            var fitter = container.GetComponent<AspectRatioFitter>();
            if (fitter != null)
                fitter.enabled = false;
        }

        var panel = popupTransform.Find("container/panel") as RectTransform;
        if (panel != null)
        {
            Stretch(panel, Vector2.zero, Vector2.zero);
            var fitter = panel.GetComponent<AspectRatioFitter>();
            if (fitter != null)
                fitter.enabled = false;

            var panelImage = panel.GetComponent<Image>();
            if (panelImage != null)
                ApplyClaimPopupSurface(panelImage, claimPopupPanelColor, true);

            EnsureOutline(panel.gameObject, new Vector2(5f, -5f), BlockyUITheme.BlackStroke);
            EnsureShadow(panel.gameObject, new Vector2(0f, -6f), new Color(0f, 0f, 0f, 0.45f));
            var headerSurface = EnsureSurface(
                panel,
                "ClaimAllHeader",
                new Vector2(0f, 0.78f),
                Vector2.one,
                BlockyUITheme.GreenHeader,
                studs: true,
                siblingIndex: 0);
            EnsureGradient(headerSurface);
            EnsureSurface(
                panel,
                "ClaimAllBody",
                new Vector2(0.035f, 0.045f),
                new Vector2(0.965f, 0.735f),
                BlockyUITheme.DarkBrownPanel,
                studs: true,
                siblingIndex: 1);
        }

        var elements = popupTransform.Find("container/panel/Elements") as RectTransform;
        if (elements != null)
        {
            Stretch(elements, claimPopupPadding, -claimPopupPadding);
            elements.SetAsLastSibling();
        }

        var title = popupTransform.Find("container/panel/Elements/Title") as RectTransform;
        if (title != null)
        {
            title.anchorMin = new Vector2(0.02f, 0.79f);
            title.anchorMax = new Vector2(0.88f, 1f);
            title.pivot = new Vector2(0.5f, 0.5f);
            title.anchoredPosition = Vector2.zero;
            title.sizeDelta = Vector2.zero;
            title.localScale = Vector3.one;
        }

        var description = popupTransform.Find("container/panel/Elements/Discription") as RectTransform;
        if (description != null)
        {
            description.anchorMin = new Vector2(0.055f, 0.37f);
            description.anchorMax = new Vector2(0.945f, 0.72f);
            description.pivot = new Vector2(0.5f, 0.5f);
            description.anchoredPosition = Vector2.zero;
            description.sizeDelta = Vector2.zero;
            description.localScale = Vector3.one;
        }

        var buttons = popupTransform.Find("container/panel/Elements/Buttons") as RectTransform;
        if (buttons != null)
        {
            buttons.anchorMin = new Vector2(0.045f, 0.075f);
            buttons.anchorMax = new Vector2(0.955f, 0.315f);
            buttons.pivot = new Vector2(0.5f, 0.5f);
            buttons.anchoredPosition = Vector2.zero;
            buttons.sizeDelta = Vector2.zero;
            buttons.localScale = Vector3.one;
        }

        var confirm = popupTransform.Find("container/panel/Elements/Buttons/Ok") as RectTransform;
        if (confirm != null)
        {
            SetSplitButton(confirm, 0f, 0.48f);
            var button = confirm.GetComponent<Button>();
            if (button != null)
            {
                BlockyUITheme.ApplyButton(button, claimPopupConfirmColor);
                ApplyClaimPopupSurface(button.targetGraphic as Image, claimPopupConfirmColor, true);
                EnsureGradient(confirm);
                AdButtonIconDecorator.SetAdIcon(button, true);
            }
            ConfigureAdButton(confirm);
        }

        var cancel = popupTransform.Find("container/panel/Elements/Buttons/Cancel") as RectTransform;
        if (cancel != null)
        {
            SetSplitButton(cancel, 0.52f, 1f);
            var button = cancel.GetComponent<Button>();
            if (button != null)
            {
                BlockyUITheme.ApplyButton(button, claimPopupCancelColor);
                ApplyClaimPopupSurface(button.targetGraphic as Image, claimPopupCancelColor, true);
                EnsureGradient(cancel);
            }
            ConfigurePermanentButton(cancel);
        }

        var close = popupTransform.Find("container/panel/Button (Legacy)") as RectTransform;
        if (close != null)
        {
            close.anchorMin = new Vector2(1f, 1f);
            close.anchorMax = new Vector2(1f, 1f);
            close.pivot = new Vector2(1f, 1f);
            close.anchoredPosition = new Vector2(-8f, -8f);
            close.sizeDelta = new Vector2(62f, 62f);
            close.localScale = Vector3.one;
            ConfigureCloseButton(close);
        }

        ConfigureText(popupTransform.Find("container/panel/Elements/Title/Text (TMP)"), claimPopupTitleFontSize);
        ConfigureText(popupTransform.Find("container/panel/Elements/Discription/Text (TMP) (1)"), claimPopupDescriptionFontSize);
        ConfigureText(popupTransform.Find("container/panel/Elements/Buttons/Ok/Text (TMP)"), claimPopupButtonFontSize);
        ConfigureText(popupTransform.Find("container/panel/Elements/Buttons/Cancel/Text (TMP)"), claimPopupButtonFontSize);
    }

    private static void EnsureDimBackdrop(Transform popupTransform)
    {
        if (popupTransform == null)
            return;

        var backdrop = popupTransform.Find("ClaimAllDimBackdrop") as RectTransform;
        if (backdrop == null)
        {
            var backdropObject = new GameObject(
                "ClaimAllDimBackdrop",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            backdropObject.transform.SetParent(popupTransform, false);
            backdrop = backdropObject.transform as RectTransform;
        }

        Stretch(backdrop, Vector2.zero, Vector2.zero);
        var image = backdrop.GetComponent<Image>();
        image.sprite = null;
        image.type = Image.Type.Simple;
        image.color = new Color(0.025f, 0.012f, 0.02f, 0.62f);
        image.raycastTarget = true;
        backdrop.SetAsFirstSibling();
    }

    private RectTransform EnsureSurface(
        Transform parent,
        string objectName,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Color color,
        bool studs,
        int siblingIndex)
    {
        if (parent == null)
            return null;

        var rect = parent.Find(objectName) as RectTransform;
        if (rect == null)
        {
            var surfaceObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            surfaceObject.transform.SetParent(parent, false);
            rect = surfaceObject.transform as RectTransform;
        }

        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        rect.localScale = Vector3.one;
        rect.SetSiblingIndex(Mathf.Clamp(siblingIndex, 0, parent.childCount - 1));

        var image = rect.GetComponent<Image>();
        ApplyClaimPopupSurface(image, color, false);
        image.raycastTarget = false;
        EnsureOutline(rect.gameObject, new Vector2(3f, -3f), BlockyUITheme.BlackStroke);
        return rect;
    }

    private void ApplyClaimPopupSurface(Image image, Color color, bool raycastTarget)
    {
        if (image == null)
            return;

        if (claimPopupTextureSprite == null)
        {
            BlockyUITheme.ApplyPanel(image, color, studs: true);
        }
        else
        {
            image.sprite = claimPopupTextureSprite;
            image.type = Image.Type.Tiled;
            image.pixelsPerUnitMultiplier = 1f;
            image.color = color;
        }

        image.raycastTarget = raycastTarget;
    }

    private void EnsureGradient(Transform parent)
    {
        if (parent == null || claimPopupGradientSprite == null)
            return;

        var gradient = parent.Find("ClaimAllGradient") as RectTransform;
        if (gradient == null)
        {
            var gradientObject = new GameObject(
                "ClaimAllGradient",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            gradientObject.transform.SetParent(parent, false);
            gradient = gradientObject.transform as RectTransform;
        }

        Stretch(gradient, Vector2.zero, Vector2.zero);
        var image = gradient.GetComponent<Image>();
        image.sprite = claimPopupGradientSprite;
        image.type = Image.Type.Simple;
        image.color = new Color(1f, 1f, 1f, 0.34f);
        image.raycastTarget = false;
        gradient.SetAsFirstSibling();
    }

    private static void ConfigureAdButton(RectTransform buttonRect)
    {
        if (buttonRect == null)
            return;

        var label = buttonRect.Find("Text (TMP)") as RectTransform;
        if (label != null)
        {
            label.anchorMin = Vector2.zero;
            label.anchorMax = Vector2.one;
            label.offsetMin = new Vector2(18f, 5f);
            label.offsetMax = new Vector2(-62f, -5f);
        }

        var icon = buttonRect.Find("AdIconBadge") as RectTransform;
        if (icon == null)
            return;

        icon.anchorMin = new Vector2(1f, 0.5f);
        icon.anchorMax = new Vector2(1f, 0.5f);
        icon.pivot = new Vector2(1f, 0.5f);
        icon.anchoredPosition = new Vector2(-12f, 0f);
        icon.sizeDelta = new Vector2(48f, 48f);
        icon.localScale = Vector3.one;
    }

    private void ConfigurePermanentButton(RectTransform buttonRect)
    {
        if (buttonRect == null)
            return;

        var label = buttonRect.Find("Text (TMP)") as RectTransform;
        if (label != null)
        {
            label.anchorMin = Vector2.zero;
            label.anchorMax = new Vector2(0.62f, 1f);
            label.offsetMin = new Vector2(14f, 4f);
            label.offsetMax = new Vector2(-4f, -4f);
        }

        var priceRoot = buttonRect.Find("ClaimAllPrice") as RectTransform;
        if (priceRoot == null)
        {
            var priceObject = new GameObject("ClaimAllPrice", typeof(RectTransform));
            priceObject.transform.SetParent(buttonRect, false);
            priceRoot = priceObject.transform as RectTransform;
        }

        priceRoot.anchorMin = new Vector2(0.62f, 0f);
        priceRoot.anchorMax = Vector2.one;
        priceRoot.pivot = new Vector2(0.5f, 0.5f);
        priceRoot.anchoredPosition = Vector2.zero;
        priceRoot.sizeDelta = Vector2.zero;
        priceRoot.localScale = Vector3.one;
        priceRoot.gameObject.SetActive(unlockPrice > 0d);

        var icon = priceRoot.Find("CurrencyIcon") as RectTransform;
        if (icon == null)
        {
            var iconObject = new GameObject(
                "CurrencyIcon",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            iconObject.transform.SetParent(priceRoot, false);
            icon = iconObject.transform as RectTransform;
        }

        icon.anchorMin = new Vector2(0.02f, 0.5f);
        icon.anchorMax = new Vector2(0.02f, 0.5f);
        icon.pivot = new Vector2(0f, 0.5f);
        icon.anchoredPosition = Vector2.zero;
        icon.sizeDelta = new Vector2(42f, 42f);
        var iconImage = icon.GetComponent<Image>();
        iconImage.sprite = G.Currency != null ? G.Currency.GetCurrencyIcon(unlockPriceCurrency) : null;
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;

        var priceText = priceRoot.Find("PriceText")?.GetComponent<TextMeshProUGUI>();
        if (priceText == null)
        {
            var textObject = new GameObject(
                "PriceText",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            textObject.transform.SetParent(priceRoot, false);
            priceText = textObject.GetComponent<TextMeshProUGUI>();
        }

        var priceRect = priceText.rectTransform;
        priceRect.anchorMin = new Vector2(0.42f, 0f);
        priceRect.anchorMax = Vector2.one;
        priceRect.offsetMin = Vector2.zero;
        priceRect.offsetMax = new Vector2(-8f, 0f);
        priceText.text = G.Currency != null
            ? G.Currency.ToString(unlockPrice)
            : Math.Round(unlockPrice).ToString("0", CultureInfo.InvariantCulture);
        priceText.alignment = TextAlignmentOptions.Center;
        priceText.enableAutoSizing = true;
        priceText.fontSizeMin = 20f;
        priceText.fontSizeMax = 31f;
        priceText.color = Color.white;
        priceText.raycastTarget = false;
        BlockyUITheme.ApplyText(priceText, Color.white, 22);
        priceText.textWrappingMode = TextWrappingModes.NoWrap;
        priceText.overflowMode = TextOverflowModes.Overflow;
    }

    private void ConfigureCloseButton(RectTransform closeRect)
    {
        if (closeRect == null)
            return;

        var button = closeRect.GetComponent<Button>();
        if (button != null)
        {
            BlockyUITheme.ApplyButton(button, BlockyUITheme.RedHeader);
            ApplyClaimPopupSurface(button.targetGraphic as Image, BlockyUITheme.RedHeader, true);
            EnsureGradient(closeRect);
        }

        var glyph = closeRect.Find("ClaimAllCloseGlyph")?.GetComponent<TextMeshProUGUI>();
        if (glyph == null)
        {
            var glyphObject = new GameObject(
                "ClaimAllCloseGlyph",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            glyphObject.transform.SetParent(closeRect, false);
            glyph = glyphObject.GetComponent<TextMeshProUGUI>();
        }

        Stretch(glyph.rectTransform, new Vector2(4f, 2f), new Vector2(-4f, -2f));
        glyph.text = "X";
        glyph.alignment = TextAlignmentOptions.Center;
        glyph.enableAutoSizing = true;
        glyph.fontSizeMin = 30f;
        glyph.fontSizeMax = 46f;
        glyph.color = Color.white;
        glyph.raycastTarget = false;
        BlockyUITheme.ApplyText(glyph, Color.white, 34);
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
        for (var i = 0; i < shadows.Length; i++)
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

    private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        rect.localScale = Vector3.one;
    }

    private static void SetSplitButton(RectTransform rect, float anchorMinX, float anchorMaxX)
    {
        rect.anchorMin = new Vector2(anchorMinX, 0f);
        rect.anchorMax = new Vector2(anchorMaxX, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private void ConfigureText(Transform textTransform, float fontSize)
    {
        var text = textTransform != null ? textTransform.GetComponent<TMP_Text>() : null;
        if (text == null)
            return;

        text.color = claimPopupTextColor;
        text.fontSize = fontSize;
        text.enableAutoSizing = true;
        text.fontSizeMin = Mathf.Max(12f, fontSize * 0.65f);
        text.fontSizeMax = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.overflowMode = TextOverflowModes.Ellipsis;
        BlockyUITheme.ApplyText(text, claimPopupTextColor, Mathf.RoundToInt(fontSize * 0.65f));
    }

    private string ResolveUnlockSaveKey()
    {
        var id = SanitizeZoneId(zoneId);
        if (string.IsNullOrWhiteSpace(id))
            id = "default";
        return UnlockSaveKeyPrefix + id;
    }

    private static string SanitizeZoneId(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        var chars = new List<char>(raw.Length);
        for (var i = 0; i < raw.Length; i++)
        {
            var c = raw[i];
            if (char.IsLetterOrDigit(c))
                chars.Add(char.ToLowerInvariant(c));
            else if (c == '_' || c == '-' || c == '.')
                chars.Add(c);
        }

        return new string(chars.ToArray());
    }

    private void PersistPermanentUnlocked()
    {
        if (G.Save == null)
            return;

        if (string.IsNullOrWhiteSpace(_resolvedSaveKey))
            _resolvedSaveKey = ResolveUnlockSaveKey();

        G.Save.SaveLevelStatus(_resolvedSaveKey, _permanentUnlocked);
    }

    private bool LoadPermanentUnlocked()
    {
        if (G.Save == null)
            return false;

        if (string.IsNullOrWhiteSpace(_resolvedSaveKey))
            _resolvedSaveKey = ResolveUnlockSaveKey();

        var scoped = G.Save.GetLevelStatus(_resolvedSaveKey);
        if (scoped)
            return true;

        if (!fallbackToLegacyUnlockKey)
            return false;

        var legacy = G.Save.GetLevelStatus(LegacyUnlockSaveKey);
        if (!legacy)
            return false;

        G.Save.SaveLevelStatus(_resolvedSaveKey, true);
        return true;
    }

    private static Transform FindChildByNameToken(Transform root, params string[] tokens)
    {
        if (root == null || tokens == null || tokens.Length == 0)
            return null;

        var stack = new Stack<Transform>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            var lowerName = current.name != null ? current.name.ToLowerInvariant() : string.Empty;
            var hit = false;
            for (var i = 0; i < tokens.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(tokens[i]))
                    continue;
                if (lowerName.Contains(tokens[i].ToLowerInvariant()))
                {
                    hit = true;
                    break;
                }
            }

            if (hit && current != root)
                return current;

            for (var i = 0; i < current.childCount; i++)
                stack.Push(current.GetChild(i));
        }

        return null;
    }

    private static string L(string key, string fallback)
    {
        if (LocalizationManager.Instance != null && LocalizationManager.Instance.LocalizationData != null)
        {
            var translated = LocalizationManager.Instance.LocalizationData.GetTranslation(key);
            if (!string.IsNullOrWhiteSpace(translated) && !string.Equals(translated, key, StringComparison.Ordinal))
                return translated;
        }

        return fallback;
    }
}
