using MirraGames.SDK;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

public class DummySaveProvider : SaveProvider
{
    private const string BigPetXPKey = "BigPetXP";
    private const string BigPetLvlKey = "BigPetLvl";
    private const string BigPetIdKey = "BigPetId";
    private const string BigPetIncomeTimeKey = "BigPetIncomeTime";
    private const string BigPetStatusKey = "BigPetStatus";
    private const string ConveyorCurrentLevelKey = "ConveyorCurrentLvl";
    private const string ConveyorUnlockedLevelKey = "ConveyorUnlockedLvl";
    private const string BackendPlayerIdKey = "BackendPlayerId";
    private const string BackendFriendCodeKey = "BackendFriendCode";
    private const string BackendDisplayNameKey = "BackendDisplayName";
    private const string TutorialStateKey = "Tutorial.State.V1";

    public override bool IsInitialized => true;

    public override void Initialize()
    {
        Debug.Log("DummySaveProvider initialized");
    }

    public override float[] LoadVolume()
    {
        return new[]
        {
            PlayerPrefs.GetFloat(SaveKey.MusicVolume.ToString(), 0.5f),
            PlayerPrefs.GetFloat(SaveKey.SoundVolume.ToString(), 0.5f)
        };
    }

    public override void SaveVolume(float musicVolume, float soundVolume)
    {
        PlayerPrefs.SetFloat(SaveKey.MusicVolume.ToString(), musicVolume);
        PlayerPrefs.SetFloat(SaveKey.SoundVolume.ToString(), soundVolume);
    }

    public override void SaveSensivity(float sens)
    {
        PlayerPrefs.SetFloat(SaveKey.Sensivity.ToString(), sens);
    }

    public override float LoadSensivity()
    {
        return PlayerPrefs.GetFloat(SaveKey.Sensivity.ToString(), 0.5f);
    }

    public override void SaveScore(float score, int levelId)
    {
        PlayerPrefs.SetFloat($"{SaveKey.Score_}{levelId}", score);
    }

    public override float LoadScore(int levelId)
    {
        return PlayerPrefs.GetFloat($"{SaveKey.Score_}{levelId}", 0f);
    }

    public override bool GetTutorialProgress()
    {
        return PlayerPrefs.GetInt(SaveKey.EndTutorial.ToString(), 0) == 1;
    }

    public override void SaveTutorialProgress(bool endTutorial)
    {
        PlayerPrefs.SetInt(SaveKey.EndTutorial.ToString(), endTutorial ? 1 : 0);
    }

    public override string LoadTutorialState()
    {
        return PlayerPrefs.GetString(TutorialStateKey, string.Empty);
    }

    public override void SaveTutorialState(string json)
    {
        PlayerPrefs.SetString(TutorialStateKey, json ?? string.Empty);
    }

    public override void SaveQuestProgress(int step)
    {
        PlayerPrefs.SetInt(SaveKey.QuestProgress.ToString(), step);
    }

    public override int LoadQuestProgress()
    {
        return PlayerPrefs.GetInt(SaveKey.QuestProgress.ToString(), 0);
    }

    public override void SaveLevelUnlock(int id, bool unlocked)
    {
        PlayerPrefs.SetInt($"{SaveKey.LevelUnlock_}{id}", unlocked ? 1 : 0);
    }

    public override void SaveLevelWin(int id, bool win)
    {
        PlayerPrefs.SetInt($"{SaveKey.LevelWin_}{id}", win ? 1 : 0);
    }

    public override void SaveProgress()
    {
        PlayerPrefs.Save();
    }

    public override bool CheckProgress()
    {
        return PlayerPrefs.GetInt(SaveKey.Save.ToString(), 0) == 1;
    }

    public override void SaveGems(double amount)
    {
        PlayerPrefs.SetString(SaveKey.Gems.ToString(), amount.ToString(CultureInfo.InvariantCulture));
    }

    public override double LoadGems()
    {
        return ReadDouble(SaveKey.Gems.ToString(), 0d);
    }

    public override void SaveGameCoin(double coin)
    {
        PlayerPrefs.SetString(SaveKey.Coins.ToString(), coin.ToString(CultureInfo.InvariantCulture));
    }

    public override double LoadGameCoin()
    {
        return ReadDouble(SaveKey.Coins.ToString(), -1d);
    }

