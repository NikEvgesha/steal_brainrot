using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using MirraGames.SDK;
using System;
using Newtonsoft.Json;

public class SaveManager : MonoBehaviour
{
    [SerializeField] private SaveProvider saveProvider; // Назначаем в инспекторе нужный провайдер (YG2SaveProvider, DebugSaveProvider и т.д.)
    [SerializeField] private bool _newPlayer;
    public bool IsNewPlayer => saveProvider == null || !saveProvider.IsInitialized || saveProvider.CheckProgress() == false;
    public bool IsReady => saveProvider != null && saveProvider.IsInitialized;

    private bool _pendingSaveFlagSet;
    private bool _pendingSaveFlagValue;
    private bool _hasCachedBackendProfile;
    private bool _pendingBackendProfilePersist;
    private string _cachedBackendPlayerId;
    private string _cachedBackendFriendCode;
    private string _cachedBackendDisplayName;

    private void Awake()
    {

        if (_newPlayer)
        {
            MirraSDK.Data.DeleteAll();
        }

        if (G.Save == null)
        {
            G.Save = this;
            DontDestroyOnLoad(gameObject);
            if (saveProvider == null)
            {
                Debug.LogError("[SaveManager] Save provider is not assigned.");
                return;
            }

            saveProvider.Initialize();
            StartCoroutine(ProgressSavingRoutine());
        }
        else
        {
            Destroy(gameObject);
        }

    }

    private IEnumerator ProgressSavingRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(1);

            if (saveProvider == null || !saveProvider.IsInitialized)
                continue;

            if (_pendingSaveFlagSet)
            {
                saveProvider.SetSave(_pendingSaveFlagValue);
                _pendingSaveFlagSet = false;
            }

            if (_pendingBackendProfilePersist && _hasCachedBackendProfile)
            {
                saveProvider.SaveBackendProfile(_cachedBackendPlayerId, _cachedBackendFriendCode, _cachedBackendDisplayName);
                _pendingBackendProfilePersist = false;
            }

