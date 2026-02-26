using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

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

    [Header("Scene Refs")]
    [SerializeField] private InteractionPanel interactionPanel;
    [SerializeField] private UniversalDecisionPopup decisionPopup;
    [SerializeField] private GameObject readyIndicator;
    [SerializeField] private AudioSource collectAudio;

    [Header("Claim Settings")]
    [SerializeField] private string rewardedAdId = "ClaimAllCoins";
    [SerializeField] private double rewardedMultiplier = 2d;
    [SerializeField] private bool includeBigPetIncome = true;
    [SerializeField] private float stateRefreshSec = 0.5f;
    [SerializeField] private bool showIndicatorOnlyWhenIncomeAvailable = true;

    [Header("Permanent Unlock")]
    [SerializeField] private CurrencyType unlockPriceCurrency = CurrencyType.Gems;
    [SerializeField] private double unlockPrice = 49d;
    [SerializeField] private bool autoCollectWhenUnlocked = true;
    [SerializeField] private bool collectImmediatelyOnEnterAfterUnlock = true;
    [SerializeField] private float autoCollectIntervalSec = 1f;

    [Header("Localization")]
    [SerializeField] private string interactionLocalizationKey = "UI/ClaimAll/OpenPopup";
    [SerializeField] private string interactionText = "Claim all";
    [SerializeField] private string popupTitleLocalizationKey = "UI/ClaimAll/PopupTitle";
    [SerializeField] private string popupTitleText = "Collect all income";
    [SerializeField] private string popupDescriptionLocalizationKey = "UI/ClaimAll/PopupDescription";
    [SerializeField] private string popupDescriptionText = "Choose how to collect income.";
    [SerializeField] private string popupCollectX2LocalizationKey = "UI/ClaimAll/PopupCollectX2Ad";
    [SerializeField] private string popupCollectX2Text = "Take x2 (AD)";
    [SerializeField] private string popupBuyForeverLocalizationKey = "UI/ClaimAll/PopupBuyForever";
    [SerializeField] private string popupBuyForeverText = "Buy forever";

    [Header("Debug")]
    [SerializeField] private bool debugLogs;

    private bool _playerInside;
    private bool _actionInFlight;
    private bool _permanentUnlocked;
    private Coroutine _stateRoutine;
    private Coroutine _autoCollectRoutine;
    private string _resolvedSaveKey;

    private void Awake()
    {
        AutoSetupReferences();
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
            return;

        AutoSetupReferences();
    }

    private void Start()
    {
        _resolvedSaveKey = ResolveUnlockSaveKey();
        _permanentUnlocked = LoadPermanentUnlocked();
        RefreshVisualState();
    }

    private void OnEnable()
    {
        SubscribeEvents();
        RefreshVisualState();
    }

    private void OnDisable()
    {
        UnsubscribeEvents();
        StopStateRoutine();
        StopAutoCollectRoutine();
        HideInteraction();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        _playerInside = true;
        StartStateRoutine();
        if (_permanentUnlocked)
        {
            StartAutoCollectRoutine();
            if (collectImmediatelyOnEnterAfterUnlock)
                TryCollectWithoutAd("enter");
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
        if (_actionInFlight || !_playerInside)
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

        if (popup.IsOpen)
            return;

        var request = new UniversalDecisionPopup.Request
        {
            title = new UniversalDecisionPopup.LocalizedTextPayload(popupTitleLocalizationKey, popupTitleText),
            description = new UniversalDecisionPopup.LocalizedTextPayload(string.Empty, BuildPopupDescription()),
            confirm = new UniversalDecisionPopup.LocalizedTextPayload(popupCollectX2LocalizationKey, popupCollectX2Text),
            cancel = new UniversalDecisionPopup.LocalizedTextPayload(string.Empty, BuildBuyForeverButtonText()),
            onConfirm = () =>
            {
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
                if (collectImmediatelyOnEnterAfterUnlock)
                    TryCollectWithoutAd("unlock");
                StartAutoCollectRoutine();
                RefreshVisualState();
            },
            closeOnConfirm = true,
            closeOnCancel = false,
            closeButtonActsAsCancel = false
        };

        popup.Show(request);
    }

    private IEnumerator ClaimWithAdX2Flow()
    {
        _actionInFlight = true;
        RefreshVisualState();

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
            return true;
        }

        if (G.Currency == null)
            return false;

        if (!G.Currency.RemoveCurrency(unlockPriceCurrency, unlockPrice))
            return false;

        _permanentUnlocked = true;
        PersistPermanentUnlocked();
        if (debugLogs)
            Debug.Log("[ClaimAll] Permanent unlock purchased.");
        return true;
    }

    private void TryCollectWithoutAd(string source)
    {
        if (!_playerInside || _actionInFlight)
            return;
        if (!HasCollectibleIncome())
            return;

        CollectAllIncomeWithBonus(1d, source);
        RefreshVisualState();
    }

    private double CollectAllIncomeWithBonus(double multiplier, string source)
    {
        var baseCollected = CollectAllIncomeRaw();
        if (baseCollected <= 0d)
            return 0d;

        var safeMultiplier = Math.Max(1d, multiplier);
        var bonus = baseCollected * (safeMultiplier - 1d);
        if (bonus > 0d)
            AddBonusCoins(bonus);

        var final = baseCollected + Math.Max(0d, bonus);
        if (collectAudio != null)
            collectAudio.Play();

        if (debugLogs)
            Debug.Log($"[ClaimAll] source={source} collected={final.ToString("0", CultureInfo.InvariantCulture)}");

        return final;
    }

    private void AddBonusCoins(double amount)
    {
        if (amount <= 0d)
            return;

        if (G.Income != null)
        {
            G.Income.AddCoins(amount);
            return;
        }

        if (G.Currency != null)
            G.Currency.AddCurrency(CurrencyType.Coins, amount);
    }

    private double CollectAllIncomeRaw()
    {
        double total = 0d;

        var brainrots = FindObjectsByType<Brainrot>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (var i = 0; i < brainrots.Length; i++)
        {
            if (brainrots[i] == null)
                continue;
            total += brainrots[i].CollectIncome(playAudio: false);
        }

        if (includeBigPetIncome)
        {
            var bigPets = FindObjectsByType<BigPetPoint>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (var i = 0; i < bigPets.Length; i++)
            {
                if (bigPets[i] == null)
                    continue;
                total += bigPets[i].CollectIncome(playAudio: false);
            }
        }

        return Math.Max(0d, total);
    }

    private bool HasCollectibleIncome()
    {
        var brainrots = FindObjectsByType<Brainrot>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (var i = 0; i < brainrots.Length; i++)
        {
            if (brainrots[i] != null && brainrots[i].HasCollectibleIncome)
                return true;
        }

        if (!includeBigPetIncome)
            return false;

        var bigPets = FindObjectsByType<BigPetPoint>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (var i = 0; i < bigPets.Length; i++)
        {
            if (bigPets[i] != null && bigPets[i].HasCollectibleIncome)
                return true;
        }

        return false;
    }

    private void RefreshVisualState()
    {
        var hasIncome = HasCollectibleIncome();
        var showInteraction = !_permanentUnlocked && _playerInside && !_actionInFlight;

        if (interactionPanel != null)
        {
            interactionPanel.gameObject.SetActive(showInteraction);
            if (showInteraction)
                interactionPanel.SetInfo(L(interactionLocalizationKey, interactionText));
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

    private UniversalDecisionPopup ResolveDecisionPopup()
    {
        if (decisionPopup != null)
            return decisionPopup;
        if (!autoDiscoverPopup)
            return null;

        var popups = FindObjectsByType<UniversalDecisionPopup>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (popups != null && popups.Length > 0)
            decisionPopup = popups[0];

        return decisionPopup;
    }

    private string BuildPopupDescription()
    {
        var baseText = L(popupDescriptionLocalizationKey, popupDescriptionText);
        return $"{baseText}\n{BuildBuyForeverButtonText()}";
    }

    private string BuildBuyForeverButtonText()
    {
        var label = L(popupBuyForeverLocalizationKey, popupBuyForeverText);
        if (unlockPrice <= 0d)
            return label;

        var priceText = G.Currency != null
            ? G.Currency.ToString(unlockPrice)
            : Math.Round(unlockPrice).ToString("0", CultureInfo.InvariantCulture);

        if (unlockPriceCurrency == CurrencyType.Coins)
            return $"{label} ({priceText})";

        return $"{label} ({priceText} {unlockPriceCurrency})";
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
