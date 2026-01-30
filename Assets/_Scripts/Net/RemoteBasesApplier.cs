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
        public bool lockCells = true;
        public bool applyLand = true;
    }

    [Header("Slots (5 remote bases)")]
    [SerializeField] private List<RemoteBaseSlot> slots = new();

    [Header("Behavior")]
    [SerializeField] private bool applyOnStart = true;
    [SerializeField] private bool clearEmptySlots = true;

    private ZooBackendClient backend;

    private void Awake()
    {
        if (backend == null) backend = G.Backend;
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

        for (int i = 0; i < slots.Count; i++)
        {
            if (i < locations.Count)
                ApplySnapshotToSlot(slots[i], locations[i]?.baseData);
            else if (clearEmptySlots)
                ClearSlot(slots[i]);
        }
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

        if (slot.applyLand && snapshot.land != null)
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

        var prefab = G.Storage.GetEgg(id);
        if (prefab == null)
            return;

        var egg = Instantiate(prefab, cell.transform);
        egg.SetData(dinamic);
        egg.OnInventoryAdd();
    }

    private void SpawnBrainrot(FieldCell cell, string id, BrainrotDinamicData dinamic, long incomeLastTime)
    {
        if (string.IsNullOrEmpty(id))
            return;

        var prefab = G.Storage.GetPet(id);
        if (prefab == null)
            return;

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
