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

    private ZooBackendClient backend;
    private readonly Dictionary<string, int> _playerToSlot = new();
    private string[] _slotPlayerIds;
    private string[] _slotUpdatedAt;

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
                UpdateChest(slots[i], loc);
                UpdateFriendBoard(slots[i], loc);
                ApplyIfChanged(i, slots[i], loc);
            }
            else if (!string.IsNullOrEmpty(pid))
            {
                _playerToSlot.Remove(pid);
                _slotPlayerIds[i] = null;
                _slotUpdatedAt[i] = null;
                UpdateChest(slots[i], null);
                UpdateFriendBoard(slots[i], null);
                if (clearEmptySlots)
                    ClearSlot(slots[i]);
            }
        }

        // assign new players to free slots
        for (int i = 0; i < slots.Count; i++)
        {
            if (!string.IsNullOrEmpty(_slotPlayerIds[i])) continue;
            if (available.Count == 0) break;

            ZooLocationItem pick = null;
            foreach (var kv in available)
            {
                if (usedPlayers.Contains(kv.Key)) continue;
                pick = kv.Value;
                break;
            }

            if (pick == null) break;

            _slotPlayerIds[i] = pick.playerId;
            _playerToSlot[pick.playerId] = i;
            usedPlayers.Add(pick.playerId);
            UpdateChest(slots[i], pick);
            UpdateFriendBoard(slots[i], pick);
            ApplyIfChanged(i, slots[i], pick, force: true);
        }

        if (clearEmptySlots)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                if (string.IsNullOrEmpty(_slotPlayerIds[i]))
                    ClearSlot(slots[i]);
            }
        }
    }

    private void UpdateChest(RemoteBaseSlot slot, ZooLocationItem loc)
    {
        if (slot == null || slot.chest == null) return;
        if (loc == null)
        {
            slot.chest.SetRemoteTarget(null, false);
            return;
        }

        slot.chest.SetRemoteTarget(loc.friendCode, loc.isOnline);
    }

    private void UpdateFriendBoard(RemoteBaseSlot slot, ZooLocationItem loc)
    {
        if (slot == null || slot.friendBoard == null) return;
        if (loc == null)
        {
            slot.friendBoard.SetRemote(null, null, false, false);
            return;
        }

        slot.friendBoard.SetRemote(loc.friendCode, loc.displayName, loc.isFriend, loc.isOnline);
    }

    private void ApplySnapshotToSlot(RemoteBaseSlot slot, BaseSnapshotDto snapshot)
    {
        if (slot == null || slot.root == null)
            return;

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

    private void EnsureSlotState()
    {
        if (slots == null) return;
        if (_slotPlayerIds == null || _slotPlayerIds.Length != slots.Count)
            _slotPlayerIds = new string[slots.Count];
        if (_slotUpdatedAt == null || _slotUpdatedAt.Length != slots.Count)
            _slotUpdatedAt = new string[slots.Count];
    }

    private void ClearSlot(RemoteBaseSlot slot)
    {
        if (slot == null || slot.root == null)
            return;

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
}
