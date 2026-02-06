using System;
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

    private ZooBackendClient backend;
    private readonly Dictionary<string, int> _playerToSlot = new();
    private string[] _slotPlayerIds;
    private string[] _slotUpdatedAt;
    private GameObject[] _slotRemotePlayers;
    private RemoteFriendBoard[] _slotRemoteBoards;

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
            foreach (var conveyor in slot.root.GetComponentsInChildren<Conveyor>(true))
                conveyor.SetRemoteMode(true);
            foreach (var bigPet in slot.root.GetComponentsInChildren<BigPetPoint>(true))
                bigPet.SetRemoteMode(true);
        }
    }

    private void ApplyLocations(List<ZooLocationItem> locations)
    {
        if (slots == null || slots.Count == 0)
            return;

        EnsureSlotState();

        var available = new Dictionary<string, ZooLocationItem>();
        if (locations != null)
        {
            foreach (var loc in locations)
            {
                if (loc == null || string.IsNullOrEmpty(loc.playerId)) continue;
                if (!available.ContainsKey(loc.playerId))
                    available.Add(loc.playerId, loc);
            }
        }

        var usedPlayers = new HashSet<string>();

        // keep existing assignments if player still in list
        for (int i = 0; i < slots.Count; i++)
        {
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

        var available = new Dictionary<string, LobbyMemberStateDto>();
        if (members != null)
        {
            foreach (var m in members)
            {
                if (m == null || string.IsNullOrEmpty(m.playerId)) continue;
                if (!available.ContainsKey(m.playerId))
                    available.Add(m.playerId, m);
            }
        }

        var usedPlayers = new HashSet<string>();

        // keep existing assignments if player still in list
        for (int i = 0; i < slots.Count; i++)
        {
            var pid = _slotPlayerIds[i];
            if (!string.IsNullOrEmpty(pid) && available.TryGetValue(pid, out var member))
            {
                usedPlayers.Add(pid);
                EnsureRemotePlayer(i);
                UpdateChestLobby(slots[i], member);
                UpdateFriendBoardLobby(slots[i], member);
                ApplyRemotePositions(i, member.positions);
                ApplyIfChangedLobby(i, slots[i], member);
            }
            else if (!string.IsNullOrEmpty(pid))
            {
                _playerToSlot.Remove(pid);
                _slotPlayerIds[i] = null;
                _slotUpdatedAt[i] = null;
                UpdateChestLobby(slots[i], null);
                UpdateFriendBoardLobby(slots[i], null);
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
            UpdateChestLobby(slots[slotIndex], pick);
            UpdateFriendBoardLobby(slots[slotIndex], pick);
            ApplyRemotePositions(slotIndex, pick.positions);
            ApplyIfChangedLobby(slotIndex, slots[slotIndex], pick, force: true);
        }

        if (clearEmptySlots)
        {
            for (int i = 0; i < slots.Count; i++)
            {
            if (string.IsNullOrEmpty(_slotPlayerIds[i]))
            {
                ClearSlot(slots[i]);
                DisableRemotePlayer(i);
            }
        }
    }
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
        if (slot == null || slot.root == null)
            return;

        if (disableEmptySlots)
            slot.root.gameObject.SetActive(true);

        EnsureFieldIds(slot);
        ClearSlot(slot);

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

        var updatedAt = !string.IsNullOrEmpty(member.updatedAt)
            ? member.updatedAt
            : member.baseData?.updatedAt ?? "";

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
                if (string.IsNullOrEmpty(_slotPlayerIds[i]))
                    return i;
            }
            return -1;
        }

        var preferred = PreferredSlotIndex(playerId);
        if (preferred < 0) return -1;

        if (string.IsNullOrEmpty(_slotPlayerIds[preferred]))
            return preferred;

        for (int offset = 1; offset < slots.Count; offset++)
        {
            var idx = (preferred + offset) % slots.Count;
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
        if (slot == null || slot.root == null)
            return;

        if (disableEmptySlots)
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
        G.Player.transform.position = target.position;
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
}
