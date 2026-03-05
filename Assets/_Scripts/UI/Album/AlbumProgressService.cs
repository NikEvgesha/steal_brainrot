using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Events;

public enum AlbumEntityType
{
    Egg,
    Animal
}

public class AlbumProgressService : MonoBehaviour
{
    [Header("Persistence")]
    [SerializeField] private string savePrefix = "AlbumV1";
    [SerializeField] private bool debugLogs = false;

    public UnityEvent Changed = new UnityEvent();

    private bool _inventorySubscribed;
    private bool _quickAccessSubscribed;
    private bool _inventoryHydrated;

    private readonly Dictionary<string, bool> _runtimeFlags = new();
    private readonly Dictionary<string, bool> _pendingProviderSync = new();
    private readonly Dictionary<string, List<CatalogAliasCandidate>> _catalogAliasLookup = new(StringComparer.Ordinal);

    private bool _catalogAliasLookupReady;
    private int _catalogEggCount = -1;
    private int _catalogAnimalCount = -1;

    private struct CatalogAliasCandidate
    {
        public AlbumEntityType type;
        public string canonicalId;
        public RareType rareType;
    }

    private void Awake()
    {
        if (G.Album == null)
        {
            G.Album = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (G.Album != this)
        {
            Destroy(gameObject);
            return;
        }
    }

    private void OnEnable()
    {
        TrySubscribeRuntime();
    }

    private void OnDisable()
    {
        UnsubscribeRuntime();
    }

    private void Update()
    {
        if (!_inventorySubscribed || !_quickAccessSubscribed)
            TrySubscribeRuntime();

        TryHydrateInventoryWhenReady();
        TryFlushPendingProviderSync();

        // Safety net: some hand-switch flows may skip events; keep rare discovery in sync.
        if (_quickAccessSubscribed && G.QuickAccess != null)
            TryDiscoverRareFromHeldItem(G.QuickAccess.CurrentActive);
    }

    public bool IsDiscovered(AlbumEntityType type, string id)
    {
        return LoadFlag(BuildEntityKey("Discovered", type, id));
    }

    public bool IsRewardClaimed(AlbumEntityType type, string id)
    {
        return LoadFlag(BuildEntityKey("RewardClaimed", type, id));
    }

    public bool HasCardMention(AlbumEntityType type, string id)
    {
        return LoadFlag(BuildEntityKey("MentionCard", type, id));
    }

    public bool HasRewardMention(AlbumEntityType type, string id)
    {
        return LoadFlag(BuildEntityKey("MentionReward", type, id));
    }

    public bool IsRareRewardClaimed(AlbumEntityType type, RareType rareType)
    {
        if (!IsValidRareType(rareType))
            return true;

        return LoadFlag(BuildRareTabKey("RewardClaimedRare", type, rareType));
    }

    public bool HasRareRewardMention(AlbumEntityType type, RareType rareType)
    {
        if (!IsValidRareType(rareType))
            return false;

        if (IsRareRewardClaimed(type, rareType))
            return false;

        return LoadFlag(BuildRareTabKey("MentionRewardRare", type, rareType));
    }

    public bool IsRareUnlocked(RareType rareType)
    {
        if (!IsValidRareType(rareType))
            return false;
        return LoadFlag(BuildRareGlobalKey("RareUnlocked", rareType));
    }

    public bool IsRareSeenForEntity(AlbumEntityType type, string id, RareType rareType)
    {
        if (!IsValidRareType(rareType))
            return false;

        var normalizedId = NormalizeId(id);
        if (string.IsNullOrEmpty(normalizedId))
            return false;

        var relatedIds = GetRelatedEntityIds(type, normalizedId);
        for (var i = 0; i < relatedIds.Count; i++)
        {
            if (LoadFlag(BuildEntityRareKey("RareSeen", type, relatedIds[i], rareType)))
                return true;
        }

        return false;
    }

    public bool HasRareMention(AlbumEntityType type, string id, RareType rareType)
    {
        if (!IsValidRareType(rareType))
            return false;

        var normalizedId = NormalizeId(id);
        if (string.IsNullOrEmpty(normalizedId))
            return false;

        var relatedIds = GetRelatedEntityIds(type, normalizedId);
        for (var i = 0; i < relatedIds.Count; i++)
        {
            if (LoadFlag(BuildEntityRareKey("MentionRare", type, relatedIds[i], rareType)))
                return true;
        }

        return false;
    }

    public bool TryDiscoverFromInventoryItem(InventoryItem item)
    {
        if (!TryMapItem(item, out var type, out var id, out var rareType))
            return false;

        return TryDiscover(type, id, rareType);
    }

    public bool TryDiscoverRareFromHeldItem(InventoryItem item)
    {
        if (!TryMapItem(item, out var type, out var id, out var rareType))
            return false;

        return TryDiscoverRareType(type, id, rareType);
    }

    public bool TryDiscover(AlbumEntityType type, string id, RareType rareType)
    {
        var normalizedId = NormalizeId(id);
        if (string.IsNullOrEmpty(normalizedId))
            return false;

        var changed = false;
        if (!IsDiscovered(type, normalizedId))
        {
            SaveFlag(BuildEntityKey("Discovered", type, normalizedId), true);
            SaveFlag(BuildEntityKey("MentionCard", type, normalizedId), true);
            SaveFlag(BuildEntityKey("MentionReward", type, normalizedId), true);
            changed = true;
            if (debugLogs)
                Debug.Log($"[Album] Discovered {type}:{normalizedId}");
        }

        if (TryDiscoverRareType(type, normalizedId, rareType))
            changed = true;

        if (changed)
            Changed?.Invoke();

        return changed;
    }

    public bool TryDiscoverRareType(AlbumEntityType type, RareType rareType)
    {
        return TryDiscoverRareType(type, null, rareType);
    }

    public bool TryDiscoverRareType(AlbumEntityType type, string id, RareType rareType)
    {
        if (!IsValidRareType(rareType))
            return false;

        var normalizedId = NormalizeId(id);
        var changed = false;
        if (!IsRareUnlocked(rareType))
        {
            SaveFlag(BuildRareGlobalKey("RareUnlocked", rareType), true);
            changed = true;
            if (debugLogs)
                Debug.Log($"[Album] Rare unlocked {rareType}");
        }

        if (!string.IsNullOrEmpty(normalizedId))
        {
            var seenKey = BuildEntityRareKey("RareSeen", type, normalizedId, rareType);
            if (!LoadFlag(seenKey))
            {
                SaveFlag(seenKey, true);
                SaveFlag(BuildEntityRareKey("MentionRare", type, normalizedId, rareType), true);
                if (!IsRareRewardClaimed(type, rareType))
                    SaveFlag(BuildRareTabKey("MentionRewardRare", type, rareType), true);
                changed = true;
                if (debugLogs)
                    Debug.Log($"[Album] Rare seen for {type}:{normalizedId}:{rareType}");
            }
        }

        if (changed)
            Changed?.Invoke();

        return changed;
    }

    public void MarkCardViewed(AlbumEntityType type, string id)
    {
        var normalizedId = NormalizeId(id);
        if (string.IsNullOrEmpty(normalizedId))
            return;

        var viewedKey = BuildEntityKey("Viewed", type, normalizedId);
        var mentionKey = BuildEntityKey("MentionCard", type, normalizedId);
        var changed = false;

        if (!LoadFlag(viewedKey))
        {
            SaveFlag(viewedKey, true);
            changed = true;
        }

        if (LoadFlag(mentionKey))
        {
            SaveFlag(mentionKey, false);
            changed = true;
        }

        if (changed)
            Changed?.Invoke();
    }

    public void MarkRareViewed(AlbumEntityType type, RareType rareType)
    {
        if (!IsValidRareType(rareType))
            return;

        var mentionKey = BuildRareTabKey("MentionRare", type, rareType);
        if (!LoadFlag(mentionKey))
            return;

        SaveFlag(mentionKey, false);
        Changed?.Invoke();
    }

    public void MarkRareViewedForEntities(AlbumEntityType type, RareType rareType, IReadOnlyList<string> entityIds)
    {
        if (!IsValidRareType(rareType))
            return;

        var changed = false;
        if (entityIds != null)
        {
            for (var i = 0; i < entityIds.Count; i++)
            {
                var normalizedId = NormalizeId(entityIds[i]);
                if (string.IsNullOrEmpty(normalizedId))
                    continue;

                var relatedIds = GetRelatedEntityIds(type, normalizedId);
                for (var relatedIndex = 0; relatedIndex < relatedIds.Count; relatedIndex++)
                {
                    var relatedId = relatedIds[relatedIndex];
                    var viewedKey = BuildEntityRareKey("RareViewed", type, relatedId, rareType);
                    var mentionKey = BuildEntityRareKey("MentionRare", type, relatedId, rareType);

                    if (!LoadFlag(viewedKey))
                    {
                        SaveFlag(viewedKey, true);
                        changed = true;
                    }

                    if (LoadFlag(mentionKey))
                    {
                        SaveFlag(mentionKey, false);
                        changed = true;
                    }
                }
            }
        }

        // Legacy key cleanup from the previous tab-level mention format.
        var legacyMentionKey = BuildRareTabKey("MentionRare", type, rareType);
        if (LoadFlag(legacyMentionKey))
        {
            SaveFlag(legacyMentionKey, false);
            changed = true;
        }

        if (changed)
            Changed?.Invoke();
    }

    public bool CanClaimReward(AlbumEntityType type, string id)
    {
        var normalizedId = NormalizeId(id);
        if (string.IsNullOrEmpty(normalizedId))
            return false;

        return IsDiscovered(type, normalizedId) && !IsRewardClaimed(type, normalizedId);
    }

    public bool TryClaimReward(AlbumEntityType type, string id)
    {
        var normalizedId = NormalizeId(id);
        if (string.IsNullOrEmpty(normalizedId))
            return false;

        if (!CanClaimReward(type, normalizedId))
            return false;

        SaveFlag(BuildEntityKey("RewardClaimed", type, normalizedId), true);
        SaveFlag(BuildEntityKey("MentionReward", type, normalizedId), false);
        Changed?.Invoke();
        return true;
    }

    public bool TryClaimRareReward(AlbumEntityType type, RareType rareType)
    {
        if (!IsValidRareType(rareType))
            return false;

        if (IsRareRewardClaimed(type, rareType))
            return false;

        SaveFlag(BuildRareTabKey("RewardClaimedRare", type, rareType), true);
        SaveFlag(BuildRareTabKey("MentionRewardRare", type, rareType), false);
        Changed?.Invoke();
        return true;
    }

    public bool HasAnyTabMention(AlbumEntityType type, IReadOnlyList<string> knownIds, IReadOnlyList<RareType> knownRareTypes)
    {
        if (knownIds != null)
        {
            for (var i = 0; i < knownIds.Count; i++)
            {
                var id = knownIds[i];
                if (HasCardMention(type, id) || HasRewardMention(type, id))
                    return true;

                if (knownRareTypes == null)
                    continue;

                for (var j = 0; j < knownRareTypes.Count; j++)
                {
                    if (HasRareMention(type, id, knownRareTypes[j]))
                        return true;
                }
            }
        }

        if (knownRareTypes != null)
        {
            for (var i = 0; i < knownRareTypes.Count; i++)
            {
                var rareType = knownRareTypes[i];
                if (HasRareRewardMention(type, rareType))
                    return true;

                if (IsRareRewardClaimed(type, rareType) || knownIds == null)
                    continue;

                for (var idIndex = 0; idIndex < knownIds.Count; idIndex++)
                {
                    if (IsRareSeenForEntity(type, knownIds[idIndex], rareType))
                        return true;
                }
            }
        }

        return false;
    }

    private void TrySubscribeRuntime()
    {
        if (!_inventorySubscribed && G.Inventory != null)
        {
            G.Inventory.ItemAdded.AddListener(OnInventoryItemAdded);
            _inventorySubscribed = true;
            _inventoryHydrated = false;
        }

        if (!_quickAccessSubscribed && G.QuickAccess != null)
        {
            G.QuickAccess.SwitchActiveItem.AddListener(OnHeldItemChanged);
            _quickAccessSubscribed = true;
            OnHeldItemChanged(G.QuickAccess.CurrentActive);
        }

        TryHydrateInventoryWhenReady();
    }

    private void UnsubscribeRuntime()
    {
        if (_inventorySubscribed && G.Inventory != null)
        {
            G.Inventory.ItemAdded.RemoveListener(OnInventoryItemAdded);
            _inventorySubscribed = false;
            _inventoryHydrated = false;
        }

        if (_quickAccessSubscribed && G.QuickAccess != null)
        {
            G.QuickAccess.SwitchActiveItem.RemoveListener(OnHeldItemChanged);
            _quickAccessSubscribed = false;
        }
    }

    private void HydrateFromInventory()
    {
        if (G.Inventory == null || !G.Inventory.IsInitialized)
            return;

        var eggs = G.Inventory.GetItems(Item.Egg);
        if (eggs != null)
        {
            for (var i = 0; i < eggs.Count; i++)
                TryDiscoverFromInventoryItem(eggs[i]);
        }

        var animals = G.Inventory.GetItems(Item.Brainrot);
        if (animals != null)
        {
            for (var i = 0; i < animals.Count; i++)
                TryDiscoverFromInventoryItem(animals[i]);
        }
    }

    private void TryHydrateInventoryWhenReady()
    {
        if (_inventoryHydrated)
            return;

        if (G.Inventory == null || !G.Inventory.IsInitialized)
            return;

        HydrateFromInventory();
        _inventoryHydrated = true;
    }

    private void OnInventoryItemAdded(InventoryItem item)
    {
        TryDiscoverFromInventoryItem(item);
    }

    private void OnHeldItemChanged(InventoryItem item)
    {
        TryDiscoverRareFromHeldItem(item);
    }

    private bool TryMapItem(InventoryItem item, out AlbumEntityType type, out string id, out RareType rareType)
    {
        type = AlbumEntityType.Egg;
        id = null;
        rareType = RareType.RareType;

        if (item == null)
            return false;

        if (item.Type == Item.Egg)
            type = AlbumEntityType.Egg;
        else if (item.Type == Item.Brainrot)
            type = AlbumEntityType.Animal;
        else
            return false;

        var normalizedItemName = NormalizeId(item.Name);
        var runtimeName = item.gameObject != null ? item.gameObject.name : item.name;
        var normalizedRuntimeName = NormalizeId(StripCloneSuffix(runtimeName));

        var resolvedByCatalog = TryResolveCatalogMapping(
            type,
            normalizedItemName,
            normalizedRuntimeName,
            item.RareType,
            out var mappedId,
            out var mappedRareType,
            out var mappedRareIsReliable);

        id = resolvedByCatalog ? mappedId : normalizedItemName;
        if (string.IsNullOrEmpty(id))
            id = normalizedRuntimeName;

        rareType = item.RareType;
        var hasDynamicRare = TryMapDynamicWeightToRare(item, out var dynamicRareType);
        if (hasDynamicRare)
            rareType = dynamicRareType;

        if (!hasDynamicRare)
        {
            if (!IsValidRareType(rareType) || (mappedRareIsReliable && IsValidRareType(mappedRareType) && mappedRareType != rareType))
                rareType = mappedRareType;
        }
        else if (!IsValidRareType(rareType) && IsValidRareType(mappedRareType))
        {
            rareType = mappedRareType;
        }

        if (debugLogs)
        {
            Debug.Log(
                $"[Album] Map item type={type} srcName='{item.Name}' runtime='{runtimeName}' " +
                $"-> id='{id}', rare={rareType} (itemRare={item.RareType}, dynamicWeightRare={dynamicRareType}, catalog={mappedRareType}, byCatalog={resolvedByCatalog})");
        }

        return !string.IsNullOrEmpty(id);
    }

    private static bool TryMapDynamicWeightToRare(InventoryItem item, out RareType rareType)
    {
        rareType = RareType.RareType;
        if (item == null)
            return false;

        var weight = -1f;
        if (item is Egg egg)
            weight = egg.Data.DinamicData.WeightMultiplier;
        else if (item is Brainrot brainrot)
            weight = brainrot.DinamicData.WeightMultiplier;

        if (weight <= 0f)
            return false;

        // Quality/weight based rarity mapping:
        // 1.0x..1.29x Common
        // 1.3x..1.59x Uncommon
        // 1.6x..1.89x Rare
        // 1.9x..2.19x Epic
        // 2.2x..2.49x Legendary
        // 2.5x+ Mythic
        if (weight >= 2.5f)
            rareType = RareType.Mythic;
        else if (weight >= 2.2f)
            rareType = RareType.Legendary;
        else if (weight >= 1.9f)
            rareType = RareType.Epic;
        else if (weight >= 1.6f)
            rareType = RareType.Rare;
        else if (weight >= 1.3f)
            rareType = RareType.Uncommon;
        else
            rareType = RareType.Common;

        return true;
    }

    private bool TryResolveCatalogMapping(
        AlbumEntityType type,
        string normalizedItemName,
        string normalizedRuntimeName,
        RareType itemRareType,
        out string canonicalId,
        out RareType mappedRareType,
        out bool mappedRareIsReliable)
    {
        canonicalId = string.Empty;
        mappedRareType = RareType.RareType;
        mappedRareIsReliable = false;

        EnsureCatalogAliasLookup();
        if (!_catalogAliasLookupReady)
            return false;

        if (TryResolveAlias(type, normalizedRuntimeName, itemRareType, out var fromRuntimeName, out var runtimeUnique))
        {
            canonicalId = fromRuntimeName.canonicalId;
            mappedRareType = fromRuntimeName.rareType;
            mappedRareIsReliable = runtimeUnique;
            return true;
        }

        var runtimeFamilyAlias = ComputeFamilyId(normalizedRuntimeName);
        if (TryResolveAlias(type, runtimeFamilyAlias, itemRareType, out var fromRuntimeFamily, out var runtimeFamilyUnique))
        {
            canonicalId = fromRuntimeFamily.canonicalId;
            mappedRareType = fromRuntimeFamily.rareType;
            mappedRareIsReliable = runtimeFamilyUnique;
            return true;
        }

        if (TryResolveAlias(type, normalizedItemName, itemRareType, out var fromItemName, out var itemUnique))
        {
            canonicalId = fromItemName.canonicalId;
            mappedRareType = fromItemName.rareType;
            mappedRareIsReliable = itemUnique;
            return true;
        }

        return false;
    }

    private bool TryResolveAlias(
        AlbumEntityType type,
        string alias,
        RareType itemRareType,
        out CatalogAliasCandidate resolved,
        out bool unique)
    {
        resolved = default;
        unique = false;

        if (string.IsNullOrEmpty(alias))
            return false;

        if (!_catalogAliasLookup.TryGetValue(alias, out var list) || list == null || list.Count == 0)
            return false;

        var filtered = new List<CatalogAliasCandidate>();
        for (var i = 0; i < list.Count; i++)
        {
            var candidate = list[i];
            if (candidate.type != type)
                continue;

            var alreadyAdded = false;
            for (var j = 0; j < filtered.Count; j++)
            {
                if (IsSameCandidate(filtered[j], candidate))
                {
                    alreadyAdded = true;
                    break;
                }
            }

            if (!alreadyAdded)
                filtered.Add(candidate);
        }

        if (filtered.Count == 0)
            return false;

        unique = filtered.Count == 1;

        if (IsValidRareType(itemRareType))
        {
            for (var i = 0; i < filtered.Count; i++)
            {
                if (filtered[i].rareType == itemRareType)
                {
                    resolved = filtered[i];
                    return true;
                }
            }
        }

        resolved = filtered[0];
        return true;
    }

    private static bool IsSameCandidate(CatalogAliasCandidate a, CatalogAliasCandidate b)
    {
        return a.type == b.type &&
               a.rareType == b.rareType &&
               string.Equals(a.canonicalId, b.canonicalId, StringComparison.Ordinal);
    }

    private void EnsureCatalogAliasLookup()
    {
        if (G.Storage == null)
            return;

        var eggs = G.Storage.GetAllEggPrefabs();
        var animals = G.Storage.GetAllPetPrefabs();

        var eggCount = eggs != null ? eggs.Count : 0;
        var animalCount = animals != null ? animals.Count : 0;

        if (_catalogAliasLookupReady && eggCount == _catalogEggCount && animalCount == _catalogAnimalCount)
            return;

        _catalogAliasLookup.Clear();

        if (eggs != null)
        {
            for (var i = 0; i < eggs.Count; i++)
                AddCatalogAliases(AlbumEntityType.Egg, eggs[i]);
        }

        if (animals != null)
        {
            for (var i = 0; i < animals.Count; i++)
                AddCatalogAliases(AlbumEntityType.Animal, animals[i]);
        }

        _catalogEggCount = eggCount;
        _catalogAnimalCount = animalCount;
        _catalogAliasLookupReady = true;
    }

    private void AddCatalogAliases(AlbumEntityType type, InventoryItem item)
    {
        if (item == null)
            return;

        var canonicalId = NormalizeId(item.Name);
        if (string.IsNullOrEmpty(canonicalId))
            canonicalId = NormalizeId(item.name);
        if (string.IsNullOrEmpty(canonicalId))
            return;

        var candidate = new CatalogAliasCandidate
        {
            type = type,
            canonicalId = canonicalId,
            rareType = item.RareType
        };

        AddCatalogAlias(canonicalId, candidate);
        AddCatalogAlias(NormalizeId(item.Name), candidate);
        AddCatalogAlias(NormalizeId(item.name), candidate);

    }

    private void AddCatalogAlias(string alias, CatalogAliasCandidate candidate)
    {
        if (string.IsNullOrEmpty(alias))
            return;

        if (!_catalogAliasLookup.TryGetValue(alias, out var list))
        {
            list = new List<CatalogAliasCandidate>();
            _catalogAliasLookup.Add(alias, list);
        }

        for (var i = 0; i < list.Count; i++)
        {
            if (IsSameCandidate(list[i], candidate))
                return;
        }

        list.Add(candidate);
    }

    private List<string> GetRelatedEntityIds(AlbumEntityType type, string id)
    {
        var normalizedId = NormalizeId(id);
        var related = new List<string>();
        if (string.IsNullOrEmpty(normalizedId))
            return related;

        var seen = new HashSet<string>(StringComparer.Ordinal);
        AddRelatedId(normalizedId, seen, related);

        EnsureCatalogAliasLookup();
        if (!_catalogAliasLookupReady)
            return related;

        AddRelatedFromAlias(type, normalizedId, seen, related);

        return related;
    }

    private void AddRelatedFromAlias(AlbumEntityType type, string alias, ISet<string> seen, ICollection<string> related)
    {
        if (string.IsNullOrEmpty(alias))
            return;

        if (!_catalogAliasLookup.TryGetValue(alias, out var candidates) || candidates == null)
            return;

        for (var i = 0; i < candidates.Count; i++)
        {
            var candidate = candidates[i];
            if (candidate.type != type)
                continue;

            AddRelatedId(candidate.canonicalId, seen, related);
        }
    }

    private static void AddRelatedId(string id, ISet<string> seen, ICollection<string> related)
    {
        var normalizedId = NormalizeId(id);
        if (string.IsNullOrEmpty(normalizedId))
            return;

        if (!seen.Add(normalizedId))
            return;

        related.Add(normalizedId);
    }

    private static string ComputeFamilyId(string id)
    {
        var normalized = NormalizeId(id);
        if (string.IsNullOrEmpty(normalized))
            return string.Empty;

        var end = normalized.Length - 1;
        while (end >= 0 && char.IsDigit(normalized[end]))
            end--;

        if (end < 0 || end == normalized.Length - 1)
            return normalized;

        var trimmed = normalized.Substring(0, end + 1).TrimEnd('_');
        return string.IsNullOrEmpty(trimmed) ? normalized : trimmed;
    }

    private bool LoadFlag(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return false;

        if (G.Save != null && G.Save.IsReady)
        {
            if (_pendingProviderSync.TryGetValue(key, out var pendingValue))
                return pendingValue;

            var providerValue = G.Save.GetLevelStatus(key);
            _runtimeFlags[key] = providerValue;
            return providerValue;
        }

        if (_runtimeFlags.TryGetValue(key, out var runtimeValue))
            return runtimeValue;

        var fallbackValue = PlayerPrefs.GetInt(BuildFallbackPrefKey(key), 0) == 1;
        _runtimeFlags[key] = fallbackValue;
        return fallbackValue;
    }

    private void SaveFlag(string key, bool value)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;

        _runtimeFlags[key] = value;
        PlayerPrefs.SetInt(BuildFallbackPrefKey(key), value ? 1 : 0);
        PlayerPrefs.Save();

        if (G.Save != null && G.Save.IsReady)
        {
            G.Save.SaveLevelStatus(key, value);
            _pendingProviderSync.Remove(key);
            return;
        }

        _pendingProviderSync[key] = value;
    }

