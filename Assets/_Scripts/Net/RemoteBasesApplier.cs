using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[DefaultExecutionOrder(-1000)]
public class RemoteBasesApplier : MonoBehaviour
{
    private const string EmptySlotMarker = "__empty__";

    private class SlotSnapshotCache
    {
        public int conveyorLevel = int.MinValue;
        public int bigPetId = int.MinValue;
        public int bigPetLvl = int.MinValue;
        public int bigPetXp = int.MinValue;
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
    [Header("Proximity Sync")]
    [SerializeField] private bool syncOnlyNearSlots = true;
    [SerializeField] private float syncDistanceMeters = 10f;
    [SerializeField] private float syncDistanceHysteresisMeters = 2f;
    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;
    [SerializeField] private bool testCloneLocalToRandomSlot = false;
    [SerializeField] private int testCloneSlotIndex = -1;

    private ZooBackendClient backend;
    private readonly Dictionary<string, int> _playerToSlot = new();
    private string[] _slotPlayerIds;
    private string[] _slotUpdatedAt;
    private GameObject[] _slotRemotePlayers;
    private RemoteFriendBoard[] _slotRemoteBoards;
    private Coroutine[] _slotSnapshotApplyRoutines;
    private BaseSnapshotDto[] _slotSnapshotApplyPending;
    private bool[] _slotHadSnapshot;
    private SlotSnapshotCache[] _slotSnapshotCaches;
    private string _lastLocalPlayerId;
    private int _serverLocalSlotIndex = -1;
    private int _lastTeleportedSlotIndex = -2;
    private string _lastTeleportedPlayerId;
    private Coroutine _teleportRoutine;
    private bool _lobbyModeActive;
    private int _lastPreparedLocalSlotIndex = -1;
    private int[] _slotModeState;
    private bool[] _slotWithinSyncRange;
    private bool _didInitialFullLobbySync;

    private void Awake()
    {
        if (backend == null) backend = G.Backend;
        EnsureSlotState();
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
    }

    private void Start()
    {
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
        EnsureLocalSlot();
        _lobbyModeActive = true;
        _didInitialFullLobbySync = false;

        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            if (slot == null || slot.root == null)
                continue;

            if (IsLocalSlotIndex(i))
            {
                slot.root.gameObject.SetActive(true);
                ApplySlotMode(i, false);
                if (_slotWithinSyncRange != null && i < _slotWithinSyncRange.Length)
                    _slotWithinSyncRange[i] = true;
                continue;
            }

            if (!string.IsNullOrEmpty(_slotPlayerIds[i]))
            {
                _playerToSlot.Remove(_slotPlayerIds[i]);
                _slotPlayerIds[i] = null;
            }

            _slotUpdatedAt[i] = EmptySlotMarker;
            if (_slotWithinSyncRange != null && i < _slotWithinSyncRange.Length)
                _slotWithinSyncRange[i] = false;
            UpdateChestLobby(slot, null);
            UpdateFriendBoardLobby(slot, null);
            ClearSlot(slot);
            DisableRemotePlayer(i);
        }
    }

