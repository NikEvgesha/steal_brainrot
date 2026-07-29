using System;
using System.Collections.Generic;
using UnityEngine;

public abstract class SaveProvider : MonoBehaviour
{
    private const string OfflineRewardLastSeenKey = "OfflineReward.LastSeenUnix";

    public bool Changed;
    public abstract bool IsInitialized { get; }
    public abstract void Initialize();

    // Audio settings
    public abstract float[] LoadVolume();
    public abstract void SaveVolume(float musicVolume, float soundVolume);
    public abstract void SaveSensivity(float sens);
    public abstract float LoadSensivity();

    // Score
    public abstract void SaveScore(float score, int levelId);
    public abstract float LoadScore(int levelId);

    // Tutorial and quest progress
    public abstract bool GetTutorialProgress();
    public abstract void SaveTutorialProgress(bool endTutorial);
    public virtual string LoadTutorialState() => string.Empty;
    public virtual void SaveTutorialState(string json) { }
    public abstract void SaveQuestProgress(int step);
    public abstract int LoadQuestProgress();

    // Level state
    public abstract void SaveLevelUnlock(int id, bool unlocked);
    public abstract void SaveLevelWin(int id, bool win);

    // Provider-level save lifecycle
    public abstract void SaveProgress();
    public abstract bool CheckProgress();

    // Currency
    public abstract void SaveGems(double amount);
    public abstract double LoadGems();
    public abstract void SaveGameCoin(double coin);
    public abstract double LoadGameCoin();

    // Achievements
    public abstract void SaveAchievementProgress(AchievementType id, int progress);
    public abstract int LoadAchievementProgress(AchievementType id);
    public abstract void SaveAchievementStatus(string id, bool progress);
    public abstract bool LoadAchievementStatus(string id);

    // Named level state
    public abstract void SaveLevelStatus(string lvlName, bool unlocked);
    public abstract bool LoadLevelStatus(string lvlName);

    // Player stats
    public abstract void SavePlayerStats(int coin, float hp);
    public abstract void SavePlayerHealth(float health);
    public abstract void SavePlayerExperience(int exp, int level);
    public abstract (int, int) LoadPlayerExperience();
    public abstract float LoadPlayerHealth();
    public abstract (int, float) LoadPlayerStats();

    // Legacy inventory
    public abstract void SaveInventory(List<ItemData> items);
    public abstract List<string> LoadInventory();

    // Ammo
    public abstract void SaveAmmo(WeaponType type, int amount);
    public abstract int LoadAmmo(WeaponType type);

    // Other legacy values
    public abstract void SetSave(bool save);
    public abstract void SaveWins(int wins);
    public abstract int LoadWins();
    public abstract void SaveLevelId(int id);
    public abstract int LoadLevelId();
    public abstract void SaveRouletteDate(DateTime date);
    public abstract DateTime LoadRouletteDate();

    public virtual void SaveOfflineRewardLastSeenUnix(long unix)
    {
        PlayerPrefs.SetString(OfflineRewardLastSeenKey, Math.Max(0L, unix).ToString());
        PlayerPrefs.Save();
    }

    public virtual long LoadOfflineRewardLastSeenUnix()
    {
        return long.TryParse(PlayerPrefs.GetString(OfflineRewardLastSeenKey, "0"), out long unix)
            ? Math.Max(0L, unix)
            : 0L;
    }

    public abstract void SaveBigPetXP(int xp);
    public abstract void SaveBigPetLvl(int lvl);
    public abstract void SaveBigPetId(int id);
    public abstract void SaveBigPetIncomeTime(string time);
    public abstract int LoadBigPetXP();
    public abstract int LoadBigPetLvl();
    public abstract int LoadBigPetId();
    public abstract string LoadBigPetIncomeTime();
    public abstract void SaveBigPetStatus(bool purchased);
    public abstract bool LoadBigPetStatus();

    // Conveyor data
    public abstract void SaveConveyorCurrentLevel(int id);
    public abstract void SaveConveyorUnlockedLevel(int id);
    public abstract int LoadConveyorCurrentLevel();
    public abstract int LoadConveyorUnlockedLevel();

    // Field data
    public abstract void SaveFieldUnblockStatus(int fieldId, bool unblocked);
    public abstract bool LoadFieldUnblockStatus(int fieldId);
    public abstract void SaveCellData(string key, CellSaveData data);
    public abstract CellSaveData LoadCellData(string key);

    // Inventory
    public abstract void SaveItemsList(Item type, string items);
    public abstract List<ItemSaveData> LoadItemsList(Item type);

    // Backend auth
    public abstract void SaveBackendProfile(string playerId, string friendCode, string displayName);
    public abstract (string playerId, string friendCode, string displayName) LoadBackendProfile();
}
