using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[DefaultExecutionOrder(-1000)]
public class RemoteBasesApplier : MonoBehaviour
{
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
    [SerializeField] private float remotePlayerDelaySec = 0.1f;
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
    private string _lastLocalPlayerId;
    private int _serverLocalSlotIndex = -1;
    private int _lastTeleportedSlotIndex = -2;
    private string _lastTeleportedPlayerId;
    private Coroutine _teleportRoutine;

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

    private void MarkRemoteComponents()
    {
        if (slots == null) return;
        foreach (var slot in slots)
        {
            if (slot == null || slot.root == null) continue;
            if (IsLocalSlot(slot)) continue;
            foreach (var conveyor in slot.root.GetComponentsInChildren<Conveyor>(true))
                conveyor.SetRemoteMode(true);
            foreach (var bigPet in slot.root.GetComponentsInChildren<BigPetPoint>(true))
                bigPet.SetRemoteMode(true);
            SetFieldManagersForSlot(slot, false);
        }
    }

    private void ApplyLocations(List<ZooLocationItem> locations)
    {
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
                EnsureRemotePlayer(i);
                UpdateChestLocation(slots[i], loc);
                UpdateFriendBoardLocation(slots[i], loc);
                ApplyIfChanged(i, slots[i], loc);
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
            EnsureRemotePlayer(slotIndex);
            UpdateChestLocation(slots[slotIndex], pick);
            UpdateFriendBoardLocation(slots[slotIndex], pick);
            ApplyIfChanged(slotIndex, slots[slotIndex], pick, force: true);
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

        var localId = GetLocalPlayerId();
        _serverLocalSlotIndex = -1;
        LobbyMemberStateDto localMember = null;

        if (members != null && !string.IsNullOrEmpty(localId))
        {
            foreach (var m in members)
            {
                if (m == null || string.IsNullOrEmpty(m.playerId)) continue;
                if (m.playerId != localId) continue;
                localMember = m;
                if (m.slotIndex >= 0 && m.slotIndex < slots.Count)
                    _serverLocalSlotIndex = m.slotIndex;
                if (debugLogs) Debug.Log($"[Lobby] local slotIndex={_serverLocalSlotIndex}");
            }
        }

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

                foreach (var conveyor in slots[i].root.GetComponentsInChildren<Conveyor>(true))
                    conveyor.SetRemoteMode(false);
                foreach (var bigPet in slots[i].root.GetComponentsInChildren<BigPetPoint>(true))
                    bigPet.SetRemoteMode(false);
                foreach (var fm in slots[i].root.GetComponentsInChildren<FieldManager>(true))
                    fm.EnsureInitialized();
                SetFieldManagersForSlot(slots[i], true);

                if (!string.IsNullOrEmpty(localId))
                {
                    _slotPlayerIds[i] = localId;
                    _playerToSlot[localId] = i;
                }
                continue;
            }
            else
            {
                if (slots[i].root != null)
                {
                    foreach (var conveyor in slots[i].root.GetComponentsInChildren<Conveyor>(true))
                        conveyor.SetRemoteMode(true);
                    foreach (var bigPet in slots[i].root.GetComponentsInChildren<BigPetPoint>(true))
                        bigPet.SetRemoteMode(true);
                    SetFieldManagersForSlot(slots[i], false);
                }
            }

