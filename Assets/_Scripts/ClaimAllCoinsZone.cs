using System;
using System.Collections;
using System.Globalization;
using UnityEngine;

public class ClaimAllCoinsZone : MonoBehaviour
{
    private const string NoAdsSaveKey = "ClaimAllNoAdsUnlocked";

    [Header("Panels")]
    [SerializeField] private InteractionPanel claimPanel;
    [SerializeField] private InteractionPanel noAdsUpgradePanel;
    [SerializeField] private GameObject readyIndicator;

    [Header("Claim Settings")]
    [SerializeField] private bool requireRewardedAdUntilUpgrade = true;
    [SerializeField] private string rewardedAdId = "ClaimAllCoins";
    [SerializeField] private bool includeBigPetIncome = true;
    [SerializeField] private bool showOnlyWhenIncomeAvailable = false;
    [SerializeField] private float stateRefreshSec = 0.5f;
    [SerializeField] private AudioSource collectAudio;

    [Header("No Ads Upgrade")]
    [SerializeField] private CurrencyType noAdsPriceCurrency = CurrencyType.Gems;
    [SerializeField] private double noAdsPrice = 49d;

    [Header("Text")]
    [SerializeField] private string claimLocalizationKey = "UI/ClaimAll/Collect";
    [SerializeField] private string claimText = "Claim all";
    [SerializeField] private string claimAdLocalizationKey = "UI/ClaimAll/CollectAd";
    [SerializeField] private string claimAdText = "Claim all (AD)";
    [SerializeField] private string noAdsLocalizationKey = "UI/ClaimAll/NoAds";
    [SerializeField] private string noAdsText = "No Ads";

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    private bool _playerInside;
    private bool _actionInFlight;
    private bool _noAdsUnlocked;
    private Coroutine _stateRoutine;

    private void Awake()
    {
        if (claimPanel == null)
            claimPanel = GetComponentInChildren<InteractionPanel>(true);
    }

    private void Start()
    {
        _noAdsUnlocked = LoadNoAdsUnlocked();
        RefreshPanels();
    }

    private void OnEnable()
    {
        SubscribePanels();
        RefreshPanels();
    }

    private void OnDisable()
    {
        UnsubscribePanels();
        StopStateRoutine();
        HidePanels();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        _playerInside = true;
        StartStateRoutine();
        RefreshPanels();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        _playerInside = false;
        StopStateRoutine();
        HidePanels();
    }

    private void SubscribePanels()
    {
        if (claimPanel != null)
            claimPanel.InteractionComplete.AddListener(OnClaimRequested);
        if (noAdsUpgradePanel != null)
            noAdsUpgradePanel.InteractionComplete.AddListener(OnNoAdsUpgradeRequested);
    }

    private void UnsubscribePanels()
    {
        if (claimPanel != null)
            claimPanel.InteractionComplete.RemoveListener(OnClaimRequested);
        if (noAdsUpgradePanel != null)
            noAdsUpgradePanel.InteractionComplete.RemoveListener(OnNoAdsUpgradeRequested);
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

    private IEnumerator StateRefreshRoutine()
    {
        var wait = new WaitForSecondsRealtime(Mathf.Max(0.2f, stateRefreshSec));
        while (_playerInside)
        {
            RefreshPanels();
            yield return wait;
        }

        _stateRoutine = null;
    }

    private void OnClaimRequested()
    {
        if (_actionInFlight || !_playerInside)
            return;

        StartCoroutine(ClaimFlow());
    }

    private void OnNoAdsUpgradeRequested()
    {
        if (_actionInFlight || !_playerInside)
            return;
        if (_noAdsUnlocked)
            return;

        if (noAdsPrice <= 0d)
        {
            _noAdsUnlocked = true;
            PersistNoAdsUnlocked();
            RefreshPanels();
            return;
        }

        if (G.Currency == null)
            return;

        if (!G.Currency.RemoveCurrency(noAdsPriceCurrency, noAdsPrice))
            return;

        _noAdsUnlocked = true;
        PersistNoAdsUnlocked();
        RefreshPanels();
    }

    private IEnumerator ClaimFlow()
    {
        _actionInFlight = true;
        RefreshPanels();

        if (!HasCollectibleIncome())
        {
            _actionInFlight = false;
            RefreshPanels();
            yield break;
        }

        if (NeedsRewardedAd())
        {
            if (G.Ad == null)
            {
                if (debugLogs) Debug.LogWarning("[ClaimAll] Ads manager is missing.");
                _actionInFlight = false;
                RefreshPanels();
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
                RefreshPanels();
                yield break;
            }
        }

        var total = CollectAllIncome();
        if (total > 0d && collectAudio != null)
            collectAudio.Play();

        if (debugLogs)
            Debug.Log($"[ClaimAll] Collected total={total.ToString("0", CultureInfo.InvariantCulture)}");

        _actionInFlight = false;
        RefreshPanels();
    }

    private bool NeedsRewardedAd()
    {
        return requireRewardedAdUntilUpgrade && !_noAdsUnlocked;
    }

    private double CollectAllIncome()
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

    private void RefreshPanels()
    {
        var hasIncome = HasCollectibleIncome();
        var canShowClaim = _playerInside && !_actionInFlight && (!showOnlyWhenIncomeAvailable || hasIncome);

        if (claimPanel != null)
        {
            claimPanel.gameObject.SetActive(canShowClaim);
            if (canShowClaim)
            {
                var label = NeedsRewardedAd()
                    ? L(claimAdLocalizationKey, claimAdText)
                    : L(claimLocalizationKey, claimText);
                claimPanel.SetInfo(label);
            }
        }

        var canShowNoAdsUpgrade =
            noAdsUpgradePanel != null &&
            _playerInside &&
            !_actionInFlight &&
            requireRewardedAdUntilUpgrade &&
            !_noAdsUnlocked &&
            noAdsPrice > 0d;

        if (noAdsUpgradePanel != null)
        {
            noAdsUpgradePanel.gameObject.SetActive(canShowNoAdsUpgrade);
            if (canShowNoAdsUpgrade)
            {
                var priceText = G.Currency != null
                    ? G.Currency.ToString(noAdsPrice)
                    : Math.Round(noAdsPrice).ToString("0", CultureInfo.InvariantCulture);
                var label = L(noAdsLocalizationKey, noAdsText);
                if (noAdsPriceCurrency == CurrencyType.Coins)
                {
                    noAdsUpgradePanel.SetInfo(label, priceText);
                }
                else
                {
                    noAdsUpgradePanel.SetInfo($"{label} ({priceText} {noAdsPriceCurrency})");
                }
            }
        }

        if (readyIndicator != null)
            readyIndicator.SetActive(hasIncome);
    }

    private void HidePanels()
    {
        if (claimPanel != null)
            claimPanel.gameObject.SetActive(false);
        if (noAdsUpgradePanel != null)
            noAdsUpgradePanel.gameObject.SetActive(false);
    }

    private void PersistNoAdsUnlocked()
    {
        if (G.Save == null)
            return;

        G.Save.SaveLevelStatus(NoAdsSaveKey, _noAdsUnlocked);
    }

    private bool LoadNoAdsUnlocked()
    {
        if (G.Save == null)
            return false;

        return G.Save.GetLevelStatus(NoAdsSaveKey);
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
