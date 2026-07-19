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
    [SerializeField] private int slotIndexOverride = -1;

    [Header("Behavior")]
    [SerializeField] private bool hideIfNoOnlinePlayerOnSlot = true;
    [SerializeField] private bool allowLocalSlotFallback = true;
    [SerializeField] private float targetRefreshIntervalSec = 0.3f;
    [SerializeField] private bool closePopupWhenTargetUnavailable = true;
    [SerializeField] private bool closePopupOnExit = false;
    [SerializeField] private bool preferSnapshotStats = true;

    [Header("Visibility")]
    [SerializeField] private GameObject boardVisualRoot;
    [SerializeField] private Collider triggerCollider;
    [SerializeField] private bool hideBoardVisualWhenNoTarget = true;
    [SerializeField] private bool disableTriggerWhenNoTarget;

    [Header("Localization")]
    [SerializeField] private string interactionLocalizationKey = "UI/Profile/OpenBoard";
    [SerializeField] private string interactionTextFallback = "Profile";
    [SerializeField] private string playerFallbackLocalizationKey = "UI/Common/Player";
    [SerializeField] private string playerFallbackText = "Player";

    private bool _playerInside;
    private bool _hasTarget;
    private bool _targetIsLocal;
    private int _resolvedSlotIndex = -1;
    private float _nextTargetRefreshAt;
    private string _targetPlayerId;
    private string _targetFriendCode;
    private string _targetDisplayName;
    private PlayerPublicStatsDto _targetStats;

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
        RefreshTargetData(force: true);
        RefreshVisibilityState();
        RefreshInteractionState();
    }

    private void OnDisable()
    {
        if (interactionPanel != null)
            interactionPanel.InteractionComplete.RemoveListener(OnInteractionRequested);
        HideInteraction();
    }

    private void Update()
    {
        RefreshTargetData();
        if (_playerInside)
            RefreshInteractionState();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        _playerInside = true;
        RefreshTargetData(force: true);
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

        RefreshTargetData(force: true, refreshStats: true);
        if (!_hasTarget)
            return;

        RemoteProfilePopup.Instance.Show(
            _targetDisplayName,
            _targetStats ?? new PlayerPublicStatsDto(),
            _targetPlayerId,
            _targetFriendCode);
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

        var snapshotStats = sync.BuildPublicStatsDto();
        if (snapshotStats == null)
            return false;

        stats = snapshotStats;
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

        var canInteract = _playerInside && _hasTarget;
        interactionPanel.gameObject.SetActive(canInteract);
        if (canInteract)
            interactionPanel.SetInfo(L(interactionLocalizationKey, interactionTextFallback));
    }

    private void HideInteraction()
    {
        if (interactionPanel != null)
            interactionPanel.gameObject.SetActive(false);
    }

    private void RefreshVisibilityState()
    {
        var shouldBeVisible = _hasTarget;

        if (boardVisualRoot != null && hideBoardVisualWhenNoTarget)
            boardVisualRoot.SetActive(shouldBeVisible);

        if (triggerCollider != null && disableTriggerWhenNoTarget)
            triggerCollider.enabled = shouldBeVisible;

        if (shouldBeVisible)
            return;

        _playerInside = false;
        HideInteraction();
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

        if (triggerCollider == null)
            triggerCollider = GetComponent<Collider>();

        if (boardVisualRoot == null)
        {
            var visual = FindChildByNameToken(transform, "visual", "board", "mesh", "model");
            if (visual != null)
                boardVisualRoot = visual.gameObject;
        }

        if (statsRoot == null)
        {
            var bases = remoteBases;
            if (bases != null && bases.TryGetResolvedLocalSlotRoot(out var resolvedRoot))
                statsRoot = resolvedRoot;
        }

        if (slotIndexOverride < 0)
        {
            var bases = remoteBases;
            if (bases != null && bases.TryResolveSlotIndex(transform, out var slotIndex))
                _resolvedSlotIndex = slotIndex;
        }
    }

    private void RefreshTargetData(bool force = false, bool refreshStats = false)
    {
        var now = Time.unscaledTime;
        if (!force && now < _nextTargetRefreshAt)
            return;

        _nextTargetRefreshAt = now + Mathf.Max(0.1f, targetRefreshIntervalSec);

        var hadTarget = _hasTarget;
        var prevPlayerId = _targetPlayerId;
        var prevFriendCode = _targetFriendCode;

        var hasTarget = TryResolveSlotTarget(
            refreshStats,
            out var playerId,
            out var friendCode,
            out var displayName,
            out var stats,
            out var isLocalTarget);

        _hasTarget = hasTarget;
        _targetIsLocal = hasTarget && isLocalTarget;

        if (!hasTarget)
        {
            _targetPlayerId = null;
            _targetFriendCode = null;
            _targetDisplayName = null;
            _targetStats = null;

            if (hadTarget && closePopupWhenTargetUnavailable)
                RemoteProfilePopup.Instance.Hide();
            RefreshVisibilityState();
            return;
        }

        _targetPlayerId = string.IsNullOrWhiteSpace(playerId) ? null : playerId;
        _targetFriendCode = string.IsNullOrWhiteSpace(friendCode) ? null : friendCode;
        _targetDisplayName = string.IsNullOrWhiteSpace(displayName) ? ResolveDisplayName() : displayName;
        if (_targetIsLocal)
        {
            if (refreshStats)
                _targetStats = stats ?? BuildStats();
        }
        else
        {
            _targetStats = stats ?? new PlayerPublicStatsDto();
        }
        RefreshVisibilityState();

        var targetChanged = !string.Equals(prevPlayerId, _targetPlayerId, StringComparison.Ordinal) ||
                            !string.Equals(prevFriendCode, _targetFriendCode, StringComparison.Ordinal);
        if (hadTarget && targetChanged && closePopupWhenTargetUnavailable)
            RemoteProfilePopup.Instance.Hide();
    }

    private bool TryResolveSlotTarget(
        bool includeLocalStats,
        out string playerId,
        out string friendCode,
        out string displayName,
        out PlayerPublicStatsDto stats,
        out bool isLocalTarget)
    {
        playerId = null;
        friendCode = null;
        displayName = null;
        stats = null;
        isLocalTarget = false;

        var bases = GetRemoteBases();
        var slotIndex = ResolveSlotIndex(bases);
        if (bases != null)
        {
            if (slotIndex < 0)
                return false;

            if (bases.TryGetProfileTargetForSlot(
                    slotIndex,
                    out var boardPlayerId,
                    out var boardFriendCode,
                    out var boardDisplayName,
                    out var boardStats,
                    out var boardOnline))
            {
                if (!hideIfNoOnlinePlayerOnSlot || boardOnline)
                {
                    playerId = boardPlayerId;
                    friendCode = boardFriendCode;
                    displayName = boardDisplayName;
                    stats = boardStats;
                    isLocalTarget = IsLocalProfile(boardPlayerId, boardFriendCode);
                    return true;
                }
            }

            if (allowLocalSlotFallback && bases.IsLocalSlotForClient(slotIndex))
            {
                if (TryResolveLocalTarget(includeLocalStats, out playerId, out friendCode, out displayName, out stats))
                {
                    isLocalTarget = true;
                    return true;
                }
            }

            return false;
        }

        if (!allowLocalSlotFallback)
            return false;

        if (!TryResolveLocalTarget(includeLocalStats, out playerId, out friendCode, out displayName, out stats))
            return false;

        isLocalTarget = true;
        return true;
    }

    private int ResolveSlotIndex(RemoteBasesApplier bases)
    {
        if (slotIndexOverride >= 0)
            return slotIndexOverride;

        if (_resolvedSlotIndex >= 0)
            return _resolvedSlotIndex;

        if (bases == null)
            return -1;

        if (bases.TryResolveSlotIndex(transform, out var slotIndex))
        {
            _resolvedSlotIndex = slotIndex;
            return slotIndex;
        }

        if (statsRoot != null && bases.TryResolveSlotIndex(statsRoot, out slotIndex))
        {
            _resolvedSlotIndex = slotIndex;
            return slotIndex;
        }

        return -1;
    }

    private bool TryResolveLocalTarget(
        bool includeStats,
        out string playerId,
        out string friendCode,
        out string displayName,
        out PlayerPublicStatsDto stats)
    {
        playerId = null;
        friendCode = null;
        displayName = ResolveDisplayName();
        stats = includeStats ? BuildStats() : null;

        if (G.Save != null)
        {
            var profile = G.Save.LoadBackendProfile();
            playerId = string.IsNullOrWhiteSpace(profile.playerId) ? null : profile.playerId;
            friendCode = string.IsNullOrWhiteSpace(profile.friendCode) ? null : profile.friendCode;
            if (!string.IsNullOrWhiteSpace(profile.displayName))
                displayName = profile.displayName;
        }

        if (!string.IsNullOrWhiteSpace(playerId) || !string.IsNullOrWhiteSpace(friendCode))
            return true;

        return G.Player != null;
    }

    private bool IsLocalProfile(string playerId, string friendCode)
    {
        if (G.Save == null)
            return false;

        var profile = G.Save.LoadBackendProfile();
        if (!string.IsNullOrWhiteSpace(playerId) &&
            !string.IsNullOrWhiteSpace(profile.playerId) &&
            string.Equals(playerId, profile.playerId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(friendCode) &&
            !string.IsNullOrWhiteSpace(profile.friendCode) &&
            string.Equals(friendCode, profile.friendCode, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static Transform FindChildByNameToken(Transform root, params string[] tokens)
    {
        if (root == null || tokens == null || tokens.Length == 0)
            return null;

        var stack = new System.Collections.Generic.Stack<Transform>();
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