            if (desiredBySlot.TryGetValue(i, out var member))
            {
                _slotPlayerIds[i] = member.playerId;
                _playerToSlot[member.playerId] = i;
                EnsureRemotePlayer(i);
                UpdateChestLobby(slots[i], member);
                UpdateFriendBoardLobby(slots[i], member);
                ApplyRemotePositions(i, member.positions);
                ApplyRemoteHolding(i, member.hand);
                ApplyIfChangedLobby(i, slots[i], member);
            }
            else
            {
                if (!string.IsNullOrEmpty(_slotPlayerIds[i]))
                {
                    _playerToSlot.Remove(_slotPlayerIds[i]);
                    _slotPlayerIds[i] = null;
                    _slotUpdatedAt[i] = null;
                }
                UpdateChestLobby(slots[i], null);
                UpdateFriendBoardLobby(slots[i], null);
                if (clearEmptySlots)
                    ClearSlot(slots[i]);
                DisableRemotePlayer(i);
            }
        }

        ApplyTestClone(localMember);
        TryTeleportLocalPlayer();
    }


    private void SetFieldManagersForSlot(RemoteBaseSlot slot, bool enabled)
    {
        if (slot == null || slot.root == null) return;
        foreach (var fm in slot.root.GetComponentsInChildren<FieldManager>(true))
            fm.enabled = enabled;
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

        board.SetRemoteWithId(loc.playerId, loc.friendCode, loc.displayName, loc.isFriend, loc.isOnline);
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

        board.SetRemoteWithId(member.playerId, member.friendCode, member.displayName, member.isFriend, member.isOnline);
    }

    private void ApplySnapshotToSlot(RemoteBaseSlot slot, BaseSnapshotDto snapshot)
    {
        if (slot == null || slot.root == null || IsLocalSlot(slot))
            return;

        if (disableEmptySlots)
            slot.root.gameObject.SetActive(true);

        EnsureFieldIds(slot);
        ClearSlot(slot, disableRoot: false);

        if (snapshot == null)
            return;

        ApplyConveyor(slot, snapshot);
        ApplyBigPet(slot, snapshot);

        if (slot.applyLand && snapshot.land != null && snapshot.land.boughtCells != null)
        {
            var land = new HashSet<int>(snapshot.land.boughtCells ?? new List<int>());
            foreach (var field in GetFields(slot))
            {
                field.SetUnblockedVisual(land.Contains(field.ID));
            }
        }

        var cellMap = BuildCellMap(slot);
        var cells = snapshot.cells;
        if (cells == null || cells.Count == 0)
        {
            if (snapshot.animalsOnCells != null && snapshot.animalsOnCells.Count > 0)
            {
                foreach (var old in snapshot.animalsOnCells)
                {
                    if (old == null || string.IsNullOrEmpty(old.cell))
                        continue;

                    if (cellMap.TryGetValue(old.cell, out var cell))
                    {
                        SpawnBrainrot(cell, old.animalId, default, 0);
                    }
                }
            }
            return;
        }

        foreach (var item in cells)
        {
            if (item == null || string.IsNullOrEmpty(item.cell))
                continue;

            if (!cellMap.TryGetValue(item.cell, out var cell))
                continue;

            if (slot.lockCells)
                cell.LockCell(true);

            if (item.kind == "egg")
            {
                SpawnEgg(cell, item.id, item.dinamic);
            }
            else if (item.kind == "brainrot")
            {
                SpawnBrainrot(cell, item.id, item.dinamic, item.incomeLastTime);
            }
        }
    }

    private void ApplyIfChanged(int slotIndex, RemoteBaseSlot slot, ZooLocationItem loc, bool force = false)
    {
        if (loc == null)
            return;

        var updatedAt = loc.baseData?.updatedAt ?? "";
        if (!force && _slotUpdatedAt[slotIndex] == updatedAt)
            return;

        _slotUpdatedAt[slotIndex] = updatedAt;
        ApplySnapshotToSlot(slot, loc.baseData);
    }

    private void ApplyIfChangedLobby(int slotIndex, RemoteBaseSlot slot, LobbyMemberStateDto member, bool force = false)
    {
        if (member == null)
            return;

        // Для лобби обновляем базу только когда реально изменилась база
        var updatedAt = member.baseData?.updatedAt ?? "";

        if (!force && _slotUpdatedAt[slotIndex] == updatedAt)
            return;

        _slotUpdatedAt[slotIndex] = updatedAt;
        ApplySnapshotToSlot(slot, member.baseData);
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
    }

    private int FindSlotForPlayer(string playerId)
    {
        if (slots == null || slots.Count == 0) return -1;

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

        if (disableRoot)
            slot.root.gameObject.SetActive(false);

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
            _slotRemoteBoards[slotIndex] = go.GetComponentInChildren<RemoteFriendBoard>(true);
            var mover = go.GetComponent<RemotePlayerMover>();
            if (mover == null) mover = go.AddComponent<RemotePlayerMover>();
            mover.SetDelay(remotePlayerDelaySec);

            var cc = go.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            foreach (var col in go.GetComponentsInChildren<Collider>(true))
            {
                if (col is CharacterController) continue;
                col.isTrigger = true;
            }
            var rb = go.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;

            if (moveFriendBoardToRemotePlayer && _slotRemoteBoards[slotIndex] == null && slot.friendBoard != null)
            {
                slot.friendBoard.transform.SetParent(go.transform, worldPositionStays: false);
                _slotRemoteBoards[slotIndex] = slot.friendBoard;
            }
        }

        _slotRemotePlayers[slotIndex].SetActive(true);
    }

    private void DisableRemotePlayer(int slotIndex)
    {
        if (_slotRemotePlayers == null || slotIndex < 0 || slotIndex >= _slotRemotePlayers.Length) return;
        if (_slotRemotePlayers[slotIndex] != null)
            _slotRemotePlayers[slotIndex].SetActive(false);
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
        var holding = hand != null && !string.IsNullOrEmpty(hand.type) && hand.type != "hammer";
        mover.SetHolding(holding);
    }

    private string GetLocalPlayerId()
    {
        if (G.Save == null) return null;
        return G.Save.LoadBackendProfile().playerId;
    }
}
