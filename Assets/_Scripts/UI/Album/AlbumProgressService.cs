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

    public bool IsRareUnlocked(RareType rareType)
    {
        if (!IsValidRareType(rareType))
            return false;
        return LoadFlag(BuildRareGlobalKey("RareUnlocked", rareType));
    }

    public bool HasRareMention(AlbumEntityType type, string id, RareType rareType)
    {
        if (!IsValidRareType(rareType))
            return false;

        var normalizedId = NormalizeId(id);
        if (string.IsNullOrEmpty(normalizedId))
            return false;

        return LoadFlag(BuildEntityRareKey("MentionRare", type, normalizedId, rareType));
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

                var viewedKey = BuildEntityRareKey("RareViewed", type, normalizedId, rareType);
                var mentionKey = BuildEntityRareKey("MentionRare", type, normalizedId, rareType);

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

        return false;
    }

    private void TrySubscribeRuntime()
    {
        if (!_inventorySubscribed && G.Inventory != null)
        {
            G.Inventory.ItemAdded.AddListener(OnInventoryItemAdded);
            _inventorySubscribed = true;
            HydrateFromInventory();
        }

        if (!_quickAccessSubscribed && G.QuickAccess != null)
        {
            G.QuickAccess.SwitchActiveItem.AddListener(OnHeldItemChanged);
            _quickAccessSubscribed = true;
            OnHeldItemChanged(G.QuickAccess.CurrentActive);
        }
    }

    private void UnsubscribeRuntime()
    {
        if (_inventorySubscribed && G.Inventory != null)
        {
            G.Inventory.ItemAdded.RemoveListener(OnInventoryItemAdded);
            _inventorySubscribed = false;
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

    private void OnInventoryItemAdded(InventoryItem item)
    {
        TryDiscoverFromInventoryItem(item);
    }

    private void OnHeldItemChanged(InventoryItem item)
    {
        TryDiscoverRareFromHeldItem(item);
    }

    private static bool TryMapItem(InventoryItem item, out AlbumEntityType type, out string id, out RareType rareType)
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

        id = NormalizeId(item.Name);
        if (string.IsNullOrEmpty(id))
        {
            var runtimeName = item.gameObject != null ? item.gameObject.name : item.name;
            id = NormalizeId(StripCloneSuffix(runtimeName));
        }

        rareType = item.RareType;
        return !string.IsNullOrEmpty(id);
    }

    private bool LoadFlag(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return false;

        if (G.Save != null)
            return G.Save.GetLevelStatus(key);

        return PlayerPrefs.GetInt(BuildFallbackPrefKey(key), 0) == 1;
    }

    private void SaveFlag(string key, bool value)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;

        if (G.Save != null)
        {
            G.Save.SaveLevelStatus(key, value);
            return;
        }

        PlayerPrefs.SetInt(BuildFallbackPrefKey(key), value ? 1 : 0);
        PlayerPrefs.Save();
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
