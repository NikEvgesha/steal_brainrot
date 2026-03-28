#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using MirraGames.SDK;
using UnityEditor;
using UnityEngine;

public static class AlbumSaveToolsEditor
{
    private const string DefaultSavePrefix = "AlbumV1";
    private const string DefaultDateSavePrefix = "AlbumDateV1";

    [MenuItem("Tools/Save/Clear Album Saves")]
    private static void ClearAlbumSavesMenu()
    {
        if (!EditorUtility.DisplayDialog(
                "Clear Album Saves",
                "Clear album progress (eggs/animals, elements, rewards, dates)?\n\nThis action cannot be undone.",
                "Clear",
                "Cancel"))
        {
            return;
        }

        var result = ClearAlbumSavesInternal();
        var message = result.success
            ? $"Album saves cleared.\nEntities: {result.entityCount}\nAlbum keys processed: {result.albumKeysProcessed}\nDate keys deleted: {result.dateKeysDeleted}\nPlayerPrefs keys deleted: {result.playerPrefsKeysDeleted}"
            : result.error;

        if (result.success)
            Debug.Log("[AlbumSaveTools] " + message);
        else
            Debug.LogWarning("[AlbumSaveTools] " + message);

        EditorUtility.DisplayDialog("Album Saves", message, "OK");
    }

    [MenuItem("Tools/Save/Clear ALL Save Data")]
    private static void ClearAllSaveDataMenu()
    {
        if (!EditorUtility.DisplayDialog(
                "Clear ALL Save Data",
                "This will wipe all game saves.\n\nIncludes:\n- PlayerPrefs (local)\n- MirraSDK data\n\nThis action cannot be undone.",
                "Continue",
                "Cancel"))
        {
            return;
        }

        if (!EditorUtility.DisplayDialog(
                "Final Confirmation",
                "Are you absolutely sure you want to delete ALL save data?",
                "Yes, delete all",
                "Cancel"))
        {
            return;
        }

        var result = ClearAllSaveDataInternal();
        var message = result.success
            ? $"All save data cleared.\nPlayerPrefs cleared: {result.playerPrefsCleared}\nMirraSDK cleared: {result.mirraCleared}"
            : result.error;

        if (result.success)
            Debug.Log("[SaveTools] " + message);
        else
            Debug.LogWarning("[SaveTools] " + message);

        EditorUtility.DisplayDialog("All Save Data", message, "OK");
    }

    private static ClearResult ClearAlbumSavesInternal()
    {
        var result = new ClearResult();

        var storage = ResolveStorage();
        if (storage == null)
        {
            result.success = false;
            result.error = "ItemPrefabStorage not found. Open a scene with ItemPrefabStorage (or run in Play Mode) and retry.";
            return result;
        }

        var savePrefix = DefaultSavePrefix;
        var datePrefix = DefaultDateSavePrefix;
        TryResolvePrefixes(ref savePrefix, ref datePrefix);

        var eggIds = CollectEggIds(storage);
        var animalIds = CollectAnimalIds(storage);
        var allElements = AlbumProgressService.GetSupportedElementTypes();

        var albumKeys = new HashSet<string>(StringComparer.Ordinal);
        var dateKeys = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < eggIds.Count; i++)
            AddEntityKeys(albumKeys, dateKeys, savePrefix, AlbumEntityType.Egg, eggIds[i], allElements);

        for (var i = 0; i < animalIds.Count; i++)
            AddEntityKeys(albumKeys, dateKeys, savePrefix, AlbumEntityType.Animal, animalIds[i], allElements);

        for (var i = 0; i < allElements.Count; i++)
        {
            var elementType = allElements[i];
            albumKeys.Add(BuildElementGlobalKey(savePrefix, "ElementUnlocked", elementType));
            albumKeys.Add(BuildElementTabKey(savePrefix, "MentionElement", AlbumEntityType.Egg, elementType));
            albumKeys.Add(BuildElementTabKey(savePrefix, "MentionElement", AlbumEntityType.Animal, elementType));
        }

        var prefDeleted = 0;
        var dateDeleted = 0;
        var keysProcessed = 0;
        var inPlayWithSave = Application.isPlaying && G.Save != null;