    private void TryFlushPendingProviderSync()
    {
        if (_pendingProviderSync.Count == 0)
            return;

        if (G.Save == null || !G.Save.IsReady)
            return;

        foreach (var kv in _pendingProviderSync)
            G.Save.SaveLevelStatus(kv.Key, kv.Value);

        _pendingProviderSync.Clear();
    }

    private string BuildEntityKey(string tag, AlbumEntityType type, string id)
    {
        return $"{savePrefix}.{tag}.{type}.{NormalizeId(id)}";
    }

    private string BuildRareGlobalKey(string tag, RareType rareType)
    {
        return $"{savePrefix}.{tag}.{NormalizeRare(rareType)}";
    }

    private string BuildEntityRareKey(string tag, AlbumEntityType type, string id, RareType rareType)
    {
        return $"{savePrefix}.{tag}.{type}.{NormalizeId(id)}.{NormalizeRare(rareType)}";
    }

    private string BuildRareTabKey(string tag, AlbumEntityType type, RareType rareType)
    {
        return $"{savePrefix}.{tag}.{type}.{NormalizeRare(rareType)}";
    }

    private static string BuildFallbackPrefKey(string key)
    {
        return "AlbumFallback." + key;
    }

    private static string NormalizeRare(RareType rareType)
    {
        return NormalizeId(rareType.ToString());
    }

    public static string NormalizeId(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        var src = raw.Trim();
        var sb = new StringBuilder(src.Length);
        var prevUnderscore = false;
        for (var i = 0; i < src.Length; i++)
        {
            var ch = char.ToLowerInvariant(src[i]);
            if (char.IsLetterOrDigit(ch))
            {
                sb.Append(ch);
                prevUnderscore = false;
            }
            else if (!prevUnderscore)
            {
                sb.Append('_');
                prevUnderscore = true;
            }
        }

        var normalized = sb.ToString().Trim('_');
        return normalized;
    }

    public static List<RareType> GetSupportedRareTypes()
    {
        var result = new List<RareType>();
        var values = (RareType[])Enum.GetValues(typeof(RareType));
        for (var i = 0; i < values.Length; i++)
        {
            var rare = values[i];
            if (!IsValidRareType(rare))
                continue;
            result.Add(rare);
        }
        return result;
    }

    private static bool IsValidRareType(RareType rareType)
    {
        return rareType != RareType.RareType;
    }

    private static string StripCloneSuffix(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var trimmed = value.Trim();
        const string suffix = "(Clone)";
        if (trimmed.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed.Substring(0, trimmed.Length - suffix.Length).TrimEnd();

        return trimmed;
    }
}
