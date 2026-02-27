using System;
using UnityEngine;

public class LocalProfileBoardPoint : MonoBehaviour
{
    [Header("Scene Refs")]
    [SerializeField] private InteractionPanel interactionPanel;
    [SerializeField] private ZooBaseSnapshotSync snapshotSync;
    [SerializeField] private RemoteBasesApplier remoteBases;
    [SerializeField] private Transform statsRoot;
    [SerializeField] private bool autoDiscoverReferences = true;

    [Header("Behavior")]
    [SerializeField] private bool closePopupOnExit = false;
    [SerializeField] private bool preferSnapshotStats = true;

    [Header("Localization")]
    [SerializeField] private string interactionLocalizationKey = "UI/Profile/OpenBoard";
    [SerializeField] private string interactionTextFallback = "Profile";
    [SerializeField] private string playerFallbackLocalizationKey = "UI/Common/Player";
    [SerializeField] private string playerFallbackText = "Player";

    private bool _playerInside;

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

    private void OnEnable()
    {
        if (interactionPanel != null)
            interactionPanel.InteractionComplete.AddListener(OnInteractionRequested);
        RefreshInteractionState();
    }

    private void OnDisable()
    {
        if (interactionPanel != null)
            interactionPanel.InteractionComplete.RemoveListener(OnInteractionRequested);
        HideInteraction();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        _playerInside = true;
        RefreshInteractionState();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        _playerInside = false;
        HideInteraction();
        if (closePopupOnExit)
            RemoteProfilePopup.Instance.Hide();
    }

    private void OnInteractionRequested()
    {
        if (!_playerInside)
            return;

        var stats = BuildStats();
        var displayName = ResolveDisplayName();
        RemoteProfilePopup.Instance.Show(displayName, stats);
    }

    private PlayerPublicStatsDto BuildStats()
    {
        if (preferSnapshotStats && TryBuildFromSnapshot(out var snapshotStats))
            return snapshotStats;

        return BuildFallbackStats();
    }

    private bool TryBuildFromSnapshot(out PlayerPublicStatsDto stats)
    {
        stats = null;
        var sync = GetSnapshotSync();
        if (sync == null)
            return false;

        var dto = sync.BuildSnapshotDto();
        if (dto?.playerStats == null)
            return false;

        stats = dto.playerStats;
        return true;
    }

    private PlayerPublicStatsDto BuildFallbackStats()
    {
        var result = new PlayerPublicStatsDto
        {
            totalHatched = LocalPlayerStatsStore.GetTotalHatched(),
            petsIncomePerSec = 0d,
            bestPetIncomePerSec = 0d,
            bigPetIncomePerSec = 0d
        };

        var root = ResolveStatsRoot();
        if (root == null)
            return result;

        var cells = root.GetComponentsInChildren<FieldCell>(true);
        for (var i = 0; i < cells.Length; i++)
        {
            var cell = cells[i];
            if (cell == null)
                continue;

            var field = cell.GetComponentInParent<Field>();
            if (field != null && field.IsRemoteMode)
                continue;

            var pet = cell.CurrentBrainrot;
            if (pet == null)
                continue;

            var income = Math.Max(0d, pet.DinamicData.ResultIncome);
            result.petsIncomePerSec += income;
            if (income > result.bestPetIncomePerSec)
                result.bestPetIncomePerSec = income;
        }

        var bigPets = root.GetComponentsInChildren<BigPetPoint>(true);
        for (var i = 0; i < bigPets.Length; i++)
        {
            var bigPet = bigPets[i];
            if (bigPet == null || bigPet.IsRemoteMode)
                continue;

            var income = Math.Max(0d, bigPet.CurrentIncomePerSecond);
            if (income > result.bigPetIncomePerSec)
                result.bigPetIncomePerSec = income;
        }

        return result;
    }

    private Transform ResolveStatsRoot()
    {
        if (statsRoot != null)
            return statsRoot;

        var bases = GetRemoteBases();
        if (bases != null)
        {
            if (bases.TryGetResolvedLocalSlotRoot(out var resolvedRoot))
                return resolvedRoot;

            var fallbackRoot = bases.GetLocalSlotRoot();
            if (fallbackRoot != null)
                return fallbackRoot;
        }

        return transform.root;
    }

    private ZooBaseSnapshotSync GetSnapshotSync()
    {
        if (snapshotSync != null)
            return snapshotSync;

        if (autoDiscoverReferences)
        {
            snapshotSync = GetComponentInParent<ZooBaseSnapshotSync>();
            if (snapshotSync == null)
                snapshotSync = FindAnyObjectByType<ZooBaseSnapshotSync>();
        }

        return snapshotSync;
    }

    private RemoteBasesApplier GetRemoteBases()
    {
        if (remoteBases != null)
            return remoteBases;

        if (autoDiscoverReferences)
        {
            remoteBases = GetComponentInParent<RemoteBasesApplier>();
            if (remoteBases == null)
                remoteBases = FindAnyObjectByType<RemoteBasesApplier>();
        }

        return remoteBases;
    }

    private string ResolveDisplayName()
    {
        if (G.Save != null)
        {
            var profile = G.Save.LoadBackendProfile();
            if (!string.IsNullOrWhiteSpace(profile.displayName))
                return profile.displayName;
        }

        return L(playerFallbackLocalizationKey, playerFallbackText);
    }

    private void RefreshInteractionState()
    {
        if (interactionPanel == null)
            return;

        interactionPanel.gameObject.SetActive(_playerInside);
        if (_playerInside)
            interactionPanel.SetInfo(L(interactionLocalizationKey, interactionTextFallback));
    }

    private void HideInteraction()
    {
        if (interactionPanel != null)
            interactionPanel.gameObject.SetActive(false);
    }

    [ContextMenu("ProfileBoard/Auto Setup References")]
    private void AutoSetupReferences()
    {
        if (!autoDiscoverReferences)
            return;

        if (interactionPanel == null)
            interactionPanel = GetComponentInChildren<InteractionPanel>(true);

        if (snapshotSync == null)
            snapshotSync = GetComponentInParent<ZooBaseSnapshotSync>();

        if (remoteBases == null)
            remoteBases = GetComponentInParent<RemoteBasesApplier>();

        if (statsRoot == null)
        {
            var bases = remoteBases;
            if (bases != null && bases.TryGetResolvedLocalSlotRoot(out var resolvedRoot))
                statsRoot = resolvedRoot;
        }
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