    public override void SaveAchievementProgress(AchievementType id, int progress)
    {
        PlayerPrefs.SetInt(id.ToString(), progress);
    }

    public override int LoadAchievementProgress(AchievementType id)
    {
        return PlayerPrefs.GetInt(id.ToString(), 0);
    }

    public override void SaveAchievementStatus(string id, bool progress)
    {
        PlayerPrefs.SetInt(id, progress ? 1 : 0);
    }

    public override bool LoadAchievementStatus(string id)
    {
        return PlayerPrefs.GetInt(id, 0) == 1;
    }

    public override void SaveLevelStatus(string key, bool unlocked)
    {
        PlayerPrefs.SetInt(key, unlocked ? 1 : 0);
    }

    public override bool LoadLevelStatus(string key)
    {
        return PlayerPrefs.GetInt(key, 0) == 1;
    }

    public override void SavePlayerStats(int coin, float hp)
    {
        SaveGameCoin(coin);
        SavePlayerHealth(hp);
    }

    public override void SavePlayerHealth(float health)
    {
        PlayerPrefs.SetFloat(SaveKey.Health.ToString(), health);
    }

    public override void SavePlayerExperience(int exp, int level)
    {
        PlayerPrefs.SetInt(SaveKey.Exp.ToString(), exp);
        PlayerPrefs.SetInt(SaveKey.Level.ToString(), level);
    }

    public override (int, int) LoadPlayerExperience()
    {
        return (PlayerPrefs.GetInt(SaveKey.Exp.ToString(), 0), PlayerPrefs.GetInt(SaveKey.Level.ToString(), 0));
    }

    public override float LoadPlayerHealth()
    {
        return PlayerPrefs.GetFloat(SaveKey.Health.ToString(), 100f);
    }

    public override (int, float) LoadPlayerStats()
    {
        return ((int)Math.Round(LoadGameCoin()), LoadPlayerHealth());
    }

    public override void SaveInventory(List<ItemData> items)
    {
        var listSaver = new ListSaver();
        if (items != null)
        {
            foreach (var item in items)
            {
                if (item != null)
                    listSaver.list.Add(item.Name);
            }
        }

        PlayerPrefs.SetString(SaveKey.InventoryList.ToString(), JsonConvert.SerializeObject(listSaver));
    }

    public override List<string> LoadInventory()
    {
        var json = PlayerPrefs.GetString(SaveKey.InventoryList.ToString(), "");
        if (string.IsNullOrWhiteSpace(json))
            return new List<string>();

        try
        {
            return JsonConvert.DeserializeObject<ListSaver>(json)?.list ?? new List<string>();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[DummySaveProvider] Failed to load inventory: {ex.Message}");
            return new List<string>();
        }
    }

    public override void SaveAmmo(WeaponType type, int amount)
    {
        PlayerPrefs.SetInt(SaveKey.Ammo_.ToString() + type, amount);
    }

    public override int LoadAmmo(WeaponType type)
    {
        return PlayerPrefs.GetInt(SaveKey.Ammo_.ToString() + type, 0);
    }

    public override void SetSave(bool save)
    {
        PlayerPrefs.SetInt(SaveKey.Save.ToString(), save ? 1 : 0);
    }

    public override void SaveWins(int wins)
    {
        PlayerPrefs.SetInt(SaveKey.Wins.ToString(), wins);
    }

    public override int LoadWins()
    {
        return PlayerPrefs.GetInt(SaveKey.Wins.ToString(), 0);
    }

    public override void SaveLevelId(int id)
    {
        PlayerPrefs.SetInt(SaveKey.LevelId.ToString(), id);
    }

    public override int LoadLevelId()
    {
        return PlayerPrefs.GetInt(SaveKey.LevelId.ToString(), -1);
    }

    public override void SaveRouletteDate(DateTime date)
    {
        PlayerPrefs.SetString(SaveKey.RouletteLastDate.ToString(), date.ToString("O", CultureInfo.InvariantCulture));
    }