        foreach (var key in albumKeys)
        {
            keysProcessed++;

            if (inPlayWithSave)
            {
                // Through SaveManager for current active provider.
                G.Save.SaveLevelStatus(key, false);
            }

            if (DeletePrefKey("LevelStatus_" + key))
                prefDeleted++;
            if (DeletePrefKey("AlbumFallback." + key))
                prefDeleted++;
        }

        foreach (var dateKey in dateKeys)
        {
            if (DeletePrefKey(datePrefix + "." + dateKey))
                dateDeleted++;
        }

        if (prefDeleted > 0 || dateDeleted > 0)
            PlayerPrefs.Save();

        if (Application.isPlaying && G.Album != null)
            G.Album.Changed?.Invoke();

        result.success = true;
        result.entityCount = eggIds.Count + animalIds.Count;
        result.albumKeysProcessed = keysProcessed;
        result.dateKeysDeleted = dateDeleted;
        result.playerPrefsKeysDeleted = prefDeleted;
        return result;
    }

    private static ClearAllResult ClearAllSaveDataInternal()
    {
        var result = new ClearAllResult
        {
            success = true
        };

        try
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            result.playerPrefsCleared = true;
        }
        catch (Exception ex)
        {
            result.success = false;
            result.error = "Failed to clear PlayerPrefs: " + ex.Message;
            return result;
        }

        try
        {
            MirraSDK.Data.DeleteAll();
            result.mirraCleared = true;
        }
        catch (Exception ex)
        {
            // PlayerPrefs are already cleared, but Mirra data could not be wiped.
            result.mirraCleared = false;
            result.error = "PlayerPrefs cleared, but MirraSDK delete failed: " + ex.Message;
        }

        if (Application.isPlaying && G.Save != null)
        {
            G.Save.SetSave(false);
            G.Save.SaveBackendProfile(string.Empty, string.Empty, string.Empty);
        }

        if (Application.isPlaying)
        {
            var lobby = LobbyClient.Instance;
            if (lobby == null)
                lobby = UnityEngine.Object.FindAnyObjectByType<LobbyClient>();

            if (lobby != null)
            {
                lobby.HandleLocalSaveReset();
            }
            else
            {
                var remoteBases = UnityEngine.Object.FindAnyObjectByType<RemoteBasesApplier>();
                if (remoteBases != null)
                    remoteBases.HandleLocalSaveReset();
            }
        }

        if (Application.isPlaying && G.Album != null)
            G.Album.Changed?.Invoke();

        if (!string.IsNullOrEmpty(result.error))
            result.success = false;

        return result;
    }

    private static void TryResolvePrefixes(ref string savePrefix, ref string datePrefix)
    {
        var services = Resources.FindObjectsOfTypeAll<AlbumProgressService>();
        for (var i = 0; i < services.Length; i++)
        {
            var service = services[i];
            if (service == null || EditorUtility.IsPersistent(service))
                continue;

            var so = new SerializedObject(service);
            var saveProp = so.FindProperty("savePrefix");
            var dateProp = so.FindProperty("dateSavePrefix");
            if (saveProp != null && !string.IsNullOrWhiteSpace(saveProp.stringValue))
                savePrefix = saveProp.stringValue.Trim();
            if (dateProp != null && !string.IsNullOrWhiteSpace(dateProp.stringValue))
                datePrefix = dateProp.stringValue.Trim();
            return;
        }
    }

    private static ItemPrefabStorage ResolveStorage()
    {
        if (Application.isPlaying && G.Storage != null)
            return G.Storage;

        var sceneStorages = Resources.FindObjectsOfTypeAll<ItemPrefabStorage>();
        for (var i = 0; i < sceneStorages.Length; i++)
        {
            var storage = sceneStorages[i];
            if (storage == null || EditorUtility.IsPersistent(storage))
                continue;
            return storage;
        }

        var prefabGuids = AssetDatabase.FindAssets("ItemPrefabStorage t:Prefab");
        for (var i = 0; i < prefabGuids.Length; i++)
        {
            var path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null)
                continue;

            var storage = go.GetComponentInChildren<ItemPrefabStorage>(true);
            if (storage != null)
                return storage;
        }

        return null;
    }

    private static List<string> CollectEggIds(ItemPrefabStorage storage)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var eggs = storage.GetAllEggPrefabs();
        if (eggs == null)
            return result;

        for (var i = 0; i < eggs.Count; i++)
        {
            var egg = eggs[i];
            if (egg == null)
                continue;

            var id = AlbumProgressService.NormalizeId(egg.Name);
            if (string.IsNullOrEmpty(id))
                id = AlbumProgressService.NormalizeId(egg.name);
            if (string.IsNullOrEmpty(id) || !seen.Add(id))
                continue;

            result.Add(id);
        }

        return result;
    }

    private static List<string> CollectAnimalIds(ItemPrefabStorage storage)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var animals = storage.GetAllPetPrefabs();
        if (animals == null)
            return result;

        for (var i = 0; i < animals.Count; i++)
        {
            var animal = animals[i];
            if (animal == null)
                continue;

            var id = AlbumProgressService.NormalizeId(animal.Name);
            if (string.IsNullOrEmpty(id))
                id = AlbumProgressService.NormalizeId(animal.name);
            if (string.IsNullOrEmpty(id) || !seen.Add(id))
                continue;

            result.Add(id);
        }

        return result;
    }

    private static void AddEntityKeys(
        ISet<string> albumKeys,
        ISet<string> dateKeys,
        string savePrefix,
        AlbumEntityType type,
        string id,
        IReadOnlyList<ElementType> elements)
    {
        if (string.IsNullOrEmpty(id))
            return;

        albumKeys.Add(BuildEntityKey(savePrefix, "Discovered", type, id));
        albumKeys.Add(BuildEntityKey(savePrefix, "Viewed", type, id));
        albumKeys.Add(BuildEntityKey(savePrefix, "MentionCard", type, id));
        albumKeys.Add(BuildEntityKey(savePrefix, "RewardClaimed", type, id));
        albumKeys.Add(BuildEntityKey(savePrefix, "MentionReward", type, id));

        dateKeys.Add(BuildEntityKey(savePrefix, "FirstDiscoveredAt", type, id));

        if (elements == null)
            return;

        for (var i = 0; i < elements.Count; i++)
        {
            var elementType = elements[i];
            albumKeys.Add(BuildEntityElementKey(savePrefix, "ElementSeen", type, id, elementType));
            albumKeys.Add(BuildEntityElementKey(savePrefix, "ElementViewed", type, id, elementType));
            albumKeys.Add(BuildEntityElementKey(savePrefix, "MentionElement", type, id, elementType));
            albumKeys.Add(BuildEntityElementKey(savePrefix, "RewardClaimed", type, id, elementType));
            albumKeys.Add(BuildEntityElementKey(savePrefix, "MentionReward", type, id, elementType));
            dateKeys.Add(BuildEntityElementKey(savePrefix, "FirstElementSeenAt", type, id, elementType));
        }
    }

    private static bool DeletePrefKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || !PlayerPrefs.HasKey(key))
            return false;

        PlayerPrefs.DeleteKey(key);
        return true;
    }

    private static string BuildEntityKey(string prefix, string tag, AlbumEntityType type, string id)
    {
        return $"{prefix}.{tag}.{type}.{AlbumProgressService.NormalizeId(id)}";
    }

    private static string BuildEntityElementKey(string prefix, string tag, AlbumEntityType type, string id, ElementType elementType)
    {
        return $"{prefix}.{tag}.{type}.{AlbumProgressService.NormalizeId(id)}.{AlbumProgressService.NormalizeId(elementType.ToString())}";
    }

    private static string BuildElementGlobalKey(string prefix, string tag, ElementType elementType)
    {
        return $"{prefix}.{tag}.{AlbumProgressService.NormalizeId(elementType.ToString())}";
    }

    private static string BuildElementTabKey(string prefix, string tag, AlbumEntityType type, ElementType elementType)
    {
        return $"{prefix}.{tag}.{type}.{AlbumProgressService.NormalizeId(elementType.ToString())}";
    }

    private struct ClearResult
    {
        public bool success;
        public string error;
        public int entityCount;
        public int albumKeysProcessed;
        public int dateKeysDeleted;
        public int playerPrefsKeysDeleted;
    }

    private struct ClearAllResult
    {
        public bool success;
        public string error;
        public bool playerPrefsCleared;
        public bool mirraCleared;
    }
}
#endif
