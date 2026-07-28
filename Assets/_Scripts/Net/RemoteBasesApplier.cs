using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[DefaultExecutionOrder(-1000)]
public class RemoteBasesApplier : MonoBehaviour
{
    private const string EmptySlotMarker = "__empty__";
    private const string OfflineLocalPlayerId = "__offline_local__";
    private const float MinSyncDistanceMeters = 100f;
    private const int BaselineConveyorLevel = 0;
    private const int BaselineBigPetId = 0;
    private const int BaselineBigPetLevel = 1;
    private const int BaselineBigPetXp = 0;
    private const bool BaselineBigPetPurchased = false;
    private const int ShowcaseFallbackVersion = 3;

    private class SlotSnapshotCache
    {
        public int conveyorLevel = int.MinValue;
        public int bigPetId = int.MinValue;
        public int bigPetLvl = int.MinValue;
        public int bigPetXp = int.MinValue;
        public bool bigPetPurchased;
        public HashSet<int> land = new();
        public Dictionary<string, string> cells = new();
    }

    [Serializable]
    public class RemoteBaseSlot
    {
        public string name;
        public Transform root;
        public Transform fieldsRoot;
        public Transform teleportTarget;
        public GiftChestController chest;
        public RemoteFriendBoard friendBoard;
        public bool isLocalSlot = false;
        public bool lockCells = true;
        public bool applyLand = true;
    }

    [Header("Slots (5 remote bases)")]
    [SerializeField] private List<RemoteBaseSlot> slots = new();

    [Header("Behavior")]
    [SerializeField] private bool applyOnStart = true;
    [SerializeField] private bool clearEmptySlots = true;
    [SerializeField] private bool disableEmptySlots = true;
    [SerializeField] private bool deterministicSlots = true;
    [SerializeField] private bool spawnRemotePlayers = true;
    [SerializeField] private GameObject remotePlayerPrefab;
    [SerializeField] private bool moveFriendBoardToRemotePlayer = true;
    [SerializeField] private float remotePlayerDelaySec = 1.1f;
    [SerializeField] private GameObject remoteInteractionCanvasPrefab;
    [SerializeField] private float remoteInteractionDistance = 2.5f;
    [SerializeField] private bool smoothSnapshotApply = true;
    [SerializeField] private int snapshotOpsPerFrame = 12;
    [SerializeField] private bool incrementalSnapshotApply = true;
    [Header("Local Home Marker")]
    [SerializeField] private bool showLocalHomeMarker = true;
    [SerializeField] private LocalHomeWorldMarker localHomeMarkerPrefab;
    [Header("Proximity Sync")]
    [SerializeField] private bool syncOnlyNearSlots = true;
    [SerializeField] private float syncDistanceMeters = 100f;
    [SerializeField] private float syncDistanceHysteresisMeters = 2f;
    [SerializeField] private bool renderRemotePlayersOutsideBaseSyncRange = true;
    [Header("Fallback Remote Progress")]
    [SerializeField] private int randomRemoteProgressSeed = 9173;
    [SerializeField] private int randomRemoteMinCells = 2;
    [SerializeField] private int randomRemoteMaxCells = 7;
    [SerializeField, Range(0f, 1f)] private float randomRemoteEggChance = 0.35f;
    [SerializeField] private int randomRemoteMaxConveyorLevel = 3;
    [SerializeField] private bool randomRemoteBigPetProgress = false;
    [SerializeField] private int randomRemoteMaxBigPetLevel = 6;
    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;
    [SerializeField] private bool testCloneLocalToRandomSlot = false;
    [SerializeField] private int testCloneSlotIndex = -1;

    private ZooBackendClient backend;
    private readonly Dictionary<string, int> _playerToSlot = new();
    private string[] _slotPlayerIds;
    private string[] _slotUpdatedAt;
    private GameObject[] _slotRemotePlayers;
    private string[] _slotRemotePlayerOwnerIds;
    private RemoteFriendBoard[] _slotRemoteBoards;
    private Coroutine[] _slotSnapshotApplyRoutines;
    private BaseSnapshotDto[] _slotSnapshotApplyPending;
    private bool[] _slotHadSnapshot;
    private SlotSnapshotCache[] _slotSnapshotCaches;
    private BaseSnapshotDto[] _slotFallbackSnapshots;
    private int _fallbackSnapshotLocalSlotIndex = int.MinValue;
    private string _lastLocalPlayerId;
    private int _serverLocalSlotIndex = -1;
    private int _lastTeleportedSlotIndex = -2;
    private string _lastTeleportedPlayerId;
    private Coroutine _teleportRoutine;
    private bool _lobbyModeActive;
    private int _lastPreparedLocalSlotIndex = -1;
    private int[] _slotModeState;
    private bool[] _slotWithinSyncRange;
    private bool[] _slotIsBaselineVisual;
    private bool _didInitialFullLobbySync;
    private bool _suppressNextAutoTeleport;
    private Coroutine _waitForSaveReadyRoutine;
    private int _pendingLocalRestoreSlotIndex = -1;
    private bool _hasServerResolvedLocalSlot;
    private bool _forceLocalRestoreOnNextResolve = true;
    private LocalHomeWorldMarker _localHomeMarker;

    private void Awake()
    {
        syncDistanceMeters = Mathf.Max(MinSyncDistanceMeters, syncDistanceMeters);
        if (!Application.isEditor)
            testCloneLocalToRandomSlot = false;
        if (backend == null) backend = G.Backend;
        EnsureSlotState();
        EnsureInitialLocalSlotCandidate();
        MarkRemoteComponents();
    }

    private void OnEnable()
    {
        if (backend != null)
            backend.LocationsUpdated += ApplyLocations;
    }

    private void OnDisable()
    {
        if (backend != null)
            backend.LocationsUpdated -= ApplyLocations;
        if (_waitForSaveReadyRoutine != null)
        {
            StopCoroutine(_waitForSaveReadyRoutine);
            _waitForSaveReadyRoutine = null;
        }
        _pendingLocalRestoreSlotIndex = -1;
    }

    private void Start()
    {
        EnsureLocalHomeMarker();

        if (!applyOnStart || backend == null)
            return;

        if (backend.LastLocations != null && backend.LastLocations.Count > 0)
            ApplyLocations(new List<ZooLocationItem>(backend.LastLocations));
    }

    public void SetOfflineLocalOnly(bool enabled)
    {
        if (!enabled)
        {
            _lobbyModeActive = false;
            _didInitialFullLobbySync = false;
            return;
        }

        ApplyOfflineLocalOnly();
    }

    public void ApplyOfflineLocalOnly()
    {
        if (slots == null || slots.Count == 0)
            return;

        EnsureSlotState();
        if (_teleportRoutine != null)
        {
            StopCoroutine(_teleportRoutine);
            _teleportRoutine = null;
        }

        _lastTeleportedSlotIndex = -2;
        _lastTeleportedPlayerId = null;
        _playerToSlot.Clear();
        _lobbyModeActive = true;
        _didInitialFullLobbySync = false;
        _hasServerResolvedLocalSlot = false;
        if (string.IsNullOrEmpty(_lastLocalPlayerId))
            _lastLocalPlayerId = OfflineLocalPlayerId;
        var localSlotIndex = ResolveOfflineLocalSlotIndex();
        var keepPreparedLocalVisual =
            localSlotIndex >= 0 &&
            localSlotIndex == _lastPreparedLocalSlotIndex &&
            _slotModeState != null &&
            localSlotIndex < _slotModeState.Length &&
            _slotModeState[localSlotIndex] == 0;
        _forceLocalRestoreOnNextResolve = !keepPreparedLocalVisual;
        if (localSlotIndex >= 0)
            _lastPreparedLocalSlotIndex = localSlotIndex;

        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            if (slot == null || slot.root == null)
                continue;

            ResetSlotRuntimeState(i);

            if (i == localSlotIndex)
            {
                slot.root.gameObject.SetActive(true);
                ApplySlotMode(i, false);
                if (!keepPreparedLocalVisual)
                {
                    if (RestoreLocalSlotFromSave(i))
                    {
                        _lastPreparedLocalSlotIndex = i;
                    }
                    else if (G.Save == null || !G.Save.IsReady)
                    {
                        _lastPreparedLocalSlotIndex = i;
                    }
                    else
                    {
                        _lastPreparedLocalSlotIndex = -1;
                    }
                }
                if (_slotWithinSyncRange != null && i < _slotWithinSyncRange.Length)
                    _slotWithinSyncRange[i] = true;
                continue;
            }

            _slotPlayerIds[i] = null;
            if (_slotWithinSyncRange != null && i < _slotWithinSyncRange.Length)
                _slotWithinSyncRange[i] = false;
            UpdateChestLobby(slot, null);
            UpdateFriendBoardLobby(slot, null);
            ForceResetSlotToBaseline(i);
        }