    public override DateTime LoadRouletteDate()
    {
        var value = PlayerPrefs.GetString(SaveKey.RouletteLastDate.ToString(), "");
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var date))
            return date;
        return DateTime.Today.AddDays(-1);
    }

    public override void SaveBigPetXP(int xp)
    {
        PlayerPrefs.SetInt(BigPetXPKey, xp);
    }

    public override void SaveBigPetLvl(int lvl)
    {
        PlayerPrefs.SetInt(BigPetLvlKey, lvl);
    }

    public override void SaveBigPetId(int id)
    {
        PlayerPrefs.SetInt(BigPetIdKey, id);
    }

    public override void SaveBigPetIncomeTime(string time)
    {
        PlayerPrefs.SetString(BigPetIncomeTimeKey, time ?? "");
    }

    public override int LoadBigPetXP()
    {
        return PlayerPrefs.GetInt(BigPetXPKey, 0);
    }

    public override int LoadBigPetLvl()
    {
        return PlayerPrefs.GetInt(BigPetLvlKey, 1);
    }

    public override int LoadBigPetId()
    {
        return PlayerPrefs.GetInt(BigPetIdKey, 0);
    }

    public override string LoadBigPetIncomeTime()
    {
        return PlayerPrefs.GetString(BigPetIncomeTimeKey, "");
    }

    public override void SaveBigPetStatus(bool purchased)
    {
        PlayerPrefs.SetInt(BigPetStatusKey, purchased ? 1 : 0);
    }

    public override bool LoadBigPetStatus()
    {
        return PlayerPrefs.GetInt(BigPetStatusKey, 0) == 1;
    }

    public override void SaveConveyorCurrentLevel(int id)
    {
        PlayerPrefs.SetInt(ConveyorCurrentLevelKey, id);
    }

    public override void SaveConveyorUnlockedLevel(int id)
    {
        PlayerPrefs.SetInt(ConveyorUnlockedLevelKey, id);
    }

    public override int LoadConveyorCurrentLevel()
    {
        return PlayerPrefs.GetInt(ConveyorCurrentLevelKey, 0);
    }

    public override int LoadConveyorUnlockedLevel()
    {
        return PlayerPrefs.GetInt(ConveyorUnlockedLevelKey, 0);
    }

    public override void SaveFieldUnblockStatus(int fieldId, bool unblocked)
    {
        PlayerPrefs.SetInt(SaveKey.Field.ToString() + fieldId, unblocked ? 1 : 0);
    }

    public override bool LoadFieldUnblockStatus(int fieldId)
    {
        return PlayerPrefs.GetInt(SaveKey.Field.ToString() + fieldId, 0) == 1;
    }

    public override void SaveCellData(string key, CellSaveData data)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;

        PlayerPrefs.SetString(key, JsonConvert.SerializeObject(data));
    }

    public override CellSaveData LoadCellData(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return null;

        var json = PlayerPrefs.GetString(key, "");
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonConvert.DeserializeObject<CellSaveData>(json);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[DummySaveProvider] Failed to load cell data '{key}': {ex.Message}");
            return null;
        }
    }

    public override void SaveItemsList(Item type, string items)
    {
        PlayerPrefs.SetString(SaveKey.InventoryList + type.ToString(), items ?? "");
    }

    public override List<ItemSaveData> LoadItemsList(Item type)
    {
        var json = PlayerPrefs.GetString(SaveKey.InventoryList + type.ToString(), "");
        if (string.IsNullOrWhiteSpace(json))
            return new List<ItemSaveData>();

        try
        {
            return JsonConvert.DeserializeObject<List<ItemSaveData>>(json) ?? new List<ItemSaveData>();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[DummySaveProvider] Failed to load inventory '{type}': {ex.Message}");
            return new List<ItemSaveData>();
        }
    }

    public override void SaveBackendProfile(string playerId, string friendCode, string displayName)
    {
        PlayerPrefs.SetString(BackendPlayerIdKey, playerId ?? "");
        PlayerPrefs.SetString(BackendFriendCodeKey, friendCode ?? "");
        PlayerPrefs.SetString(BackendDisplayNameKey, displayName ?? "");
    }

    public override (string playerId, string friendCode, string displayName) LoadBackendProfile()
    {
        return (
            PlayerPrefs.GetString(BackendPlayerIdKey, ""),
            PlayerPrefs.GetString(BackendFriendCodeKey, ""),
            PlayerPrefs.GetString(BackendDisplayNameKey, "")
        );
    }

    private static double ReadDouble(string key, double fallback)
    {
        var value = PlayerPrefs.GetString(key, fallback.ToString(CultureInfo.InvariantCulture));
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
            return result;
        if (double.TryParse(value, out result))
            return result;
        return fallback;
    }
}