            saveProvider.SaveProgress();
        }
    }

    public void SetSave(bool haveSave)
    {
        if (saveProvider != null && saveProvider.IsInitialized)
        {
            saveProvider.SetSave(haveSave);
            _pendingSaveFlagSet = false;
            return;
        }

        _pendingSaveFlagSet = true;
        _pendingSaveFlagValue = haveSave;
    }

    private void MarkProgressExists()
    {
        SetSave(true);
    }

    // Пример методов, которые делегируют работу провайдеру:
    public float[] GetVolume()
    {
        return saveProvider.LoadVolume();
    }
    public void SaveQuestProgress(int step = 0)
    {
        MarkProgressExists();
        saveProvider.SaveQuestProgress(step);
    }
    public int LoadQuestProgress() 
    {
        return saveProvider.LoadQuestProgress();
    }
    public void SaveMusicVolume(float volume)
    {
        var volumes = saveProvider.LoadVolume();
        saveProvider.SaveVolume(volume, volumes[1]);
    }

    public void SaveSoundVolume(float volume)
    {
        var volumes = saveProvider.LoadVolume();
        saveProvider.SaveVolume(volumes[0], volume);
    }


    public float[] LoadVolume()
    {
        return saveProvider.LoadVolume();
    }


    public void SaveSensivity(float sens)
    {
        saveProvider.SaveSensivity(sens);
    }

    public float LoadSensivity()
    {
        return saveProvider.LoadSensivity();
    }

    public void SaveScore(float score, int levelId)
    {
        saveProvider.SaveScore(score, levelId);
    }

    public float GetLevelScore(int levelId)
    {
        return saveProvider.LoadScore(levelId);
    }

    public bool GetTutorialProgress()
    {
        return saveProvider.GetTutorialProgress();
    }
    public void SaveTutorialProgress(bool endTutorial)
    {
        MarkProgressExists();
        saveProvider.SaveTutorialProgress(endTutorial);
    }
    public TutorialSaveData LoadTutorialState()
    {
        if (saveProvider == null || !saveProvider.IsInitialized)
            return TutorialSaveData.CreateNew();

        string json = saveProvider.LoadTutorialState();
        if (string.IsNullOrWhiteSpace(json))
        {
            TutorialSaveData newState = TutorialSaveData.CreateNew();
            newState.Normalize(saveProvider.GetTutorialProgress());
            return newState;
        }

        try
        {
            TutorialSaveData state = JsonConvert.DeserializeObject<TutorialSaveData>(json);
            if (state == null)
            {
                TutorialSaveData newState = TutorialSaveData.CreateNew();
                newState.Normalize(saveProvider.GetTutorialProgress());
                return newState;
            }
            state.Normalize(saveProvider.GetTutorialProgress());
            return state;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[SaveManager] Invalid tutorial state, starting from a safe default: {ex.Message}");
            GameAnalytics.Track(AnalyticsEventNames.SaveError, GameAnalytics.Params(
                "operation", "load_tutorial_state",
                "save_provider", saveProvider != null ? saveProvider.GetType().Name : string.Empty,
                "exception_type", ex.GetType().Name,
                "source", "save_manager",
                "result", "recovered",
                "failure_reason", "invalid_serialized_state"),
                AnalyticsPriority.Diagnostic,
                "load_tutorial_state");
            TutorialSaveData fallback = TutorialSaveData.CreateNew();
            fallback.Normalize(saveProvider.GetTutorialProgress());
            return fallback;
        }
    }
    public void SaveTutorialState(TutorialSaveData state)
    {
        if (state == null || saveProvider == null || !saveProvider.IsInitialized)
            return;

        state.Normalize();
        MarkProgressExists();
        saveProvider.SaveTutorialState(JsonConvert.SerializeObject(state));
    }
    public void SaveGems(double amount)
    {
        MarkProgressExists();
        saveProvider.SaveGems(amount);
        //LeaderboardManager.Instance.SaveScore(LBName.gems.ToString(), amount);
    }

    public double GetGems()
    {
        return saveProvider.LoadGems();
    }


    public void SaveAchiementTypeProgress(AchievementType achievementType, int progress)
    {
        saveProvider.SaveAchievementProgress(achievementType, progress);
    }

    public int GetAchievementTypeProgress(AchievementType achievementType)
    {
        return saveProvider.LoadAchievementProgress(achievementType);
    }

    public void SaveAchiementStatus(string achievementID, bool progress)
    {
        saveProvider.SaveAchievementStatus(achievementID, progress);
    }

    public bool GetAchievementStatus(string achievementID)
    {
        return saveProvider.LoadAchievementStatus(achievementID);
    }

    public void SaveLevelStatus(string lvlName, bool unlocked)
    {
        saveProvider.SaveLevelStatus("LevelStatus_" + lvlName, unlocked);
    }

    public bool GetLevelStatus(string lvlName)
    {
        return saveProvider.LoadLevelStatus("LevelStatus_" + lvlName);
    }




    public void SaveGameProgress(int distance = -1, List<ItemData> items = null, int lvlId = -1)
    { 
        saveProvider.SaveLevelId(lvlId);
        if (items != null)
            saveProvider.SaveInventory(items);

        //Debug.Log("Progress Saved");
       
    }

    public void SavePlayerStats(int coin, float hp)
    {
        saveProvider.SavePlayerStats(coin, hp);
    }
    public void SaveGameCoin(double coin)
    {
        MarkProgressExists();
        saveProvider.SaveGameCoin(coin);
    }
    public double LoadGameCoin()
    {
        return saveProvider.LoadGameCoin();
    }
    public void SavePlayerHealth(float health)
    {
        saveProvider.SavePlayerHealth(health);
    }
    public float LoadPlayerHealth()
    {
        return saveProvider.LoadPlayerHealth();
    }
    public void SavePlayerExperience(int exp , int level)
    {
        saveProvider.SavePlayerExperience(exp, level);
    }
    public (int, int) LoadPlayerExperience()
    {
        return saveProvider.LoadPlayerExperience();
    }
    public (int, float) LoadPlayerStats()
    {
        return saveProvider.LoadPlayerStats();
    }
    public void ResetGameProgress() => SaveGameProgress();


    public int LoadLevelId() {
        return saveProvider.LoadLevelId();
    }

    public void SaveAmmo(WeaponType type, int amount) {
        saveProvider.SaveAmmo(type, amount);
    }

    public int LoadAmmo(WeaponType type) {
        return saveProvider.LoadAmmo(type);
    }

    public void SaveWin()
    {
        int wins = saveProvider.LoadWins();
        saveProvider.SaveWins(wins+1);
        LeaderboardManager.Instance.SaveScore(LBName.wins.ToString(), wins+1);
    }

    public void SaveRouletteDate(DateTime date)
    {
        if (saveProvider == null || !saveProvider.IsInitialized)
        {
            Debug.LogWarning("[SaveManager] Roulette date was not saved because the provider is not ready.");
            return;
        }

        MarkProgressExists();
        saveProvider.SaveRouletteDate(date.ToUniversalTime());

        // A daily reward must survive an immediate page close/reload. Do not
        // leave this timestamp waiting for the one-second background save loop.
        saveProvider.SaveProgress();
    }

    public DateTime LoadRouletteDate()
    {
        return saveProvider.LoadRouletteDate();
    }

    public void SaveOfflineRewardLastSeenUnix(long unix)
    {
        if (saveProvider == null || !saveProvider.IsInitialized)
            return;

        saveProvider.SaveOfflineRewardLastSeenUnix(unix);
        saveProvider.SaveProgress();
    }

    public long LoadOfflineRewardLastSeenUnix()
    {
        return saveProvider != null && saveProvider.IsInitialized
            ? saveProvider.LoadOfflineRewardLastSeenUnix()
            : 0L;
    }

    public void SaveBigPetStatus(bool purchased)
    {
        MarkProgressExists();
        saveProvider.SaveBigPetStatus(purchased);
    }

    public bool LoadBigPetStatus()
    {
        return saveProvider.LoadBigPetStatus();
    }

    public void SaveBigPetXP(int xp)
    {
        MarkProgressExists();
        saveProvider.SaveBigPetXP(xp);
    }
    public void SaveBigPetLvl(int lvl)
    {
        MarkProgressExists();
        saveProvider.SaveBigPetLvl(lvl);
    }
    public void SaveBigPetId(int id)
    {
        MarkProgressExists();
        saveProvider.SaveBigPetId(id);
    }
    public void SaveBigPetIncomeTime(string incomeTime)
    {
        MarkProgressExists();
        saveProvider.SaveBigPetIncomeTime(incomeTime);
    }
    public int LoadBigPetXP()
    {
        return saveProvider.LoadBigPetXP();
    }
    public int LoadBigPetLvl()
    {
        return saveProvider.LoadBigPetLvl();
    }
    public int LoadBigPetId()
    {
        return saveProvider.LoadBigPetId();
    }
    public string LoadBigPetIncomeTime()
    {
        return saveProvider.LoadBigPetIncomeTime();
    }


    public void SaveConveyorCurrentLevel(int id)
    {
        MarkProgressExists();
        saveProvider.SaveConveyorCurrentLevel(id);
    }
    public void SaveConveyorUnlockedLevel(int id)
    {
        MarkProgressExists();
        saveProvider.SaveConveyorUnlockedLevel(id);
    }

    public int LoadConveyorCurrentLevel()
    {
        return saveProvider.LoadConveyorCurrentLevel();
    }
    public int LoadConveyorUnlockedLevel()
    {
        return saveProvider.LoadConveyorUnlockedLevel();
    }


    public void SaveFieldUnblockStatus(int fieldId, bool unblocked)
    {
        MarkProgressExists();
        saveProvider.SaveFieldUnblockStatus(fieldId, unblocked);
    }

    public bool LoadFieldUnblockStatus(int fieldId)
    {
        return saveProvider.LoadFieldUnblockStatus(fieldId);
    }


    public void SaveCellData(string id, CellSaveData data)
    {
        MarkProgressExists();
        saveProvider.SaveCellData(id, data);
    }

    public CellSaveData LoadCellData(string id)
    {
        return saveProvider.LoadCellData(id);
    }

    public void SaveInventory(Item type, List<ItemSaveData> items)
    {
        MarkProgressExists();
        saveProvider.SaveItemsList(type, JsonConvert.SerializeObject(items));
    }

    public List<ItemSaveData> LoadInventory(Item type)
    {
        return saveProvider.LoadItemsList(type);
    }
    public void SaveBackendProfile(string playerId, string friendCode, string displayName)
    {
        _cachedBackendPlayerId = playerId ?? "";
        _cachedBackendFriendCode = friendCode ?? "";
        _cachedBackendDisplayName = displayName ?? "";
        _hasCachedBackendProfile = !string.IsNullOrEmpty(_cachedBackendPlayerId) ||
                                  !string.IsNullOrEmpty(_cachedBackendFriendCode) ||
                                  !string.IsNullOrEmpty(_cachedBackendDisplayName);

        if (saveProvider != null && saveProvider.IsInitialized)
        {
            saveProvider.SaveBackendProfile(_cachedBackendPlayerId, _cachedBackendFriendCode, _cachedBackendDisplayName);
            _pendingBackendProfilePersist = false;
            return;
        }

        _pendingBackendProfilePersist = _hasCachedBackendProfile;
    }

    public (string playerId, string friendCode, string displayName) LoadBackendProfile()
    {
        if (saveProvider != null && saveProvider.IsInitialized)
        {
            var profile = saveProvider.LoadBackendProfile();
            var hasProviderData = !string.IsNullOrEmpty(profile.playerId) ||
                                  !string.IsNullOrEmpty(profile.friendCode) ||
                                  !string.IsNullOrEmpty(profile.displayName);

            if (hasProviderData)
            {
                _cachedBackendPlayerId = profile.playerId ?? "";
                _cachedBackendFriendCode = profile.friendCode ?? "";
                _cachedBackendDisplayName = profile.displayName ?? "";
                _hasCachedBackendProfile = true;
                _pendingBackendProfilePersist = false;
                return profile;
            }

            if (_hasCachedBackendProfile)
                return (_cachedBackendPlayerId, _cachedBackendFriendCode, _cachedBackendDisplayName);

            return profile;
        }

        if (_hasCachedBackendProfile)
            return (_cachedBackendPlayerId, _cachedBackendFriendCode, _cachedBackendDisplayName);

        return ("", "", "");
    }


}