        EnsureLocalSlot();
    }

    public void ApplyCachedLocationsFallback()
    {
        // First restore the authoritative local save and clear live lobby objects.
        ApplyOfflineLocalOnly();

        // Capacity fallback is snapshot-based, so allow the low-frequency locations
        // feed again after the live lobby state has been cleared.
        _lobbyModeActive = false;
        _didInitialFullLobbySync = false;
        if (backend != null && backend.LastLocations != null && backend.LastLocations.Count > 0)
            ApplyLocations(new List<ZooLocationItem>(backend.LastLocations));
    }

    private int ResolveOfflineLocalSlotIndex()
    {
        if (slots == null || slots.Count == 0)
            return -1;

        if (_lastPreparedLocalSlotIndex >= 0 &&
            _lastPreparedLocalSlotIndex < slots.Count &&
            slots[_lastPreparedLocalSlotIndex] != null &&
            slots[_lastPreparedLocalSlotIndex].root != null)
            return _lastPreparedLocalSlotIndex;

        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            if (slot == null || slot.root == null)
                continue;

            if (slot.isLocalSlot)
                return i;
        }

        return GetFallbackLocalSlotIndex();
    }

    private void ResetSlotRuntimeState(int slotIndex)
    {
        if (slots == null || slotIndex < 0 || slotIndex >= slots.Count)
            return;

        if (_slotSnapshotApplyRoutines != null && slotIndex < _slotSnapshotApplyRoutines.Length)
        {
            if (_slotSnapshotApplyRoutines[slotIndex] != null)
                StopCoroutine(_slotSnapshotApplyRoutines[slotIndex]);
            _slotSnapshotApplyRoutines[slotIndex] = null;
        }

        if (_slotSnapshotApplyPending != null && slotIndex < _slotSnapshotApplyPending.Length)
            _slotSnapshotApplyPending[slotIndex] = null;
        if (_slotHadSnapshot != null && slotIndex < _slotHadSnapshot.Length)
            _slotHadSnapshot[slotIndex] = false;
        if (_slotUpdatedAt != null && slotIndex < _slotUpdatedAt.Length)
            _slotUpdatedAt[slotIndex] = EmptySlotMarker;

        ResetSlotSnapshotCache(slotIndex);
        DisableRemotePlayer(slotIndex);
    }

    private void ForceResetSlotToBaseline(int slotIndex)
    {
        if (slots == null || slotIndex < 0 || slotIndex >= slots.Count)
            return;

        var slot = slots[slotIndex];
        if (slot == null || slot.root == null || IsLocalSlotIndex(slotIndex))
            return;

        slot.root.gameObject.SetActive(true);
        ApplySlotMode(slotIndex, true);
        DisableRemotePlayer(slotIndex);
        ClearSlot(slot, disableRoot: false);
        ApplySlotBaselineState(slotIndex, slot);

        if (_slotIsBaselineVisual != null && slotIndex < _slotIsBaselineVisual.Length)
            _slotIsBaselineVisual[slotIndex] = true;
    }

    private void MarkRemoteComponents()
    {
        if (slots == null) return;
        EnsureInitialLocalSlotCandidate();

        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            if (slot == null || slot.root == null) continue;
            ApplySlotMode(i, !IsLocalSlotIndex(i));
        }
    }

    private void EnsureInitialLocalSlotCandidate()
    {
        if (slots == null || slots.Count == 0)
            return;

        if (_serverLocalSlotIndex >= 0 && _serverLocalSlotIndex < slots.Count)
            return;

        if (_lastPreparedLocalSlotIndex >= 0 && _lastPreparedLocalSlotIndex < slots.Count)
            return;

        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            if (slot == null || slot.root == null)
                continue;

            if (slot.isLocalSlot)
            {
                _lastPreparedLocalSlotIndex = i;
                return;
            }
        }

        _lastPreparedLocalSlotIndex = GetFallbackLocalSlotIndex();
    }

    private void ApplyLocations(List<ZooLocationItem> locations)
    {
        // In lobby mode we use only lobby snapshots to avoid slot flicker from legacy locations feed.
        if (_lobbyModeActive)
            return;

        if (slots == null || slots.Count == 0)
            return;

        EnsureSlotState();
        EnsureLocalSlot();

        var available = new Dictionary<string, ZooLocationItem>();
        var localId = GetLocalPlayerId();
        if (locations != null)
        {
            foreach (var loc in locations)
            {
                if (loc == null || string.IsNullOrEmpty(loc.playerId)) continue;
                if (!string.IsNullOrEmpty(localId) && loc.playerId == localId) continue;
                if (!available.ContainsKey(loc.playerId))
                    available.Add(loc.playerId, loc);
            }
        }

        var usedPlayers = new HashSet<string>();

        // keep existing assignments if player still in list
        for (int i = 0; i < slots.Count; i++)
        {
            if (IsLocalSlotIndex(i)) continue;
            var pid = _slotPlayerIds[i];
            if (!string.IsNullOrEmpty(pid) && available.TryGetValue(pid, out var loc))
            {
                usedPlayers.Add(pid);
                if (IsSlotInSyncRange(i))
                {
                    if (slots[i].root != null)
                        slots[i].root.gameObject.SetActive(true);
                    MarkSlotAsLiveVisual(i);
                    EnsureRemotePlayer(i, loc.playerId);
                    UpdateChestLocation(slots[i], loc);
                    UpdateFriendBoardLocation(slots[i], loc);
                    ApplyIfChanged(i, slots[i], loc);
                }
                else
                {
                    UpdateChestLocation(slots[i], null);
                    UpdateFriendBoardLocation(slots[i], null);
                    ShowSlotBaselineVisual(i, forceSnapshotRefresh: true);
                }
            }
            else if (!string.IsNullOrEmpty(pid))
            {
                _playerToSlot.Remove(pid);
                _slotPlayerIds[i] = null;
                _slotUpdatedAt[i] = EmptySlotMarker;
                UpdateChestLocation(slots[i], null);
                UpdateFriendBoardLocation(slots[i], null);
                ShowSlotBaselineVisual(i, forceSnapshotRefresh: false);
            }
        }

        // assign new players to free slots (deterministic by playerId if enabled)
        foreach (var kv in available)
        {
            if (usedPlayers.Contains(kv.Key)) continue;
            var pick = kv.Value;
            var slotIndex = FindSlotForPlayer(pick.playerId);
            if (slotIndex < 0) break;

            _slotPlayerIds[slotIndex] = pick.playerId;
            _playerToSlot[pick.playerId] = slotIndex;
            usedPlayers.Add(pick.playerId);
            if (IsSlotInSyncRange(slotIndex))
            {
                if (slots[slotIndex].root != null)
                    slots[slotIndex].root.gameObject.SetActive(true);
                MarkSlotAsLiveVisual(slotIndex);
                EnsureRemotePlayer(slotIndex, pick.playerId);
                UpdateChestLocation(slots[slotIndex], pick);
                UpdateFriendBoardLocation(slots[slotIndex], pick);
                ApplyIfChanged(slotIndex, slots[slotIndex], pick, force: true);
            }
            else
            {
                UpdateChestLocation(slots[slotIndex], null);
                UpdateFriendBoardLocation(slots[slotIndex], null);
                ShowSlotBaselineVisual(slotIndex, forceSnapshotRefresh: true);
            }
        }

        if (clearEmptySlots)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                if (IsLocalSlotIndex(i)) continue;
                if (string.IsNullOrEmpty(_slotPlayerIds[i]))
                {
                    ShowSlotBaselineVisual(i, forceSnapshotRefresh: false);
                }
            }
        }
    }

    public void ApplyLobbyMembers(List<LobbyMemberStateDto> members)
    {
        if (slots == null || slots.Count == 0)
            return;

        EnsureSlotState();
        _lobbyModeActive = members != null && members.Count > 0;

        var previousKnownLocalId = _lastLocalPlayerId;
        var localId = GetLocalPlayerId();
        if (!string.IsNullOrEmpty(localId) &&
            !string.IsNullOrEmpty(previousKnownLocalId) &&
            !string.Equals(localId, previousKnownLocalId, StringComparison.Ordinal))
        {
            // Local profile changed (e.g. after progress reset) - drop stale slot/teleport state.
            _serverLocalSlotIndex = -1;
            _lastPreparedLocalSlotIndex = -1;
            _lastTeleportedSlotIndex = -2;
            _lastTeleportedPlayerId = null;
            _hasServerResolvedLocalSlot = false;
            _forceLocalRestoreOnNextResolve = true;
        }
        var previousServerLocalSlotIndex = _serverLocalSlotIndex;
        var resolvedServerLocalSlotIndex = -1;
        LobbyMemberStateDto localMember = null;
        if (members != null)
        {
            foreach (var m in members)
            {
                if (m == null || string.IsNullOrEmpty(m.playerId)) continue;
                if (!string.IsNullOrEmpty(localId) && m.playerId == localId)
                {
                    localMember = m;
                    break;
                }
            }

            if (localMember == null && !string.IsNullOrEmpty(_lastLocalPlayerId))
            {
                foreach (var m in members)
                {
                    if (m == null || string.IsNullOrEmpty(m.playerId)) continue;
                    if (m.playerId != _lastLocalPlayerId) continue;
                    localMember = m;
                    localId = m.playerId;
                    break;
                }
            }
        }

        if (localMember != null && localMember.slotIndex >= 0 && localMember.slotIndex < slots.Count)
            resolvedServerLocalSlotIndex = localMember.slotIndex;

        if (resolvedServerLocalSlotIndex >= 0)
            _serverLocalSlotIndex = resolvedServerLocalSlotIndex;
        else if (previousServerLocalSlotIndex >= 0 &&
                 previousServerLocalSlotIndex < slots.Count &&
                 !string.IsNullOrEmpty(localId) &&
                 string.Equals(localId, previousKnownLocalId, StringComparison.Ordinal))
            _serverLocalSlotIndex = previousServerLocalSlotIndex;
        else
            _serverLocalSlotIndex = -1;

        if (!string.IsNullOrEmpty(localId))
            _lastLocalPlayerId = localId;

        var hasResolvedLocalSlot = _serverLocalSlotIndex >= 0 && _serverLocalSlotIndex < slots.Count;
        if (!hasResolvedLocalSlot)
        {
            // Avoid switching every slot into remote mode when local player id is still bootstrapping.
            _hasServerResolvedLocalSlot = false;
            if (debugLogs)
                Debug.LogWarning("[Lobby] local slot unresolved, skip apply.");
            return;
        }
        _hasServerResolvedLocalSlot = true;

        if (debugLogs && localMember != null)
            Debug.Log($"[Lobby] local slotIndex={_serverLocalSlotIndex}");

        var remoteMembersCount = 0;
        if (members != null)
        {
            foreach (var m in members)
            {
                if (m == null || string.IsNullOrEmpty(m.playerId)) continue;
                if (localMember != null && m.playerId == localMember.playerId) continue;
                if (!m.isOnline) continue;
                remoteMembersCount++;
            }
        }
        var applyDistanceCulling = syncOnlyNearSlots && syncDistanceMeters > 0f && _didInitialFullLobbySync;

        var desiredBySlot = new Dictionary<int, LobbyMemberStateDto>();
        if (members != null)
        {
            foreach (var m in members)
            {
                if (m == null || string.IsNullOrEmpty(m.playerId)) continue;
                if (!string.Equals(m.playerId, localId, StringComparison.Ordinal) && !m.isOnline)
                    continue;
                var slotIndex = m.slotIndex;
                if (slotIndex < 0 || slotIndex >= slots.Count)
                    slotIndex = FindSlotForPlayer(m.playerId);
                if (slotIndex < 0 || slotIndex >= slots.Count) continue;

                if (IsLocalSlotIndex(slotIndex) && m.playerId != localId)
                    continue;

                if (!desiredBySlot.ContainsKey(slotIndex))
                    desiredBySlot.Add(slotIndex, m);
            }
        }

        for (int i = 0; i < slots.Count; i++)
        {
            if (IsLocalSlotIndex(i))
            {
                if (slots[i].root != null)
                    slots[i].root.gameObject.SetActive(true);

                DisableRemotePlayer(i);
                ApplySlotMode(i, false);
                var needsRestore = _forceLocalRestoreOnNextResolve || _lastPreparedLocalSlotIndex != i;

                if (needsRestore)
                {
                    if (RestoreLocalSlotFromSave(i))
                    {
                        _lastPreparedLocalSlotIndex = i;
                        _forceLocalRestoreOnNextResolve = false;
                    }
                    else
                    {
                        _lastPreparedLocalSlotIndex = -1;
                        _forceLocalRestoreOnNextResolve = true;
                    }
                }

                if (!string.IsNullOrEmpty(localId))
                {
                    _slotPlayerIds[i] = localId;
                    _playerToSlot[localId] = i;
                }
                continue;
            }
            else
            {
                ApplySlotMode(i, true);
                if (_lastPreparedLocalSlotIndex == i)
                    _lastPreparedLocalSlotIndex = -1;
            }

            if (desiredBySlot.TryGetValue(i, out var member))
            {
                var previousPlayerId = _slotPlayerIds[i];
                if (!string.IsNullOrEmpty(previousPlayerId) &&
                    !string.Equals(previousPlayerId, member.playerId, StringComparison.Ordinal))
                {
                    _playerToSlot.Remove(previousPlayerId);
                }

                _slotPlayerIds[i] = member.playerId;
                _playerToSlot[member.playerId] = i;
                EnsureRemotePlayer(i, member.playerId);
                ApplyRemotePositions(i, member.positions);
                ApplyRemoteHolding(i, member.hand);
                UpdateFriendBoardLobby(slots[i], member);

                var inSyncRange = !applyDistanceCulling || IsSlotInSyncRange(i);
                if (!inSyncRange)
                {
                    UpdateChestLobby(slots[i], null);
                    if (!renderRemotePlayersOutsideBaseSyncRange)
                        UpdateFriendBoardLobby(slots[i], null);
                    ShowSlotBaselineVisual(i, forceSnapshotRefresh: true, keepRemotePlayer: renderRemotePlayersOutsideBaseSyncRange);
                    continue;
                }

                if (slots[i].root != null)
                    slots[i].root.gameObject.SetActive(true);

                MarkSlotAsLiveVisual(i);
                UpdateChestLobby(slots[i], member);
                ApplyIfChangedLobby(i, slots[i], member);
            }
            else
            {
                var hadPlayer = !string.IsNullOrEmpty(_slotPlayerIds[i]);
                if (!string.IsNullOrEmpty(_slotPlayerIds[i]))
                {
                    _playerToSlot.Remove(_slotPlayerIds[i]);
                    _slotPlayerIds[i] = null;
                    _slotUpdatedAt[i] = null;
                }
                UpdateChestLobby(slots[i], null);
                UpdateFriendBoardLobby(slots[i], null);
                if (clearEmptySlots && (hadPlayer || _slotUpdatedAt[i] != EmptySlotMarker))
                    _slotUpdatedAt[i] = EmptySlotMarker;
                ShowSlotBaselineVisual(i, forceSnapshotRefresh: false);
            }
        }

        // Debug clone is only useful for solo local testing; disable it when real remote members exist.
        if (remoteMembersCount == 0)
            ApplyTestClone(localMember);
        else
            _didInitialFullLobbySync = true;
        if (localMember != null)
            TryTeleportLocalPlayer();
    }


    private void SetFieldManagersForSlot(RemoteBaseSlot slot, bool enabled)
    {
        if (slot == null || slot.root == null) return;
        foreach (var fm in slot.root.GetComponentsInChildren<FieldManager>(true))
            fm.enabled = enabled;
        if (enabled)
        {
            foreach (var fm in slot.root.GetComponentsInChildren<FieldManager>(true))
                fm.EnsureInitialized();
        }
    }

    private void SetFieldsRemoteMode(RemoteBaseSlot slot, bool remote)
    {
        if (slot == null || slot.root == null) return;
        foreach (var field in slot.root.GetComponentsInChildren<Field>(true))
            field.SetRemoteMode(remote);
    }

    private void ApplySlotMode(int slotIndex, bool remote)
    {
        if (slots == null || slotIndex < 0 || slotIndex >= slots.Count) return;
        if (_slotModeState == null || _slotModeState.Length != slots.Count) return;
        if (_slotModeState[slotIndex] == (remote ? 1 : 0)) return;

        var slot = slots[slotIndex];
        if (slot == null || slot.root == null) return;

        foreach (var conveyor in slot.root.GetComponentsInChildren<Conveyor>(true))
            conveyor.SetRemoteMode(remote);
        foreach (var bigPet in slot.root.GetComponentsInChildren<BigPetPoint>(true))
            bigPet.SetRemoteMode(remote);
        SetFieldManagersForSlot(slot, !remote);
        SetFieldsRemoteMode(slot, remote);

        _slotModeState[slotIndex] = remote ? 1 : 0;
    }

    private void UnlockCellsForSlot(RemoteBaseSlot slot)
    {
        if (slot == null || slot.root == null) return;
        foreach (var cell in slot.root.GetComponentsInChildren<FieldCell>(true))
            cell.LockCell(false);
    }

    private bool RestoreLocalSlotFromSave(int slotIndex)
    {
        if (slots == null || slotIndex < 0 || slotIndex >= slots.Count || G.Save == null)
            return false;

        if (!G.Save.IsReady)
        {
            QueueLocalRestoreWhenSaveReady(slotIndex);
            return false;
        }

        var slot = slots[slotIndex];
        if (slot == null || slot.root == null)
            return false;

        UnlockCellsForSlot(slot);

        // Slot can previously be used as remote and cleared to baseline.
        // Force a local save re-apply when ownership switches back to local.
        foreach (var manager in slot.root.GetComponentsInChildren<FieldManager>(true))
            manager.ReloadFromSave();

        foreach (var conveyor in slot.root.GetComponentsInChildren<Conveyor>(true))
            conveyor.SetRemoteMode(false);

        foreach (var bigPet in slot.root.GetComponentsInChildren<BigPetPoint>(true))
            bigPet.SetRemoteMode(false);

        return true;
    }

    private void QueueLocalRestoreWhenSaveReady(int slotIndex)
    {
        _pendingLocalRestoreSlotIndex = slotIndex;
        if (_waitForSaveReadyRoutine == null)
            _waitForSaveReadyRoutine = StartCoroutine(WaitForSaveReadyAndRestore());
    }

    private IEnumerator WaitForSaveReadyAndRestore()
    {
        while (G.Save == null || !G.Save.IsReady)
            yield return null;

        _waitForSaveReadyRoutine = null;
        var slotIndex = _pendingLocalRestoreSlotIndex;
        _pendingLocalRestoreSlotIndex = -1;

        if (slotIndex < 0 || slots == null || slotIndex >= slots.Count)
            yield break;

        if (RestoreLocalSlotFromSave(slotIndex))
        {
            _lastPreparedLocalSlotIndex = slotIndex;
            _forceLocalRestoreOnNextResolve = false;
        }
        else
        {
            _lastPreparedLocalSlotIndex = -1;
            _forceLocalRestoreOnNextResolve = true;
        }
    }

    private bool IsSlotInSyncRange(int slotIndex)
    {
        if (!syncOnlyNearSlots || syncDistanceMeters <= 0f)
            return true;

        if (IsLocalSlotIndex(slotIndex))
            return true;

        if (slots == null || slotIndex < 0 || slotIndex >= slots.Count)
            return false;

        if (_slotWithinSyncRange == null || slotIndex >= _slotWithinSyncRange.Length)
            return true;

        var slot = slots[slotIndex];
        if (slot == null || slot.root == null)
        {
            _slotWithinSyncRange[slotIndex] = false;
            return false;
        }

        var player = G.Player;
        if (player == null)
        {
            _slotWithinSyncRange[slotIndex] = true;
            return true;
        }

        var anchor = slot.teleportTarget != null ? slot.teleportTarget.position : slot.root.position;
        var sqrDist = (anchor - player.transform.position).sqrMagnitude;

        var enterDist = Mathf.Max(0.5f, syncDistanceMeters);
        var exitDist = Mathf.Max(enterDist, enterDist + Mathf.Max(0f, syncDistanceHysteresisMeters));
        var wasNear = _slotWithinSyncRange[slotIndex];
        var threshold = wasNear ? exitDist : enterDist;
        var near = sqrDist <= threshold * threshold;
        _slotWithinSyncRange[slotIndex] = near;
        return near;
    }

    private void ShowSlotBaselineVisual(int slotIndex, bool forceSnapshotRefresh, bool keepRemotePlayer = false)
    {
        if (slots == null || slotIndex < 0 || slotIndex >= slots.Count)
            return;
        if (IsLocalSlotIndex(slotIndex))
            return;

        var slot = slots[slotIndex];
        if (slot == null || slot.root == null)
            return;

        if (slot.root != null)
            slot.root.gameObject.SetActive(true);

        ApplySlotMode(slotIndex, true);
        if (!keepRemotePlayer)
            DisableRemotePlayer(slotIndex);

        if (_slotWithinSyncRange != null && slotIndex < _slotWithinSyncRange.Length)
            _slotWithinSyncRange[slotIndex] = false;

        var alreadyBaseline = _slotIsBaselineVisual != null &&
                              slotIndex < _slotIsBaselineVisual.Length &&
                              _slotIsBaselineVisual[slotIndex];
        if (forceSnapshotRefresh)
            _slotUpdatedAt[slotIndex] = null;

        if (alreadyBaseline)
            return;

        ClearSlot(slot, disableRoot: false);
        ApplySlotBaselineState(slotIndex, slot);

        if (_slotIsBaselineVisual != null && slotIndex < _slotIsBaselineVisual.Length)
            _slotIsBaselineVisual[slotIndex] = true;
    }

    private void MarkSlotAsLiveVisual(int slotIndex)
    {
        if (_slotIsBaselineVisual == null || slotIndex < 0 || slotIndex >= _slotIsBaselineVisual.Length)
            return;
        _slotIsBaselineVisual[slotIndex] = false;
    }

    private void ApplySlotBaselineState(int slotIndex, RemoteBaseSlot slot)
    {
        if (slot == null || slot.root == null)
            return;

        if (TryGetFallbackRemoteSnapshot(slotIndex, slot, out var fallbackSnapshot))
        {
            ApplySnapshotToSlot(slotIndex, slot, fallbackSnapshot);
            return;
        }

        foreach (var conveyor in slot.root.GetComponentsInChildren<Conveyor>(true))
        {
            conveyor.SetRemoteMode(true);
            conveyor.ApplyRemoteLevel(BaselineConveyorLevel);
            conveyor.ClearSpawnedEggs();
        }

        foreach (var bigPet in slot.root.GetComponentsInChildren<BigPetPoint>(true))
            bigPet.ApplyRemoteDefaultState(BaselineBigPetId, BaselineBigPetLevel, BaselineBigPetXp, BaselineBigPetPurchased);

        foreach (var field in GetFields(slot))
            field.SetUnblockedVisual(false);
    }

    private bool TryGetFallbackRemoteSnapshot(int slotIndex, RemoteBaseSlot slot, out BaseSnapshotDto snapshot)
    {
        snapshot = null;
        // Empty remote islands are part of the world presentation in both online
        // and offline modes. Real player snapshots replace this cached fallback.
        if (slotIndex < 0 || slot == null || slot.root == null)
            return false;

        EnsureSlotState();
        var localSlotIndex = GetLocalSlotIndex();
        if (_fallbackSnapshotLocalSlotIndex != localSlotIndex)
        {
            if (_slotFallbackSnapshots != null)
                Array.Clear(_slotFallbackSnapshots, 0, _slotFallbackSnapshots.Length);
            _fallbackSnapshotLocalSlotIndex = localSlotIndex;
        }

        if (_slotFallbackSnapshots != null &&
            slotIndex < _slotFallbackSnapshots.Length &&
            _slotFallbackSnapshots[slotIndex] != null)
        {
            snapshot = _slotFallbackSnapshots[slotIndex];
            return true;
        }

        snapshot = BuildFallbackRemoteSnapshot(slotIndex, slot);
        if (_slotFallbackSnapshots != null && slotIndex < _slotFallbackSnapshots.Length)
            _slotFallbackSnapshots[slotIndex] = snapshot;

        return snapshot != null;
    }

    private BaseSnapshotDto BuildFallbackRemoteSnapshot(int slotIndex, RemoteBaseSlot slot)
    {
        EnsureFieldIds(slot);

        var rng = new System.Random(BuildFallbackSeed(slotIndex, slot));
        var showcaseProgress = IsShowcaseFallbackSlot(slotIndex);
        var fields = GetFields(slot)
            .Where(field => field != null)
            .OrderBy(field => field.ID)
            .ToList();

        if (fields.Count == 0)
            return null;

        var boughtFieldIds = PickFallbackBoughtFieldIds(fields, rng, showcaseProgress);
        var availableCells = CollectFallbackCells(slot, fields, boughtFieldIds);
        var eggs = G.Storage != null ? GetNamedPrefabs(G.Storage.GetAllEggPrefabs()) : new List<Egg>();
        var pets = G.Storage != null ? GetNamedPrefabs(G.Storage.GetAllPetPrefabs()) : new List<Brainrot>();
        if (showcaseProgress)
            pets = pets.OrderByDescending(pet => pet.Data.StartIncome).ToList();

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var cells = new List<CellSnapshotDto>();
        if (availableCells.Count > 0 && (eggs.Count > 0 || pets.Count > 0))
        {
            var minCells = showcaseProgress
                ? Mathf.Clamp(Mathf.Max(randomRemoteMinCells, 10), 0, Mathf.Min(availableCells.Count, 18))
                : Mathf.Clamp(Mathf.Min(randomRemoteMinCells, randomRemoteMaxCells), 0, availableCells.Count);
            var maxCells = showcaseProgress
                ? Mathf.Clamp(Mathf.Max(minCells, 16), minCells, Mathf.Min(availableCells.Count, 22))
                : Mathf.Clamp(Mathf.Max(randomRemoteMinCells, randomRemoteMaxCells), minCells, availableCells.Count);
            var targetCells = maxCells > minCells ? rng.Next(minCells, maxCells + 1) : minCells;
            if (targetCells == 0)
                targetCells = 1;

            for (var i = 0; i < targetCells && availableCells.Count > 0; i++)
            {
                var cellIndex = rng.Next(availableCells.Count);
                var cell = availableCells[cellIndex];
                availableCells.RemoveAt(cellIndex);

                var eggChance = showcaseProgress ? Math.Min(0.15d, randomRemoteEggChance) : randomRemoteEggChance;
                var useEgg = eggs.Count > 0 && (pets.Count == 0 || rng.NextDouble() < eggChance);
                if (useEgg)
                {
                    var prefab = eggs[rng.Next(eggs.Count)];
                    var hatchDelay = Mathf.Max(60, prefab.Data.SecondsToHatching);
                    cells.Add(new CellSnapshotDto
                    {
                        cell = cell.Id,
                        kind = "egg",
                        id = prefab.Name,
                        dinamic = CreateFallbackDinamic(rng, prefab.Data.Price, showcaseProgress),
                        hatchingTimestamp = now + rng.Next(60, hatchDelay + 1)
                    });
                }
                else
                {
                    var petPoolCount = showcaseProgress
                        ? Mathf.Clamp(Mathf.CeilToInt(pets.Count * 0.4f), 1, pets.Count)
                        : pets.Count;
                    var prefab = pets[rng.Next(petPoolCount)];
                    cells.Add(new CellSnapshotDto
                    {
                        cell = cell.Id,
                        kind = "brainrot",
                        id = prefab.Name,
                        dinamic = CreateFallbackDinamic(rng, prefab.Data.StartIncome, showcaseProgress),
                        incomeLastTime = now - rng.Next(60, 7200)
                    });
                }
            }
        }

        cells.Sort((a, b) => string.CompareOrdinal(a.cell, b.cell));
        var bigPet = BuildFallbackBigPet(rng, showcaseProgress, pets.Count);
        var stats = BuildFallbackPlayerStats(cells, bigPet);

        return new BaseSnapshotDto
        {
            updatedAt = $"fallback:{ShowcaseFallbackVersion}:{randomRemoteProgressSeed}:{slotIndex}",
            conveyor = new ConveyorDto { lvl = GetFallbackConveyorLevel(slot, rng, showcaseProgress) },
            bigPet = bigPet,
            land = new LandDto { boughtCells = boughtFieldIds.OrderBy(id => id).ToList() },
            cells = cells,
            animalsOnCells = new List<AnimalOnCellDto>(),
            playerStats = stats
        };
    }

    private int BuildFallbackSeed(int slotIndex, RemoteBaseSlot slot)
    {
        unchecked
        {
            var seed = randomRemoteProgressSeed;
            seed = seed * 397 ^ slotIndex;
            var raw = slot != null && !string.IsNullOrEmpty(slot.name)
                ? slot.name
                : slot?.root != null ? slot.root.name : string.Empty;
            for (var i = 0; i < raw.Length; i++)
                seed = seed * 31 + raw[i];
            return seed;
        }
    }

    private bool IsShowcaseFallbackSlot(int slotIndex)
    {
        if (slots == null || slotIndex < 0 || slotIndex >= slots.Count || IsLocalSlotIndex(slotIndex))
            return false;

        // Pick two deterministic remote neighbours regardless of which physical
        // slot is currently owned by the local player.
        var remoteOrdinal = 0;
        for (var i = 0; i < slots.Count; i++)
        {
            if (IsLocalSlotIndex(i))
                continue;
            if (i == slotIndex)
                return remoteOrdinal == 1 || remoteOrdinal == 3;
            remoteOrdinal++;
        }

        return false;
    }

    private HashSet<int> PickFallbackBoughtFieldIds(List<Field> fields, System.Random rng, bool showcaseProgress)
    {
        var bought = new HashSet<int>();
        for (var i = 0; i < fields.Count; i++)
        {
            if (fields[i].DefaultUnblocked)
                bought.Add(fields[i].ID);
        }

        var targetFieldCount = Mathf.Clamp(
            showcaseProgress
                ? Mathf.CeilToInt(fields.Count * Mathf.Lerp(0.88f, 1f, (float)rng.NextDouble()))
                : Mathf.CeilToInt(fields.Count * Mathf.Lerp(0.3f, 0.85f, (float)rng.NextDouble())),
            1,
            fields.Count);
        targetFieldCount = Mathf.Max(targetFieldCount, bought.Count);

        while (bought.Count < targetFieldCount)
            bought.Add(fields[rng.Next(fields.Count)].ID);

        return bought;
    }

    private List<FieldCell> CollectFallbackCells(RemoteBaseSlot slot, List<Field> fields, HashSet<int> boughtFieldIds)
    {
        var result = new List<FieldCell>();
        var seen = new HashSet<string>();
        foreach (var cell in GetCells(slot))
        {
            if (cell == null || string.IsNullOrEmpty(cell.Id) || !seen.Add(cell.Id))
                continue;

            if (!TryGetFieldIdForCell(cell, fields, out var fieldId) || !boughtFieldIds.Contains(fieldId))
                continue;

            result.Add(cell);
        }

        return result;
    }

    private static bool TryGetFieldIdForCell(FieldCell cell, List<Field> fields, out int fieldId)
    {
        fieldId = -1;
        if (cell == null || fields == null)
            return false;

        for (var i = 0; i < fields.Count; i++)
        {
            var field = fields[i];
            if (field == null)
                continue;

            if (cell.transform == field.transform || cell.transform.IsChildOf(field.transform))
            {
                fieldId = field.ID;
                return true;
            }
        }

        return false;
    }

    private int GetFallbackConveyorLevel(RemoteBaseSlot slot, System.Random rng, bool showcaseProgress)
    {
        var maxLevel = Mathf.Max(0, randomRemoteMaxConveyorLevel);
        var conveyor = slot.root.GetComponentInChildren<Conveyor>(true);
        if (conveyor != null)
            maxLevel = showcaseProgress
                ? conveyor.MaxLevelIndex
                : Mathf.Min(maxLevel, conveyor.MaxLevelIndex);

        if (maxLevel <= 0)
            return 0;

        var minLevel = showcaseProgress ? Mathf.Max(0, maxLevel - 1) : 0;
        return rng.Next(minLevel, maxLevel + 1);
    }

    private BigPetDto BuildFallbackBigPet(System.Random rng, bool showcaseProgress, int basePetCount)
    {
        var purchased = showcaseProgress ||
                        (randomRemoteBigPetProgress && rng.NextDouble() < 0.65d);
        var firstElementalLevel = Mathf.Max(1, basePetCount) * 5 + 1;
        var maxLevel = showcaseProgress
            ? Mathf.Max(randomRemoteMaxBigPetLevel, firstElementalLevel + 55)
            : Mathf.Max(1, randomRemoteMaxBigPetLevel);
        var minLevel = showcaseProgress
            ? Mathf.Min(maxLevel, firstElementalLevel + 4)
            : 1;
        var lvl = purchased ? rng.Next(minLevel, maxLevel + 1) : BaselineBigPetLevel;
        var maxVariantId = Mathf.Max(0, (lvl - 1) / 5);
        var petId = purchased
            ? showcaseProgress ? Mathf.Max(0, maxVariantId - rng.Next(0, 3)) : rng.Next(0, maxVariantId + 1)
            : BaselineBigPetId;
        var income = purchased ? Mathf.Max(1, lvl) * 25f : 0f;

        return new BigPetDto
        {
            id = petId,
            lvl = lvl,
            xp = purchased ? rng.Next(0, 250) : BaselineBigPetXp,
            income = income,
            purchased = purchased
        };
    }

    private PlayerPublicStatsDto BuildFallbackPlayerStats(List<CellSnapshotDto> cells, BigPetDto bigPet)
    {
        var totalPetsIncome = 0d;
        var bestPetIncome = 0d;
        var totalHatched = 0;

        if (cells != null)
        {
            for (var i = 0; i < cells.Count; i++)
            {
                var cell = cells[i];
                if (cell == null || cell.kind != "brainrot")
                    continue;

                totalHatched++;
                var income = Math.Max(0d, cell.dinamic.ResultIncome);
                totalPetsIncome += income;
                if (income > bestPetIncome)
                    bestPetIncome = income;
            }
        }

        return new PlayerPublicStatsDto
        {
            totalHatched = totalHatched,
            petsIncomePerSec = totalPetsIncome,
            bestPetIncomePerSec = bestPetIncome,
            bigPetIncomePerSec = bigPet != null && bigPet.purchased ? Math.Max(0d, bigPet.income) : 0d
        };
    }

    private BrainrotDinamicData CreateFallbackDinamic(System.Random rng, double baseIncome, bool showcaseProgress)
    {
        var element = PickFallbackElement(rng, showcaseProgress);
        var minWeight = showcaseProgress ? 3.4f : 1f;
        var weightRange = showcaseProgress ? 2.1f : 4f;
        var weight = Mathf.Round((minWeight + (float)rng.NextDouble() * weightRange) * 10f) / 10f;
        var multiplier = G.Elements != null ? G.Elements.GetMultiplaer(element) : 1f;

        return new BrainrotDinamicData
        {
            ElementType = element,
            WeightMultiplier = weight,
            ResultIncome = Math.Round(Math.Max(0d, baseIncome) * multiplier * (weight / 2f))
        };
    }

    private static ElementType PickFallbackElement(System.Random rng, bool showcaseProgress)
    {
        if (rng.NextDouble() < (showcaseProgress ? 0.08d : 0.45d))
            return ElementType.NoElement;

        var elements = new[]
        {
            ElementType.Gold,
            ElementType.Diamond,
            ElementType.Electric,
            ElementType.Fire
        };
        return elements[rng.Next(elements.Length)];
    }

    private static List<T> GetNamedPrefabs<T>(IReadOnlyList<T> prefabs) where T : InventoryItem
    {
        var result = new List<T>();
        if (prefabs == null)
            return result;

        for (var i = 0; i < prefabs.Count; i++)
        {
            var prefab = prefabs[i];
            if (prefab != null && !string.IsNullOrEmpty(prefab.Name))
                result.Add(prefab);
        }

        return result;
    }


    private void ApplyTestClone(LobbyMemberStateDto localMember)
    {
        if (!Application.isEditor) return;
        if (!testCloneLocalToRandomSlot || localMember == null) return;
        var baseData = localMember.baseData;
        if (baseData == null)
        {
            var snapshotSync = FindAnyObjectByType<ZooBaseSnapshotSync>();
            var json = snapshotSync != null ? snapshotSync.BuildSnapshotJson() : null;
            baseData = TryParseSnapshot(json);
        }
        if (baseData == null) { if (debugLogs) Debug.Log("[Lobby] test clone no baseData"); return; }
        if (debugLogs) Debug.Log("[Lobby] test clone apply");
        var localIdx = GetLocalSlotIndex();
        int target = testCloneSlotIndex;
        if (target < 0 || target >= slots.Count || target == localIdx)
        {
            target = -1;
            for (int i = 0; i < slots.Count; i++)
            {
                if (i == localIdx) continue;
                target = i; break;
            }
        }
        if (debugLogs) Debug.Log($"[Lobby] test clone target={target}");
        if (target < 0 || target >= slots.Count || target == localIdx) return;

        var slot = slots[target];
        if (slot == null || slot.root == null) return;
        slot.root.gameObject.SetActive(true);
        foreach (var conveyor in slot.root.GetComponentsInChildren<Conveyor>(true))
            conveyor.SetRemoteMode(true);
        foreach (var bigPet in slot.root.GetComponentsInChildren<BigPetPoint>(true))
            bigPet.SetRemoteMode(true);
        SetFieldManagersForSlot(slot, false);
        SetFieldsRemoteMode(slot, true);

        ApplySnapshotToSlot(slot, baseData);
    }


    private BaseSnapshotDto TryParseSnapshot(string json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        try { return Newtonsoft.Json.JsonConvert.DeserializeObject<BaseSnapshotDto>(json); }
        catch { return null; }
    }

    private void UpdateChestLocation(RemoteBaseSlot slot, ZooLocationItem loc)
    {
        if (slot == null || slot.chest == null) return;
        if (loc == null)
        {
            slot.chest.SetRemoteTarget(null, false);
            return;
        }

        slot.chest.SetRemoteTarget(loc.friendCode, loc.isOnline);
    }

    private void UpdateChestLobby(RemoteBaseSlot slot, LobbyMemberStateDto member)
    {
        if (slot == null || slot.chest == null) return;
        if (member == null)
        {
            slot.chest.SetRemoteTarget(null, false);
            return;
        }

        slot.chest.SetRemoteTarget(member.friendCode, member.isOnline);
    }

    private void UpdateFriendBoardLocation(RemoteBaseSlot slot, ZooLocationItem loc)
    {
        if (slot == null) return;
        var board = GetBoardForSlot(slot);
        if (board == null) return;
        if (loc == null)
        {
            board.SetRemote(null, null, false, false);
            return;
        }

        board.SetRemoteWithId(loc.playerId, loc.friendCode, loc.displayName, loc.isFriend, loc.isOnline, ExtractStats(loc.baseData));
    }

    private void UpdateFriendBoardLobby(RemoteBaseSlot slot, LobbyMemberStateDto member)
    {
        if (slot == null) return;
        var board = GetBoardForSlot(slot);
        if (board == null) return;
        if (member == null)
        {
            board.SetRemote(null, null, false, false);
            return;
        }

        // If member is present in lobby state, treat as online even when backend flag is stale.
        var effectiveOnline = member.isOnline || !string.IsNullOrEmpty(member.playerId);
        board.SetRemoteWithId(member.playerId, member.friendCode, member.displayName, member.isFriend, effectiveOnline, ExtractStats(member.baseData));
    }

    private static PlayerPublicStatsDto ExtractStats(BaseSnapshotDto snapshot)
    {
        return snapshot != null ? snapshot.playerStats : null;
    }

    private void QueueSnapshotApply(int slotIndex, RemoteBaseSlot slot, BaseSnapshotDto snapshot)
    {
        if (slot == null || slot.root == null)
            return;
        if (slots == null || slotIndex < 0 || slotIndex >= slots.Count)
            return;

        if (!smoothSnapshotApply)
        {
            ApplySnapshotToSlot(slotIndex, slot, snapshot);
            _slotHadSnapshot[slotIndex] = snapshot != null;
            return;
        }

        _slotSnapshotApplyPending[slotIndex] = snapshot;
        if (_slotSnapshotApplyRoutines[slotIndex] == null)
            _slotSnapshotApplyRoutines[slotIndex] = StartCoroutine(ApplySnapshotWorker(slotIndex, slot));
    }

    private IEnumerator ApplySnapshotWorker(int slotIndex, RemoteBaseSlot slot)
    {
        while (true)
        {
            var snapshot = _slotSnapshotApplyPending[slotIndex];
            _slotSnapshotApplyPending[slotIndex] = null;

            var skipClear = !_slotHadSnapshot[slotIndex] && snapshot != null;
            yield return ApplySnapshotToSlotAsync(slotIndex, slot, snapshot, skipClear);
            _slotHadSnapshot[slotIndex] = snapshot != null;

            if (_slotSnapshotApplyPending[slotIndex] == null)
                break;

            yield return null;
        }

        _slotSnapshotApplyRoutines[slotIndex] = null;
    }

    private IEnumerator ApplySnapshotToSlotAsync(int slotIndex, RemoteBaseSlot slot, BaseSnapshotDto snapshot, bool skipClear)
    {
        if (slot == null || slot.root == null || IsLocalSlot(slot))
            yield break;

        slot.root.gameObject.SetActive(true);

        EnsureFieldIds(slot);

        if (snapshot == null)
        {
            yield return ClearSlotAsync(slotIndex, slot, disableRoot: false);
            yield break;
        }

        var canApplyIncremental =
            incrementalSnapshotApply &&
            !skipClear &&
            _slotSnapshotCaches != null &&
            slotIndex >= 0 &&
            slotIndex < _slotSnapshotCaches.Length &&
            _slotSnapshotCaches[slotIndex] != null;

        if (canApplyIncremental)
        {
            yield return ApplySnapshotDeltaAsync(slotIndex, slot, snapshot);
            yield break;
        }

        if (!skipClear)
            yield return ClearSlotAsync(slotIndex, slot, disableRoot: false);

        ApplyConveyor(slot, snapshot);
        ApplyBigPet(slot, snapshot);

        var opsBudget = Mathf.Max(1, snapshotOpsPerFrame);
        var ops = 0;

        if (slot.applyLand && snapshot.land != null && snapshot.land.boughtCells != null)
        {
            var land = new HashSet<int>(snapshot.land.boughtCells ?? new List<int>());
            foreach (var field in GetFields(slot))
            {
                field.SetUnblockedVisual(land.Contains(field.ID));
                if (++ops >= opsBudget)
                {
                    ops = 0;
                    yield return null;
                }
            }
        }

        var cellMap = BuildCellMap(slot);
        var cells = BuildSnapshotCells(snapshot);
        foreach (var item in cells.Values)
        {
            if (item == null || string.IsNullOrEmpty(item.cell))
                continue;

            if (!cellMap.TryGetValue(item.cell, out var cell))
                continue;

            if (slot.lockCells)
                cell.LockCell(true);

            SpawnSnapshotCell(cell, item);
            if (++ops >= opsBudget)
            {
                ops = 0;
                yield return null;
            }
        }

        SetSlotSnapshotCache(slotIndex, BuildSnapshotCache(snapshot));
    }

    private IEnumerator ApplySnapshotDeltaAsync(int slotIndex, RemoteBaseSlot slot, BaseSnapshotDto snapshot)
    {
        var cache = GetOrCreateSlotSnapshotCache(slotIndex);
        ApplyConveyorDiff(slot, snapshot, cache);
        ApplyBigPetDiff(slot, snapshot, cache);

        var opsBudget = Mathf.Max(1, snapshotOpsPerFrame);
        var ops = 0;

        if (slot.applyLand && snapshot.land != null && snapshot.land.boughtCells != null)
        {
            var newLand = new HashSet<int>(snapshot.land.boughtCells ?? new List<int>());
            if (!cache.land.SetEquals(newLand))
            {
                foreach (var field in GetFields(slot))
                {
                    field.SetUnblockedVisual(newLand.Contains(field.ID));
                    if (++ops >= opsBudget)
                    {
                        ops = 0;
                        yield return null;
                    }
                }
                cache.land = newLand;
            }
        }

        var desiredCells = BuildSnapshotCells(snapshot);
        var desiredSignatures = BuildCellSignatures(desiredCells);
        var cellMap = BuildCellMap(slot);

        if (cache.cells.Count > 0)
        {
            var removed = new List<string>();
            foreach (var existing in cache.cells.Keys)
            {
                if (!desiredSignatures.ContainsKey(existing))
                    removed.Add(existing);
            }

            foreach (var cellId in removed)
            {
                if (cellMap.TryGetValue(cellId, out var cell))
                {
                    if (slot.lockCells)
                        cell.LockCell(true);
                    ClearCellActors(cell);
                }
                cache.cells.Remove(cellId);
                if (++ops >= opsBudget)
                {
                    ops = 0;
                    yield return null;
                }
            }
        }

        foreach (var kv in desiredCells)
        {
            var cellId = kv.Key;
            var item = kv.Value;
            if (item == null)
                continue;

            var sig = desiredSignatures[cellId];
            if (cache.cells.TryGetValue(cellId, out var oldSig) && oldSig == sig)
                continue;

            if (cellMap.TryGetValue(cellId, out var cell))
            {
                if (slot.lockCells)
                    cell.LockCell(true);
                ClearCellActors(cell);
                SpawnSnapshotCell(cell, item);
            }

            cache.cells[cellId] = sig;
            if (++ops >= opsBudget)
            {
                ops = 0;
                yield return null;
            }
        }
    }

    private IEnumerator ClearSlotAsync(int slotIndex, RemoteBaseSlot slot, bool disableRoot)
    {
        if (slot == null || slot.root == null || IsLocalSlot(slot))
            yield break;

        ResetSlotSnapshotCache(slotIndex);

        if (disableRoot)
            slot.root.gameObject.SetActive(false);

        foreach (var conveyor in slot.root.GetComponentsInChildren<Conveyor>(true))
            conveyor.ClearSpawnedEggs();

        var opsBudget = Mathf.Max(1, snapshotOpsPerFrame);
        var ops = 0;
        foreach (var cell in GetCells(slot))
        {
            if (slot.lockCells)
                cell.LockCell(true);

            ClearCellActors(cell);
            if (++ops >= opsBudget)
            {
                ops = 0;
                yield return null;
            }
        }
    }

    private void ApplySnapshotToSlot(int slotIndex, RemoteBaseSlot slot, BaseSnapshotDto snapshot)
    {
        if (slot == null || slot.root == null || IsLocalSlot(slot))
            return;

        slot.root.gameObject.SetActive(true);

        EnsureFieldIds(slot);

        if (snapshot == null)
        {
            ClearSlot(slot, disableRoot: false);
            ResetSlotSnapshotCache(slotIndex);
            return;
        }

        var canApplyIncremental =
            incrementalSnapshotApply &&
            _slotSnapshotCaches != null &&
            slotIndex >= 0 &&
            slotIndex < _slotSnapshotCaches.Length &&
            _slotSnapshotCaches[slotIndex] != null;

        if (canApplyIncremental)
        {
            ApplySnapshotDeltaImmediate(slotIndex, slot, snapshot);
            return;
        }

        ClearSlot(slot, disableRoot: false);

        ApplyConveyor(slot, snapshot);
        ApplyBigPet(slot, snapshot);

        if (slot.applyLand && snapshot.land != null && snapshot.land.boughtCells != null)
        {
            var land = new HashSet<int>(snapshot.land.boughtCells ?? new List<int>());
            foreach (var field in GetFields(slot))
                field.SetUnblockedVisual(land.Contains(field.ID));
        }

        var cellMap = BuildCellMap(slot);
        var cells = BuildSnapshotCells(snapshot);
        foreach (var item in cells.Values)
        {
            if (item == null || string.IsNullOrEmpty(item.cell))
                continue;

            if (!cellMap.TryGetValue(item.cell, out var cell))
                continue;

            if (slot.lockCells)
                cell.LockCell(true);
            SpawnSnapshotCell(cell, item);
        }

        SetSlotSnapshotCache(slotIndex, BuildSnapshotCache(snapshot));
    }

    private void ApplySnapshotToSlot(RemoteBaseSlot slot, BaseSnapshotDto snapshot)
    {
        var slotIndex = slots != null ? slots.IndexOf(slot) : -1;
        ApplySnapshotToSlot(slotIndex, slot, snapshot);
    }

    private void ApplySnapshotDeltaImmediate(int slotIndex, RemoteBaseSlot slot, BaseSnapshotDto snapshot)
    {
        var cache = GetOrCreateSlotSnapshotCache(slotIndex);
        ApplyConveyorDiff(slot, snapshot, cache);
        ApplyBigPetDiff(slot, snapshot, cache);

        if (slot.applyLand && snapshot.land != null && snapshot.land.boughtCells != null)
        {
            var newLand = new HashSet<int>(snapshot.land.boughtCells ?? new List<int>());
            if (!cache.land.SetEquals(newLand))
            {
                foreach (var field in GetFields(slot))
                    field.SetUnblockedVisual(newLand.Contains(field.ID));
                cache.land = newLand;
            }
        }

        var desiredCells = BuildSnapshotCells(snapshot);
        var desiredSignatures = BuildCellSignatures(desiredCells);
        var cellMap = BuildCellMap(slot);

        if (cache.cells.Count > 0)
        {
            var removed = new List<string>();
            foreach (var existing in cache.cells.Keys)
            {
                if (!desiredSignatures.ContainsKey(existing))
                    removed.Add(existing);
            }

            foreach (var cellId in removed)
            {
                if (cellMap.TryGetValue(cellId, out var cell))
                {
                    if (slot.lockCells)
                        cell.LockCell(true);
                    ClearCellActors(cell);
                }
                cache.cells.Remove(cellId);
            }
        }

        foreach (var kv in desiredCells)
        {
            var cellId = kv.Key;
            var item = kv.Value;
            if (item == null)
                continue;

            var sig = desiredSignatures[cellId];
            if (cache.cells.TryGetValue(cellId, out var oldSig) && oldSig == sig)
                continue;

            if (cellMap.TryGetValue(cellId, out var cell))
            {
                if (slot.lockCells)
                    cell.LockCell(true);
                ClearCellActors(cell);
                SpawnSnapshotCell(cell, item);
            }
            cache.cells[cellId] = sig;
        }
    }

    private void ApplyConveyorDiff(RemoteBaseSlot slot, BaseSnapshotDto snapshot, SlotSnapshotCache cache)
    {
        if (snapshot?.conveyor == null)
            return;

        if (cache.conveyorLevel == snapshot.conveyor.lvl)
            return;

        ApplyConveyor(slot, snapshot);
        cache.conveyorLevel = snapshot.conveyor.lvl;
    }

    private void ApplyBigPetDiff(RemoteBaseSlot slot, BaseSnapshotDto snapshot, SlotSnapshotCache cache)
    {
        if (snapshot?.bigPet == null)
            return;

        if (cache.bigPetId == snapshot.bigPet.id &&
            cache.bigPetLvl == snapshot.bigPet.lvl &&
            cache.bigPetXp == snapshot.bigPet.xp &&
            cache.bigPetPurchased == snapshot.bigPet.purchased)
            return;

        ApplyBigPet(slot, snapshot);
        cache.bigPetId = snapshot.bigPet.id;
        cache.bigPetLvl = snapshot.bigPet.lvl;
        cache.bigPetXp = snapshot.bigPet.xp;
        cache.bigPetPurchased = snapshot.bigPet.purchased;
    }

    private Dictionary<string, CellSnapshotDto> BuildSnapshotCells(BaseSnapshotDto snapshot)
    {
        var result = new Dictionary<string, CellSnapshotDto>();
        if (snapshot == null)
            return result;

        if (snapshot.cells != null && snapshot.cells.Count > 0)
        {
            foreach (var item in snapshot.cells)
            {
                if (item == null || string.IsNullOrEmpty(item.cell))
                    continue;
                if (result.ContainsKey(item.cell))
                    continue;
                result.Add(item.cell, item);
            }
            return result;
        }

        if (snapshot.animalsOnCells == null || snapshot.animalsOnCells.Count == 0)
            return result;

        foreach (var legacy in snapshot.animalsOnCells)
        {
            if (legacy == null || string.IsNullOrEmpty(legacy.cell))
                continue;
            if (result.ContainsKey(legacy.cell))
                continue;

            result.Add(legacy.cell, new CellSnapshotDto
            {
                cell = legacy.cell,
                kind = "brainrot",
                id = legacy.animalId
            });
        }

        return result;
    }

    private Dictionary<string, string> BuildCellSignatures(Dictionary<string, CellSnapshotDto> cells)
    {
        var result = new Dictionary<string, string>();
        if (cells == null || cells.Count == 0)
            return result;

        foreach (var kv in cells)
            result[kv.Key] = BuildCellSignature(kv.Value);

        return result;
    }

    private string BuildCellSignature(CellSnapshotDto item)
    {
        if (item == null)
            return string.Empty;

        var d = item.dinamic;
        return string.Concat(
            item.kind ?? string.Empty, "|",
            item.id ?? string.Empty, "|",
            item.hatchingTimestamp, "|",
            item.incomeLastTime, "|",
            (int)d.ElementType, "|",
            d.WeightMultiplier.ToString("R"), "|",
            d.ResultIncome.ToString("R"));
    }

    private void SpawnSnapshotCell(FieldCell cell, CellSnapshotDto item)
    {
        if (cell == null || item == null)
            return;

        if (item.kind == "egg")
            SpawnEgg(cell, item.id, item.dinamic, item.hatchingTimestamp);
        else if (item.kind == "brainrot")
            SpawnBrainrot(cell, item.id, item.dinamic, item.incomeLastTime);
    }

    private void ClearCellActors(FieldCell cell)
    {
        if (cell == null)
            return;

        var eggs = cell.GetComponentsInChildren<Egg>(true);
        foreach (var egg in eggs)
        {
            if (egg != null)
                Destroy(egg.gameObject);
        }

        var pets = cell.GetComponentsInChildren<Brainrot>(true);
        foreach (var pet in pets)
        {
            if (pet != null)
                Destroy(pet.gameObject);
        }
    }

    private SlotSnapshotCache BuildSnapshotCache(BaseSnapshotDto snapshot)
    {
        if (snapshot == null)
            return null;

        var cache = new SlotSnapshotCache();
        if (snapshot.conveyor != null)
            cache.conveyorLevel = snapshot.conveyor.lvl;
        if (snapshot.bigPet != null)
        {
            cache.bigPetId = snapshot.bigPet.id;
            cache.bigPetLvl = snapshot.bigPet.lvl;
            cache.bigPetXp = snapshot.bigPet.xp;
            cache.bigPetPurchased = snapshot.bigPet.purchased;
        }

        if (snapshot.land != null && snapshot.land.boughtCells != null)
            cache.land = new HashSet<int>(snapshot.land.boughtCells);

        var cells = BuildSnapshotCells(snapshot);
        cache.cells = BuildCellSignatures(cells);
        return cache;
    }

    private SlotSnapshotCache GetOrCreateSlotSnapshotCache(int slotIndex)
    {
        if (_slotSnapshotCaches == null || slotIndex < 0 || slotIndex >= _slotSnapshotCaches.Length)
            return new SlotSnapshotCache();

        if (_slotSnapshotCaches[slotIndex] == null)
            _slotSnapshotCaches[slotIndex] = new SlotSnapshotCache();

        return _slotSnapshotCaches[slotIndex];
    }

    private void SetSlotSnapshotCache(int slotIndex, SlotSnapshotCache cache)
    {
        if (_slotSnapshotCaches == null || slotIndex < 0 || slotIndex >= _slotSnapshotCaches.Length)
            return;
        _slotSnapshotCaches[slotIndex] = cache;
    }

    private void ResetSlotSnapshotCache(int slotIndex)
    {
        if (_slotSnapshotCaches == null || slotIndex < 0 || slotIndex >= _slotSnapshotCaches.Length)
            return;
        _slotSnapshotCaches[slotIndex] = null;
    }

    private void ApplyIfChanged(int slotIndex, RemoteBaseSlot slot, ZooLocationItem loc, bool force = false)
    {
        if (loc == null)
            return;

        var updatedAt = loc.baseData?.updatedAt ?? "";
        if (!force && _slotUpdatedAt[slotIndex] == updatedAt)
            return;

        _slotUpdatedAt[slotIndex] = updatedAt;
        QueueSnapshotApply(slotIndex, slot, loc.baseData);
    }

    private void ApplyIfChangedLobby(int slotIndex, RemoteBaseSlot slot, LobbyMemberStateDto member, bool force = false)
    {
        if (member == null)
            return;

        // Для лобби обновляем базу только когда реально изменилась база
        var updatedAt = BuildLobbySnapshotSignature(member);

        if (!force && _slotUpdatedAt[slotIndex] == updatedAt)
            return;

        _slotUpdatedAt[slotIndex] = updatedAt;
        QueueSnapshotApply(slotIndex, slot, member.baseData);
    }

    private string BuildLobbySnapshotSignature(LobbyMemberStateDto member)
    {
        if (member == null)
            return string.Empty;

        var raw = member.baseDataRaw;
        if (!string.IsNullOrEmpty(raw))
        {
            // Ignore snapshot.updatedAt noise to prevent full base reapply on every lobby tick.
            raw = NormalizeSnapshotRawForHash(raw);

            unchecked
            {
                var hash = 17;
                for (int i = 0; i < raw.Length; i++)
                    hash = hash * 31 + raw[i];
                return "raw:" + hash;
            }
        }

        return member.baseData?.updatedAt ?? string.Empty;
    }

    private string NormalizeSnapshotRawForHash(string raw)
    {
        if (string.IsNullOrEmpty(raw))
            return string.Empty;

        const string marker = "\"updatedAt\":";
        var markerIdx = raw.IndexOf(marker, StringComparison.Ordinal);
        if (markerIdx < 0)
            return raw;

        var quoteStart = raw.IndexOf('"', markerIdx + marker.Length);
        if (quoteStart < 0)
            return raw;
        var quoteEnd = raw.IndexOf('"', quoteStart + 1);
        if (quoteEnd <= quoteStart)
            return raw;

        return raw.Remove(quoteStart + 1, quoteEnd - quoteStart - 1);
    }

    private void EnsureSlotState()
    {
        if (slots == null) return;
        if (_slotPlayerIds == null || _slotPlayerIds.Length != slots.Count)
            _slotPlayerIds = new string[slots.Count];
        if (_slotUpdatedAt == null || _slotUpdatedAt.Length != slots.Count)
            _slotUpdatedAt = new string[slots.Count];
        if (_slotRemotePlayers == null || _slotRemotePlayers.Length != slots.Count)
            _slotRemotePlayers = new GameObject[slots.Count];
        if (_slotRemotePlayerOwnerIds == null || _slotRemotePlayerOwnerIds.Length != slots.Count)
            _slotRemotePlayerOwnerIds = new string[slots.Count];
        if (_slotRemoteBoards == null || _slotRemoteBoards.Length != slots.Count)
            _slotRemoteBoards = new RemoteFriendBoard[slots.Count];
        if (_slotSnapshotApplyRoutines == null || _slotSnapshotApplyRoutines.Length != slots.Count)
            _slotSnapshotApplyRoutines = new Coroutine[slots.Count];
        if (_slotSnapshotApplyPending == null || _slotSnapshotApplyPending.Length != slots.Count)
            _slotSnapshotApplyPending = new BaseSnapshotDto[slots.Count];
        if (_slotHadSnapshot == null || _slotHadSnapshot.Length != slots.Count)
            _slotHadSnapshot = new bool[slots.Count];
        if (_slotSnapshotCaches == null || _slotSnapshotCaches.Length != slots.Count)
            _slotSnapshotCaches = new SlotSnapshotCache[slots.Count];
        if (_slotFallbackSnapshots == null || _slotFallbackSnapshots.Length != slots.Count)
            _slotFallbackSnapshots = new BaseSnapshotDto[slots.Count];
        if (_slotModeState == null || _slotModeState.Length != slots.Count)
        {
            _slotModeState = new int[slots.Count];
            for (int i = 0; i < _slotModeState.Length; i++)
                _slotModeState[i] = -1;
        }
        if (_slotWithinSyncRange == null || _slotWithinSyncRange.Length != slots.Count)
        {
            _slotWithinSyncRange = new bool[slots.Count];
            for (int i = 0; i < _slotWithinSyncRange.Length; i++)
                _slotWithinSyncRange[i] = true;
        }
        if (_slotIsBaselineVisual == null || _slotIsBaselineVisual.Length != slots.Count)
            _slotIsBaselineVisual = new bool[slots.Count];
    }

    private int FindSlotForPlayer(string playerId)
    {
        if (slots == null || slots.Count == 0) return -1;
        if (string.IsNullOrEmpty(playerId)) return -1;

        if (_playerToSlot.TryGetValue(playerId, out var assigned) &&
            assigned >= 0 &&
            assigned < slots.Count &&
            !IsLocalSlotIndex(assigned))
            return assigned;

        if (!deterministicSlots)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                if (IsLocalSlotIndex(i)) continue;
                if (string.IsNullOrEmpty(_slotPlayerIds[i]))
                    return i;
            }
            return -1;
        }

        var preferred = PreferredSlotIndex(playerId);
        if (preferred < 0) return -1;

        if (!IsLocalSlotIndex(preferred) && string.IsNullOrEmpty(_slotPlayerIds[preferred]))
            return preferred;

        for (int offset = 1; offset < slots.Count; offset++)
        {
            var idx = (preferred + offset) % slots.Count;
            if (IsLocalSlotIndex(idx)) continue;
            if (string.IsNullOrEmpty(_slotPlayerIds[idx]))
                return idx;
        }

        return -1;
    }

    private int PreferredSlotIndex(string playerId)
    {
        if (string.IsNullOrEmpty(playerId) || slots == null || slots.Count == 0)
            return -1;

        unchecked
        {
            var hash = 23;
            for (int i = 0; i < playerId.Length; i++)
                hash = hash * 31 + playerId[i];
            if (hash < 0) hash = -hash;
            return hash % slots.Count;
        }
    }

    private void ClearSlot(RemoteBaseSlot slot)
    {
        ClearSlot(slot, disableEmptySlots);
    }

    private void ClearSlot(RemoteBaseSlot slot, bool disableRoot)
    {
        if (slot == null || slot.root == null || IsLocalSlot(slot))
            return;

        if (slots != null)
        {
            var slotIndex = slots.IndexOf(slot);
            if (slotIndex >= 0 && _slotSnapshotApplyRoutines != null && slotIndex < _slotSnapshotApplyRoutines.Length)
            {
                if (_slotSnapshotApplyRoutines[slotIndex] != null)
                    StopCoroutine(_slotSnapshotApplyRoutines[slotIndex]);
                _slotSnapshotApplyRoutines[slotIndex] = null;
                if (_slotSnapshotApplyPending != null && slotIndex < _slotSnapshotApplyPending.Length)
                    _slotSnapshotApplyPending[slotIndex] = null;
                if (_slotHadSnapshot != null && slotIndex < _slotHadSnapshot.Length)
                    _slotHadSnapshot[slotIndex] = false;
                ResetSlotSnapshotCache(slotIndex);
            }
        }

        if (disableRoot)
            slot.root.gameObject.SetActive(false);

        foreach (var conveyor in slot.root.GetComponentsInChildren<Conveyor>(true))
            conveyor.ClearSpawnedEggs();

        foreach (var cell in GetCells(slot))
        {
            if (slot.lockCells)
                cell.LockCell(true);

            var eggs = cell.GetComponentsInChildren<Egg>(true);
            foreach (var egg in eggs)
                Destroy(egg.gameObject);

            var pets = cell.GetComponentsInChildren<Brainrot>(true);
            foreach (var pet in pets)
                Destroy(pet.gameObject);
        }
    }

    private void ApplyConveyor(RemoteBaseSlot slot, BaseSnapshotDto snapshot)
    {
        if (snapshot.conveyor == null) return;
        var conveyor = slot.root.GetComponentInChildren<Conveyor>(true);
        if (conveyor == null) return;
        conveyor.ApplyRemoteLevel(snapshot.conveyor.lvl);
    }

    private void ApplyBigPet(RemoteBaseSlot slot, BaseSnapshotDto snapshot)
    {
        if (snapshot.bigPet == null) return;
        var bigPet = slot.root.GetComponentInChildren<BigPetPoint>(true);
        if (bigPet == null) return;
        bigPet.ApplyRemoteState(snapshot.bigPet.id, snapshot.bigPet.lvl, snapshot.bigPet.xp, snapshot.bigPet.purchased);
    }

    private void SpawnEgg(FieldCell cell, string id, BrainrotDinamicData dinamic, long hatchingTimestamp)
    {
        if (string.IsNullOrEmpty(id))
            return;

        if (G.Storage == null)
        {
            Debug.LogWarning("[RemoteBases] ItemPrefabStorage is not initialized.");
            return;
        }

        var prefab = G.Storage.GetEgg(id);
        if (prefab == null)
        {
            Debug.LogWarning($"[RemoteBases] Egg prefab not found: {id}");
            return;
        }

        var egg = Instantiate(prefab, cell.transform, false);
        AttachActorToCell(egg.transform, cell, Quaternion.identity);
        egg.SetData(dinamic);
        egg.OnInventoryAdd();
        AttachActorToCell(egg.transform, cell, Quaternion.identity);
        var info = egg.GetComponentInChildren<EggInfoUI>(true);
        if (info != null)
            info.SetRemoteView(true);
    }

    private void SpawnBrainrot(FieldCell cell, string id, BrainrotDinamicData dinamic, long incomeLastTime)
    {
        if (string.IsNullOrEmpty(id))
            return;

        if (G.Storage == null)
        {
            Debug.LogWarning("[RemoteBases] ItemPrefabStorage is not initialized.");
            return;
        }

        var prefab = G.Storage.GetPet(id);
        if (prefab == null)
        {
            Debug.LogWarning($"[RemoteBases] Brainrot prefab not found: {id}");
            return;
        }

        var pet = Instantiate(prefab, cell.transform, false);
        var localRotation = Quaternion.Euler(0f, 180f, 0f);
        AttachActorToCell(pet.transform, cell, localRotation);
        pet.Init(dinamic, null, incomeLastTime);
        AttachActorToCell(pet.transform, cell, localRotation);
        var info = pet.GetComponentInChildren<BrainrotInfoUI>(true);
        if (info != null)
            info.SetRemoteView(true);
    }

    private static void AttachActorToCell(Transform actor, FieldCell cell, Quaternion localRotation)
    {
        if (actor == null || cell == null)
            return;

        actor.SetParent(cell.transform, false);
        actor.localPosition = Vector3.zero;
        actor.localRotation = localRotation;
    }

    private void EnsureFieldIds(RemoteBaseSlot slot)
    {
        var fieldsRoot = slot.fieldsRoot != null ? slot.fieldsRoot : slot.root;
        if (fieldsRoot == null)
            return;

        int fieldId = 0;
        for (int i = 0; i < fieldsRoot.childCount; i++)
        {
            var row = fieldsRoot.GetChild(i);
            var fields = row.GetComponentsInChildren<Field>(true).ToList();
            foreach (var field in fields)
            {
                field.AssignIdsForRemote(fieldId++);
            }
        }
    }

    private List<Field> GetFields(RemoteBaseSlot slot)
    {
        var root = slot.fieldsRoot != null ? slot.fieldsRoot : slot.root;
        if (root == null) return new List<Field>();
        return root.GetComponentsInChildren<Field>(true).ToList();
    }

    private List<FieldCell> GetCells(RemoteBaseSlot slot)
    {
        var root = slot.root != null ? slot.root : transform;
        return root.GetComponentsInChildren<FieldCell>(true).ToList();
    }

    private Dictionary<string, FieldCell> BuildCellMap(RemoteBaseSlot slot)
    {
        var map = new Dictionary<string, FieldCell>();
        foreach (var cell in GetCells(slot))
        {
            if (!string.IsNullOrEmpty(cell.Id) && !map.ContainsKey(cell.Id))
                map.Add(cell.Id, cell);
        }
        return map;
    }


    private void EnsureLocalSlot()
    {
        if (slots == null || slots.Count == 0) return;
        var localId = GetLocalPlayerId();
        if (string.IsNullOrEmpty(localId) && _lobbyModeActive)
            localId = OfflineLocalPlayerId;
        if (string.IsNullOrEmpty(localId)) return;
        var idx = GetLocalSlotIndex();
        if (idx < 0 || idx >= slots.Count) return;
        _slotPlayerIds[idx] = localId;
        _playerToSlot[localId] = idx;
    }


    private int GetLocalSlotIndex()
    {
        if (slots == null || slots.Count == 0) return -1;
        if (_serverLocalSlotIndex >= 0 && _serverLocalSlotIndex < slots.Count)
            return _serverLocalSlotIndex;
        if (_lastPreparedLocalSlotIndex >= 0 && _lastPreparedLocalSlotIndex < slots.Count)
            return _lastPreparedLocalSlotIndex;
        if (_lastTeleportedSlotIndex >= 0 && _lastTeleportedSlotIndex < slots.Count)
            return _lastTeleportedSlotIndex;
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] != null && slots[i].isLocalSlot) return i;
        }
        var localId = GetLocalPlayerId();
        if (!string.IsNullOrEmpty(localId) &&
            !string.Equals(localId, OfflineLocalPlayerId, StringComparison.Ordinal))
        {
            var preferred = PreferredSlotIndex(localId);
            if (preferred >= 0 && preferred < slots.Count)
                return preferred;
        }

        return GetFallbackLocalSlotIndex();
    }

    private int GetFallbackLocalSlotIndex()
    {
        if (slots == null || slots.Count == 0)
            return -1;

        for (var i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            if (slot != null && slot.root != null)
                return i;
        }

        return 0;
    }

    private bool IsLocalSlotIndex(int index)
    {
        var localIdx = GetLocalSlotIndex();
        return index >= 0 && index == localIdx;
    }

    private bool IsLocalSlot(RemoteBaseSlot slot)
    {
        if (slots == null || slot == null) return false;
        var idx = slots.IndexOf(slot);
        return IsLocalSlotIndex(idx);
    }

    public Transform GetLocalSlotRoot()
    {
        if (slots == null || slots.Count == 0)
            return null;

        var idx = GetLocalSlotIndex();
        if (idx >= 0 && idx < slots.Count && slots[idx] != null && slots[idx].root != null)
            return slots[idx].root;

        if (G.Player != null)
        {
            var playerPos = G.Player.transform.position;
            var bestDist = float.MaxValue;
            Transform bestRoot = null;
            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot == null || slot.root == null) continue;
                var anchor = slot.teleportTarget != null ? slot.teleportTarget.position : slot.root.position;
                var dist = (anchor - playerPos).sqrMagnitude;
                if (dist >= bestDist) continue;
                bestDist = dist;
                bestRoot = slot.root;
            }

            if (bestRoot != null)
                return bestRoot;
        }

        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] != null && slots[i].root != null)
                return slots[i].root;
        }

        return null;
    }

    public bool TryResolveSlotIndex(Transform context, out int slotIndex)
    {
        slotIndex = -1;
        if (context == null || slots == null || slots.Count == 0)
            return false;

        for (var i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            if (slot == null || slot.root == null)
                continue;

            if (context == slot.root || context.IsChildOf(slot.root) || slot.root.IsChildOf(context))
            {
                slotIndex = i;
                return true;
            }
        }

        return false;
    }

    public bool IsLocalSlotForClient(int slotIndex)
    {
        return IsLocalSlotIndex(slotIndex);
    }

    public bool TryGetProfileTargetForSlot(
        int slotIndex,
        out string playerId,
        out string friendCode,
        out string displayName,
        out PlayerPublicStatsDto stats,
        out bool isOnline)
    {
        playerId = null;
        friendCode = null;
        displayName = null;
        stats = null;
        isOnline = false;

        if (slots == null || slotIndex < 0 || slotIndex >= slots.Count)
            return false;

        var slot = slots[slotIndex];
        if (slot == null)
            return false;

        var board = GetBoardForSlot(slot);
        if (board == null || !board.HasRemoteData)
            return false;

        playerId = board.RemotePlayerId;
        friendCode = board.RemoteFriendCode;
        displayName = board.RemoteDisplayName;
        stats = board.RemoteStats;
        isOnline = board.IsOnline;
        return true;
    }

    public bool TryGetResolvedLocalSlotRoot(out Transform root)
    {
        root = null;
        if (slots == null || slots.Count == 0)
            return false;

        var idx = _hasServerResolvedLocalSlot ? _serverLocalSlotIndex : _lastPreparedLocalSlotIndex;
        if (idx < 0 || idx >= slots.Count)
            return false;
        if (_lastPreparedLocalSlotIndex != idx)
            return false;

        var slot = slots[idx];
        if (slot == null || slot.root == null)
            return false;

        root = slot.root;
        return true;
    }

    public Transform GetLocalSlotEntryPoint()
    {
        if (slots == null || slots.Count == 0)
            return null;

        var idx = GetLocalSlotIndex();
        if (idx < 0 || idx >= slots.Count)
            return null;

        return GetSlotEntryPoint(idx);
    }

    private void EnsureLocalHomeMarker()
    {
        if (!showLocalHomeMarker)
            return;

        if (_localHomeMarker == null)
        {
            _localHomeMarker = GetComponentInChildren<LocalHomeWorldMarker>(true);
            if (_localHomeMarker == null && localHomeMarkerPrefab != null)
                _localHomeMarker = Instantiate(localHomeMarkerPrefab, transform, false);
        }

        if (_localHomeMarker == null)
        {
            Debug.LogWarning("[Lobby] LocalHomeWorldMarker prefab is not assigned.", this);
            return;
        }

        _localHomeMarker.Initialize(this);
    }

    public void ApplyFriendBase(BaseSnapshotDto snapshot, int slotIndex = 0)
    {
        if (slots == null || slots.Count == 0) return;
        slotIndex = Mathf.Clamp(slotIndex, 0, slots.Count - 1);
        ApplySnapshotToSlot(slots[slotIndex], snapshot);
    }

    public Transform GetSlotEntryPoint(int slotIndex = 0)
    {
        if (slots == null || slots.Count == 0) return null;
        slotIndex = Mathf.Clamp(slotIndex, 0, slots.Count - 1);

        var slot = slots[slotIndex];
        if (slot == null || slot.root == null) return null;

        if (slot.teleportTarget != null)
            return slot.teleportTarget;

        var playerBase = slot.root.GetComponentInChildren<PlayerBase>(true);
        if (playerBase != null)
            return playerBase.EntryPoint;

        return slot.root;
    }

    public void TeleportPlayerToSlot(int slotIndex = 0)
    {
        var target = GetSlotEntryPoint(slotIndex);
        if (target == null || G.Player == null) return;
        if (debugLogs) Debug.Log($"[Lobby] Teleport target pos={target.position}");

        var cc = G.Player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        var rb = G.Player.GetComponent<Rigidbody>();
        if (rb != null) rb.MovePosition(target.position); else G.Player.transform.position = target.position;
        G.Player.transform.rotation = target.rotation;
        if (cc != null) cc.enabled = true;
        if (debugLogs) Debug.Log($"[Lobby] Player pos after tp={G.Player.transform.position}");
    }

    private void TryTeleportLocalPlayer()
    {
        var pid = GetLocalPlayerId();
        if (string.IsNullOrEmpty(pid)) return;

        var idx = GetLocalSlotIndex();
        if (idx < 0) idx = FindSlotForPlayer(pid);
        if (idx < 0) return;

        var samePlayer = _lastTeleportedPlayerId == pid;
        var sameSlot = _lastTeleportedSlotIndex == idx;
        if (samePlayer && sameSlot) return;

        if (G.Player == null)
        {
            if (_teleportRoutine == null)
                _teleportRoutine = StartCoroutine(TeleportWhenReady(idx, pid));
            return;
        }

        if (_suppressNextAutoTeleport)
        {
            if (debugLogs) Debug.Log($"[Lobby] Auto-teleport suppressed for player {pid} at slot {idx}");
            _lastTeleportedSlotIndex = idx;
            _lastTeleportedPlayerId = pid;
            _suppressNextAutoTeleport = false;
            return;
        }

        if (debugLogs) Debug.Log($"[Lobby] Teleport to slot {idx}");
        TeleportPlayerToSlot(idx);
        _lastTeleportedSlotIndex = idx;
        _lastTeleportedPlayerId = pid;
    }


    private IEnumerator TeleportWhenReady(int slotIndex, string playerId)
    {
        while (G.Player == null)
            yield return null;
        TeleportPlayerToSlot(slotIndex);
        _lastTeleportedSlotIndex = slotIndex;
        _lastTeleportedPlayerId = playerId;
        _teleportRoutine = null;
    }

    private RemoteFriendBoard GetBoardForSlot(RemoteBaseSlot slot)
    {
        if (!moveFriendBoardToRemotePlayer || slot == null)
            return slot != null ? slot.friendBoard : null;

        var index = slots.IndexOf(slot);
        if (index < 0) return slot.friendBoard;

        if (_slotRemoteBoards != null && _slotRemoteBoards.Length > index && _slotRemoteBoards[index] != null)
            return _slotRemoteBoards[index];

        return slot.friendBoard;
    }

    private void EnsureRemotePlayer(int slotIndex, string ownerPlayerId = null)
    {
        if (!spawnRemotePlayers) return;
        if (remotePlayerPrefab == null) return;
        if (slots == null || slotIndex < 0 || slotIndex >= slots.Count) return;

        var slot = slots[slotIndex];
        var spawn = slot != null
            ? (slot.teleportTarget != null ? slot.teleportTarget : slot.root)
            : null;

        if (_slotRemotePlayers[slotIndex] == null)
        {
            if (spawn == null) return;

            var go = Instantiate(remotePlayerPrefab, spawn.position, spawn.rotation);
            go.name = $"RemotePlayer_{slotIndex}";
            _slotRemotePlayers[slotIndex] = go;

            var mover = go.GetComponent<RemotePlayerMover>();
            if (mover == null) mover = go.AddComponent<RemotePlayerMover>();
            mover.SetDelay(Mathf.Max(remotePlayerDelaySec, 1f));

            var cc = go.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            foreach (var col in go.GetComponentsInChildren<Collider>(true))
            {
                if (col is CharacterController) continue;
                col.isTrigger = true;
            }
            var rb = go.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;
        }

        var remotePlayer = _slotRemotePlayers[slotIndex];
        if (remotePlayer == null)
            return;

        var ownerChanged = !string.IsNullOrEmpty(ownerPlayerId) &&
                           _slotRemotePlayerOwnerIds != null &&
                           slotIndex >= 0 &&
                           slotIndex < _slotRemotePlayerOwnerIds.Length &&
                           !string.Equals(_slotRemotePlayerOwnerIds[slotIndex], ownerPlayerId, StringComparison.Ordinal);

        if (ownerChanged)
        {
            var mover = remotePlayer.GetComponent<RemotePlayerMover>();
            if (mover != null)
                mover.ResetTransientState();

            if (spawn != null)
            {
                remotePlayer.transform.position = spawn.position;
                remotePlayer.transform.rotation = spawn.rotation;
            }

            if (_slotRemoteBoards != null &&
                slotIndex >= 0 &&
                slotIndex < _slotRemoteBoards.Length &&
                _slotRemoteBoards[slotIndex] != null)
            {
                _slotRemoteBoards[slotIndex].SetRemote(null, null, false, false);
            }
        }

        if (_slotRemotePlayerOwnerIds != null &&
            slotIndex >= 0 &&
            slotIndex < _slotRemotePlayerOwnerIds.Length)
        {
            _slotRemotePlayerOwnerIds[slotIndex] = ownerPlayerId;
        }

        _slotRemoteBoards[slotIndex] = EnsureRemoteBoard(slotIndex, remotePlayer);
        remotePlayer.SetActive(true);
    }

    private RemoteFriendBoard EnsureRemoteBoard(int slotIndex, GameObject remotePlayer)
    {
        if (remotePlayer == null || slots == null || slotIndex < 0 || slotIndex >= slots.Count)
            return null;

        var slot = slots[slotIndex];
        var board = remotePlayer.GetComponentInChildren<RemoteFriendBoard>(true);
        if (board == null && moveFriendBoardToRemotePlayer && slot != null && slot.friendBoard != null)
        {
            slot.friendBoard.transform.SetParent(remotePlayer.transform, worldPositionStays: false);
            board = slot.friendBoard;
        }
        if (board == null)
            board = remotePlayer.AddComponent<RemoteFriendBoard>();

        var interactionPanel = board.InteractionPanel;
        if (interactionPanel == null)
        {
            interactionPanel = remotePlayer.GetComponentInChildren<InteractionPanel>(true);
            if (interactionPanel == null && remoteInteractionCanvasPrefab != null)
            {
                var panelInstance = Instantiate(remoteInteractionCanvasPrefab, remotePlayer.transform, false);
                panelInstance.name = $"RemoteInteractionCanvas_{slotIndex}";
                var panelTransform = panelInstance.transform;
                panelTransform.localPosition = new Vector3(0f, 2f, 0f);
                panelTransform.localRotation = Quaternion.identity;
                interactionPanel = panelInstance.GetComponentInChildren<InteractionPanel>(true);
            }
        }

        if (interactionPanel != null)
        {
            board.SetInteractionPanel(interactionPanel);
            interactionPanel.gameObject.SetActive(false);
        }

        var listener = EnsureRemoteInteractionListener(remotePlayer);
        board.BindRaycastListener(listener, Mathf.Max(0.1f, remoteInteractionDistance));
        return board;
    }

    private InteractionRaycastListener EnsureRemoteInteractionListener(GameObject remotePlayer)
    {
        if (remotePlayer == null)
            return null;

        var listener = remotePlayer.GetComponent<InteractionRaycastListener>();
        if (listener == null)
            listener = remotePlayer.AddComponent<InteractionRaycastListener>();

        var interactableLayer = LayerMask.NameToLayer("Interactable");
        if (interactableLayer >= 0)
            SetLayerRecursively(remotePlayer.transform, interactableLayer);

        listener.MaxDistance = Mathf.Max(0.1f, remoteInteractionDistance);
        return listener;
    }

    private static void SetLayerRecursively(Transform root, int layer)
    {
        if (root == null || layer < 0)
            return;

        root.gameObject.layer = layer;
        for (int i = 0; i < root.childCount; i++)
            SetLayerRecursively(root.GetChild(i), layer);
    }

    private void DisableRemotePlayer(int slotIndex)
    {
        if (_slotRemotePlayers == null || slotIndex < 0 || slotIndex >= _slotRemotePlayers.Length) return;
        if (_slotRemotePlayers[slotIndex] != null)
        {
            var mover = _slotRemotePlayers[slotIndex].GetComponent<RemotePlayerMover>();
            if (mover != null)
                mover.ResetTransientState();
            _slotRemotePlayers[slotIndex].SetActive(false);
        }

        if (_slotRemotePlayerOwnerIds != null && slotIndex >= 0 && slotIndex < _slotRemotePlayerOwnerIds.Length)
            _slotRemotePlayerOwnerIds[slotIndex] = null;

        if (_slotRemoteBoards != null && slotIndex >= 0 && slotIndex < _slotRemoteBoards.Length)
        {
            if (_slotRemoteBoards[slotIndex] != null)
                _slotRemoteBoards[slotIndex].SetRemote(null, null, false, false);
            _slotRemoteBoards[slotIndex] = null;
        }
    }

    private void ApplyRemotePositions(int slotIndex, List<LobbyPosSampleDto> positions)
    {
        if (_slotRemotePlayers == null || slotIndex < 0 || slotIndex >= _slotRemotePlayers.Length) return;
        var go = _slotRemotePlayers[slotIndex];
        if (go == null) return;
        var mover = go.GetComponent<RemotePlayerMover>();
        if (mover == null) return;
        mover.PushSamples(positions);
    }

    private void ApplyRemoteHolding(int slotIndex, LobbyHandItemDto hand)
    {
        if (_slotRemotePlayers == null || slotIndex < 0 || slotIndex >= _slotRemotePlayers.Length) return;
        var go = _slotRemotePlayers[slotIndex];
        if (go == null) return;
        var mover = go.GetComponent<RemotePlayerMover>();
        if (mover == null) return;
        mover.SetHand(hand);
    }

    private string GetLocalPlayerId()
    {
        try
        {
            if (G.Save == null)
                return _lastLocalPlayerId;

            if (!G.Save.IsReady)
                return _lastLocalPlayerId;

            var id = G.Save.LoadBackendProfile().playerId;
            if (!string.IsNullOrEmpty(id))
                _lastLocalPlayerId = id;
        }
        catch
        {
            // Ignore profile-read race during bootstrap and keep last known id.
        }

        return _lastLocalPlayerId;
    }

    public void SuppressNextAutoTeleport()
    {
        _suppressNextAutoTeleport = true;
    }

    public void HandleLocalSaveReset()
    {
        if (_waitForSaveReadyRoutine != null)
        {
            StopCoroutine(_waitForSaveReadyRoutine);
            _waitForSaveReadyRoutine = null;
        }

        _pendingLocalRestoreSlotIndex = -1;
        _lastLocalPlayerId = null;
        _serverLocalSlotIndex = -1;
        _lastPreparedLocalSlotIndex = -1;
        _lastTeleportedSlotIndex = -2;
        _lastTeleportedPlayerId = null;
        _hasServerResolvedLocalSlot = false;
        _forceLocalRestoreOnNextResolve = true;
        _suppressNextAutoTeleport = false;

        _playerToSlot.Clear();

        EnsureSlotState();
        if (_slotPlayerIds != null)
            Array.Clear(_slotPlayerIds, 0, _slotPlayerIds.Length);
        if (_slotUpdatedAt != null)
        {
            for (var i = 0; i < _slotUpdatedAt.Length; i++)
                _slotUpdatedAt[i] = EmptySlotMarker;
        }

        ApplyOfflineLocalOnly();
    }
}
