using System;
using System.Collections.Generic;
using System.Globalization;
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
    [SerializeField] private string dateSavePrefix = "AlbumDateV1";
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

    public bool TryGetFirstDiscoveryDate(AlbumEntityType type, string id, out DateTimeOffset discoveredAtUtc)
    {
        discoveredAtUtc = default;

        var normalizedId = NormalizeId(id);
        if (string.IsNullOrEmpty(normalizedId))
            return false;

        var key = BuildEntityKey("FirstDiscoveredAt", type, normalizedId);
        if (TryLoadTimestamp(key, out discoveredAtUtc))
            return true;

        if (!IsDiscovered(type, normalizedId))
            return false;

        discoveredAtUtc = DateTimeOffset.UtcNow;
        SaveTimestampIfMissing(key, discoveredAtUtc);
        return true;
    }

    public bool TryGetFirstElementDiscoveryDate(AlbumEntityType type, string id, ElementType elementType, out DateTimeOffset discoveredAtUtc)
    {
        discoveredAtUtc = default;
        if (!IsValidElementType(elementType))
            return false;

        var normalizedId = NormalizeId(id);
        if (string.IsNullOrEmpty(normalizedId))
            return false;

        var key = BuildEntityElementKey("FirstElementSeenAt", type, normalizedId, elementType);
        if (TryLoadTimestamp(key, out discoveredAtUtc))
            return true;

        if (LoadFlag(BuildEntityElementKey("ElementSeen", type, normalizedId, elementType)))
        {
            discoveredAtUtc = DateTimeOffset.UtcNow;
            SaveTimestampIfMissing(key, discoveredAtUtc);
            return true;
        }

        return TryGetFirstDiscoveryDate(type, normalizedId, out discoveredAtUtc);
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

    public bool IsElementUnlocked(ElementType elementType)
    {
        if (!IsValidElementType(elementType))
            return false;
        return LoadFlag(BuildElementGlobalKey("ElementUnlocked", elementType));
    }

    public bool IsElementSeen(AlbumEntityType type, string id, ElementType elementType)
    {
        if (!IsValidElementType(elementType))
            return false;

        var normalizedId = NormalizeId(id);
        if (string.IsNullOrEmpty(normalizedId))
            return false;

        return LoadFlag(BuildEntityElementKey("ElementSeen", type, normalizedId, elementType));
    }

    public bool HasElementMention(AlbumEntityType type, string id, ElementType elementType)
    {
        if (!IsValidElementType(elementType))
            return false;

        var normalizedId = NormalizeId(id);
        if (string.IsNullOrEmpty(normalizedId))
            return false;

        return LoadFlag(BuildEntityElementKey("MentionElement", type, normalizedId, elementType));
    }

    public bool TryDiscoverFromInventoryItem(InventoryItem item)
    {
        if (!TryMapItem(item, out var type, out var id, out var elementType))
            return false;

        return TryDiscover(type, id, elementType);
    }

    public bool TryDiscoverElementFromHeldItem(InventoryItem item)
    {
        if (!TryMapItem(item, out var type, out var id, out var elementType))
            return false;

        return TryDiscoverElementType(type, id, elementType);
    }

    public bool TryDiscover(AlbumEntityType type, string id, ElementType elementType)
    {
        var normalizedId = NormalizeId(id);
        if (string.IsNullOrEmpty(normalizedId))
            return false;

        var resolvedElementType = ResolveElementTypeFallback(type, normalizedId, elementType);

        var changed = false;
        if (!IsDiscovered(type, normalizedId))
        {
            SaveFlag(BuildEntityKey("Discovered", type, normalizedId), true);
            SaveTimestampIfMissing(BuildEntityKey("FirstDiscoveredAt", type, normalizedId), DateTimeOffset.UtcNow);
            SaveFlag(BuildEntityKey("MentionCard", type, normalizedId), true);
            SaveFlag(BuildEntityKey("MentionReward", type, normalizedId), true);
            changed = true;
            if (debugLogs)
                Debug.Log($"[Album] Discovered {type}:{normalizedId}");
        }

        if (TryDiscoverElementType(type, normalizedId, resolvedElementType))
            changed = true;

        if (changed)
            Changed?.Invoke();

        return changed;
    }

    public bool TryDiscoverElementType(AlbumEntityType type, ElementType elementType)
    {
        return TryDiscoverElementType(type, null, elementType);
    }

    public bool TryDiscoverElementType(AlbumEntityType type, string id, ElementType elementType)
    {
        var normalizedId = NormalizeId(id);
        elementType = ResolveElementTypeFallback(type, normalizedId, elementType);
        if (!IsValidElementType(elementType))
            return false;

        var changed = false;
        if (!IsElementUnlocked(elementType))
        {
            SaveFlag(BuildElementGlobalKey("ElementUnlocked", elementType), true);
            changed = true;
            if (debugLogs)
                Debug.Log($"[Album] Element unlocked {elementType}");
        }

        if (!string.IsNullOrEmpty(normalizedId))
        {
            var seenKey = BuildEntityElementKey("ElementSeen", type, normalizedId, elementType);
            if (!LoadFlag(seenKey))
            {
                SaveFlag(seenKey, true);
                SaveTimestampIfMissing(BuildEntityElementKey("FirstElementSeenAt", type, normalizedId, elementType), DateTimeOffset.UtcNow);
                SaveFlag(BuildEntityElementKey("MentionElement", type, normalizedId, elementType), true);
                changed = true;
                if (debugLogs)
                    Debug.Log($"[Album] Element seen for {type}:{normalizedId}:{elementType}");
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

    public void MarkElementViewed(AlbumEntityType type, ElementType elementType)
    {
        if (!IsValidElementType(elementType))
            return;

        var mentionKey = BuildElementTabKey("MentionElement", type, elementType);
        if (!LoadFlag(mentionKey))
            return;

        SaveFlag(mentionKey, false);
        Changed?.Invoke();
    }

    public void MarkElementViewedForEntities(AlbumEntityType type, ElementType elementType, IReadOnlyList<string> entityIds)
    {
        if (!IsValidElementType(elementType))
            return;

        var changed = false;
        if (entityIds != null)
        {
            for (var i = 0; i < entityIds.Count; i++)
            {
                var normalizedId = NormalizeId(entityIds[i]);
                if (string.IsNullOrEmpty(normalizedId))
                    continue;

                var viewedKey = BuildEntityElementKey("ElementViewed", type, normalizedId, elementType);
                var mentionKey = BuildEntityElementKey("MentionElement", type, normalizedId, elementType);

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
        var legacyMentionKey = BuildElementTabKey("MentionElement", type, elementType);
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

    public bool HasAnyTabMention(AlbumEntityType type, IReadOnlyList<string> knownIds, IReadOnlyList<ElementType> knownElementTypes)
    {
        if (knownIds != null)
        {
            for (var i = 0; i < knownIds.Count; i++)
            {
                var id = knownIds[i];
                if (HasCardMention(type, id) || HasRewardMention(type, id))
                    return true;

                if (knownElementTypes == null)
                    continue;

                for (var j = 0; j < knownElementTypes.Count; j++)
                {
                    if (HasElementMention(type, id, knownElementTypes[j]))
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
        TryDiscoverElementFromHeldItem(item);
    }

    private static bool TryMapItem(InventoryItem item, out AlbumEntityType type, out string id, out ElementType elementType)
    {
        type = AlbumEntityType.Egg;
        id = null;
        elementType = ElementType.ElementType;

        if (item == null)
            return false;

        if (item.Type == Item.Egg)
            type = AlbumEntityType.Egg;
        else if (item.Type == Item.Brainrot)
            type = AlbumEntityType.Animal;
        else
            return false;

        id = ResolveItemId(item);
        elementType = ResolveElementTypeFallback(type, id, ResolveItemElementType(item));
        return !string.IsNullOrEmpty(id);
    }

    private static string ResolveItemId(InventoryItem item)
    {
        if (item == null)
            return string.Empty;

        var normalized = NormalizeRuntimeId(item.Name);
        if (!string.IsNullOrEmpty(normalized))
            return normalized;

        return NormalizeRuntimeId(item.gameObject != null ? item.gameObject.name : null);
    }

    private static string NormalizeRuntimeId(string raw)
    {
        var normalized = NormalizeId(raw);
        if (normalized.EndsWith("_clone", StringComparison.Ordinal))
            normalized = normalized.Substring(0, normalized.Length - "_clone".Length).Trim('_');

        return normalized;
    }

    private static ElementType ResolveItemElementType(InventoryItem item)
    {
        if (item == null)
            return ElementType.ElementType;

        if (item is Egg egg)
            return egg.Data.DinamicData.ElementType;
        if (item is Brainrot brainrot)
            return brainrot.DinamicData.ElementType;

        if (item.TryGetComponent<Egg>(out var eggComponent))
            return eggComponent.Data.DinamicData.ElementType;
        if (item.TryGetComponent<Brainrot>(out var brainrotComponent))
            return brainrotComponent.DinamicData.ElementType;

        return ElementType.ElementType;
    }

    private static ElementType ResolveElementTypeFallback(AlbumEntityType type, string normalizedId, ElementType elementType)
    {
        if (IsValidElementType(elementType))
            return elementType;

        if (string.IsNullOrEmpty(normalizedId))
            return elementType;

        var storage = G.Storage;
        if (storage == null)
            return elementType;

        if (type == AlbumEntityType.Egg)
        {
            var eggs = storage.GetAllEggPrefabs();
            for (var i = 0; i < eggs.Count; i++)
            {
                var egg = eggs[i];
                if (egg == null)
                    continue;

                var candidateId = NormalizeRuntimeId(egg.Name);
                if (string.IsNullOrEmpty(candidateId))
                    candidateId = NormalizeRuntimeId(egg.name);
                if (!string.Equals(candidateId, normalizedId, StringComparison.Ordinal))
                    continue;

                var candidateElement = egg.Data.DinamicData.ElementType;
                if (IsValidElementType(candidateElement))
                    return candidateElement;
            }
        }
        else
        {
            var pets = storage.GetAllPetPrefabs();
            for (var i = 0; i < pets.Count; i++)
            {
                var pet = pets[i];
                if (pet == null)
                    continue;

                var candidateId = NormalizeRuntimeId(pet.Name);
                if (string.IsNullOrEmpty(candidateId))
                    candidateId = NormalizeRuntimeId(pet.name);
                if (!string.Equals(candidateId, normalizedId, StringComparison.Ordinal))
                    continue;

                var candidateElement = pet.DinamicData.ElementType;
                if (IsValidElementType(candidateElement))
                    return candidateElement;
            }
        }

        return elementType;
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

    private string BuildElementGlobalKey(string tag, ElementType elementType)
    {
        return $"{savePrefix}.{tag}.{NormalizeElement(elementType)}";
    }

    private string BuildEntityElementKey(string tag, AlbumEntityType type, string id, ElementType elementType)
    {
        return $"{savePrefix}.{tag}.{type}.{NormalizeId(id)}.{NormalizeElement(elementType)}";
    }

    private string BuildElementTabKey(string tag, AlbumEntityType type, ElementType elementType)
    {
        return $"{savePrefix}.{tag}.{type}.{NormalizeElement(elementType)}";
    }

    private static string BuildFallbackPrefKey(string key)
    {
        return "AlbumFallback." + key;
    }

    private string BuildDatePrefKey(string key)
    {
        return $"{dateSavePrefix}.{key}";
    }

    private bool TryLoadTimestamp(string key, out DateTimeOffset value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(key))
            return false;

        var raw = PlayerPrefs.GetString(BuildDatePrefKey(key), string.Empty);
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        if (long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
        {
            try
            {
                // Old builds could store ticks; new builds store unix seconds.
                value = number > 253402300799L
                    ? new DateTimeOffset(number, TimeSpan.Zero)
                    : DateTimeOffset.FromUnixTimeSeconds(number);
                return true;
            }
            catch
            {
                // Ignore malformed values and fallback to string parse.
            }
        }

        if (DateTimeOffset.TryParse(
                raw,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed))
        {
            value = parsed;
            return true;
        }

        return false;
    }

    private void SaveTimestampIfMissing(string key, DateTimeOffset value)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;

        if (TryLoadTimestamp(key, out _))
            return;

        PlayerPrefs.SetString(
            BuildDatePrefKey(key),
            value.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture));
        PlayerPrefs.Save();
    }

    private static string NormalizeElement(ElementType elementType)
    {
        return NormalizeId(elementType.ToString());
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

    public static List<ElementType> GetSupportedElementTypes()
    {
        var result = new List<ElementType>();
        var values = (ElementType[])Enum.GetValues(typeof(ElementType));
        for (var i = 0; i < values.Length; i++)
        {
            var elementType = values[i];
            if (!IsValidElementType(elementType))
                continue;
            result.Add(elementType);
        }
        return result;
    }

    private static bool IsValidElementType(ElementType elementType)
    {
        return elementType != ElementType.ElementType;
    }
}