    private void MarkRemoteComponents()
    {
        if (slots == null) return;
        var hasConfiguredLocalSlot = _serverLocalSlotIndex >= 0 || slots.Any(s => s != null && s.isLocalSlot);
        if (!hasConfiguredLocalSlot)
            return;

        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            if (slot == null || slot.root == null) continue;
            ApplySlotMode(i, !IsLocalSlotIndex(i));
        }
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
                    EnsureRemotePlayer(i);
                    UpdateChestLocation(slots[i], loc);
                    UpdateFriendBoardLocation(slots[i], loc);
                    ApplyIfChanged(i, slots[i], loc);
                }
                else
                {
                    if (disableEmptySlots && slots[i].root != null)
                        slots[i].root.gameObject.SetActive(false);
                    DisableRemotePlayer(i);
                }
            }
            else if (!string.IsNullOrEmpty(pid))
            {
                _playerToSlot.Remove(pid);
                _slotPlayerIds[i] = null;
                _slotUpdatedAt[i] = null;
                UpdateChestLocation(slots[i], null);
                UpdateFriendBoardLocation(slots[i], null);
                if (clearEmptySlots)
                    ClearSlot(slots[i]);
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
                EnsureRemotePlayer(slotIndex);
                UpdateChestLocation(slots[slotIndex], pick);
                UpdateFriendBoardLocation(slots[slotIndex], pick);
                ApplyIfChanged(slotIndex, slots[slotIndex], pick, force: true);
            }
            else
            {
                if (disableEmptySlots && slots[slotIndex].root != null)
                    slots[slotIndex].root.gameObject.SetActive(false);
                DisableRemotePlayer(slotIndex);
            }
        }

        if (clearEmptySlots)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                if (IsLocalSlotIndex(i)) continue;
                if (string.IsNullOrEmpty(_slotPlayerIds[i]))
                {
                    ClearSlot(slots[i]);
                    DisableRemotePlayer(i);
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

        var localId = GetLocalPlayerId();
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
        else if (previousServerLocalSlotIndex >= 0 && previousServerLocalSlotIndex < slots.Count)
            _serverLocalSlotIndex = previousServerLocalSlotIndex;
        else
            _serverLocalSlotIndex = -1;

        if (!string.IsNullOrEmpty(localId))
            _lastLocalPlayerId = localId;

        var hasResolvedLocalSlot = _serverLocalSlotIndex >= 0 && _serverLocalSlotIndex < slots.Count;
        if (!hasResolvedLocalSlot)
        {
            // Avoid switching every slot into remote mode when local player id is still bootstrapping.
            if (debugLogs)
                Debug.LogWarning("[Lobby] local slot unresolved, skip apply.");
            return;
        }

        if (debugLogs && localMember != null)
            Debug.Log($"[Lobby] local slotIndex={_serverLocalSlotIndex}");

        var remoteMembersCount = 0;
        if (members != null)
        {
            foreach (var m in members)
            {
                if (m == null || string.IsNullOrEmpty(m.playerId)) continue;
                if (localMember != null && m.playerId == localMember.playerId) continue;
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

                ApplySlotMode(i, false);
                if (_lastPreparedLocalSlotIndex != i)
                {
                    UnlockCellsForSlot(slots[i]);
                    _lastPreparedLocalSlotIndex = i;
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
                _slotPlayerIds[i] = member.playerId;
                _playerToSlot[member.playerId] = i;
                EnsureRemotePlayer(i);
                ApplyRemotePositions(i, member.positions);
                ApplyRemoteHolding(i, member.hand);
                UpdateFriendBoardLobby(slots[i], member);

                var inSyncRange = !applyDistanceCulling || IsSlotInSyncRange(i);
                if (!inSyncRange)
                {
                    if (disableEmptySlots && slots[i].root != null)
                        slots[i].root.gameObject.SetActive(false);
                    UpdateChestLobby(slots[i], null);
                    continue;
                }

                if (slots[i].root != null)
                    slots[i].root.gameObject.SetActive(true);

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
                {
                    ClearSlot(slots[i]);
                    _slotUpdatedAt[i] = EmptySlotMarker;
                }
                DisableRemotePlayer(i);
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


    private void ApplyTestClone(LobbyMemberStateDto localMember)
    {
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

        board.SetRemoteWithId(member.playerId, member.friendCode, member.displayName, member.isFriend, member.isOnline, ExtractStats(member.baseData));
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

        if (disableEmptySlots)
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

        if (disableEmptySlots)
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
            cache.bigPetXp == snapshot.bigPet.xp)
            return;

        ApplyBigPet(slot, snapshot);
        cache.bigPetId = snapshot.bigPet.id;
        cache.bigPetLvl = snapshot.bigPet.lvl;
        cache.bigPetXp = snapshot.bigPet.xp;
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
            SpawnEgg(cell, item.id, item.dinamic);
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
        bigPet.ApplyRemoteState(snapshot.bigPet.id, snapshot.bigPet.lvl, snapshot.bigPet.xp);
    }

    private void SpawnEgg(FieldCell cell, string id, BrainrotDinamicData dinamic)
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

        var egg = Instantiate(prefab, cell.transform);
        egg.SetData(dinamic);
        egg.OnInventoryAdd();
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

        var pet = Instantiate(prefab, cell.transform);
        pet.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        pet.Init(dinamic, null, incomeLastTime);
        var info = pet.GetComponentInChildren<BrainrotInfoUI>(true);
        if (info != null)
            info.SetRemoteView(true);
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
            if (slots[i].isLocalSlot) return i;
        }
        var localId = GetLocalPlayerId();
        if (string.IsNullOrEmpty(localId)) return -1;
        return PreferredSlotIndex(localId);
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

    public Transform GetLocalSlotEntryPoint()
    {
        if (slots == null || slots.Count == 0)
            return null;

        var idx = GetLocalSlotIndex();
        if (idx < 0 || idx >= slots.Count)
            return null;

        return GetSlotEntryPoint(idx);
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
        StartCoroutine(TeleportVerify(target.position, target.rotation));
    }

    private void TryTeleportLocalPlayer()
    {
        if (G.Save == null) return;
        var pid = G.Save.LoadBackendProfile().playerId;
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


    private IEnumerator TeleportVerify(Vector3 targetPos, Quaternion targetRot)
    {
        yield return new WaitForSeconds(0.2f);
        if (G.Player == null) yield break;
        if ((G.Player.transform.position - targetPos).sqrMagnitude > 0.01f)
        {
            if (debugLogs) Debug.Log("[Lobby] Teleport re-apply");
            var cc = G.Player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            var rb = G.Player.GetComponent<Rigidbody>();
            if (rb != null)
                rb.MovePosition(targetPos);
            else
                G.Player.transform.position = targetPos;
            G.Player.transform.rotation = targetRot;
            if (cc != null) cc.enabled = true;
        }
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

    private void EnsureRemotePlayer(int slotIndex)
    {
        if (!spawnRemotePlayers) return;
        if (remotePlayerPrefab == null) return;
        if (slots == null || slotIndex < 0 || slotIndex >= slots.Count) return;

        if (_slotRemotePlayers[slotIndex] == null)
        {
            var slot = slots[slotIndex];
            var spawn = slot.teleportTarget != null ? slot.teleportTarget : slot.root;
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
            remotePlayer.layer = interactableLayer;

        listener.MaxDistance = Mathf.Max(0.1f, remoteInteractionDistance);
        return listener;
    }

    private void DisableRemotePlayer(int slotIndex)
    {
        if (_slotRemotePlayers == null || slotIndex < 0 || slotIndex >= _slotRemotePlayers.Length) return;
        if (_slotRemotePlayers[slotIndex] != null)
        {
            var mover = _slotRemotePlayers[slotIndex].GetComponent<RemotePlayerMover>();
            if (mover != null)
                mover.SetHand(null);
            _slotRemotePlayers[slotIndex].SetActive(false);
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
}
