using MirraGames.SDK;
using Newtonsoft.Json;
using System;  // доступ к MirraSDK.Data
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

[System.Serializable]
public class ListSaver
{
    public List<string> list = new();
}
[Serializable]
public class SavedItem //Положение, статус,
{
    public string prefabName;
    public Vector3 position;
    public Quaternion rotation;
    public ItemStatus status;
}

[System.Serializable]
public class SavedItems
{
    public List<SavedItem> list = new();
}
public class MirraSDKSaveProvider : SaveProvider
{
    private const string TutorialStateKey = "Tutorial.State.V1";
    private const string OfflineRewardLastSeenKey = "OfflineReward.LastSeenUnix";
    public override bool IsInitialized => isInitialize;
    private bool isInitialize;
    public override void Initialize()
    {
        // Дождёмся полной готовности системы сохранений
        MirraSDK.WaitForProviders(() =>
        {
            //Debug.Log("MirraSDKSaveProvider initialized");
            isInitialize = true;
        });
    }

    public override float[] LoadVolume()
    {
        if (!isInitialize)
            return new float[] { 0.5f, 0.5f };
        // Достаём значения, с дефолтом 0.5f
        float music = MirraSDK.Data.GetFloat(SaveKey.MusicVolume.ToString(), 0.5f);
        float sound = MirraSDK.Data.GetFloat(SaveKey.SoundVolume.ToString(), 0.5f);
        return new float[] { music, sound };
    }

    public override void SaveVolume(float musicVolume, float soundVolume)
    {
        if (!isInitialize) return;
        MirraSDK.Data.SetFloat(SaveKey.MusicVolume.ToString(), musicVolume);
        MirraSDK.Data.SetFloat(SaveKey.SoundVolume.ToString(), soundVolume);
        Changed = true;
    }


    public override void SaveSensivity(float sens)
    {
        if (!isInitialize) return;
        MirraSDK.Data.SetFloat(SaveKey.Sensivity.ToString(), sens);
        Changed = true;
    }

    public override float LoadSensivity()
    {
        if (!isInitialize)
            return 0.5f;
        float sens = MirraSDK.Data.GetFloat(SaveKey.Sensivity.ToString(), 0.5f);
        return sens;
    }

    public override void SaveScore(float score, int levelId)
    {
        if (!isInitialize) return;
        // Ключ «Score_1», «Score_2» и т.д.
        MirraSDK.Data.SetFloat($"{SaveKey.Score_}{levelId}", score);
        Changed = true;
    }

    public override float LoadScore(int levelId)
    {
        if (!isInitialize)
            return 0f;
        return MirraSDK.Data.GetFloat($"{SaveKey.Score_}{levelId}", 0f);
    }
    public override void SaveTutorialProgress(bool endTutorial)
    {
        if (!isInitialize) return;
        MirraSDK.Data.SetBool(SaveKey.EndTutorial.ToString(), endTutorial);
        Changed = true;
    }
    public override string LoadTutorialState()
    {
        if (!isInitialize) return string.Empty;
        return MirraSDK.Data.GetString(TutorialStateKey, string.Empty);
    }
    public override void SaveTutorialState(string json)
    {
        if (!isInitialize) return;
        MirraSDK.Data.SetString(TutorialStateKey, json ?? string.Empty);
        Changed = true;
    }
    public override void SaveQuestProgress(int step)
    {
        if (!isInitialize) return;
        MirraSDK.Data.SetInt(SaveKey.QuestProgress.ToString(), step);
        Changed = true;
    }
    public override int LoadQuestProgress()
    {
        if (!isInitialize) return 0;
        return MirraSDK.Data.GetInt(SaveKey.QuestProgress.ToString(), 0);
    }
    public override bool GetTutorialProgress()
    {
        if (!isInitialize) return false;
        return MirraSDK.Data.GetBool(SaveKey.EndTutorial.ToString(), false);
    }
    public override void SaveLevelUnlock(int id, bool unlocked)
    {
        if (!isInitialize) return;
        MirraSDK.Data.SetBool($"{SaveKey.LevelUnlock_}{id}", unlocked);
        Changed = true;
    }

    public override void SaveLevelWin(int id, bool win)
    {
        if (!isInitialize) return;
        MirraSDK.Data.SetBool($"{SaveKey.LevelWin_}{id}", win);
        Changed = true;
    }

    public override void SaveGems(double amount)
    {
        if (!isInitialize) return;
        Changed = true;
        MirraSDK.Data.SetString(SaveKey.Gems.ToString(), amount.ToString(CultureInfo.InvariantCulture));
    }

    public override double LoadGems()
    {
        double res = 0;

        if (isInitialize)
        {
            string resStr = MirraSDK.Data.GetString(SaveKey.Gems.ToString(), "0");
            if (!double.TryParse(resStr, NumberStyles.Float, CultureInfo.InvariantCulture, out res))
                double.TryParse(resStr, out res);
        }
        return res;
    }

    public override void SaveProgress()
    {
        if (!isInitialize) return;
        // Синхронизировать все изменения с провайдером (локальным или облачным)
        if (Changed)
        {
            MirraSDK.Data.Save();
            Changed = false;
        }
    }

    public override bool CheckProgress()
    {
        if (!isInitialize) return false;
        // Есть ли хоть что-то из основных ключей?
        return MirraSDK.Data.GetBool(SaveKey.Save.ToString(), false);
    }


    public override void SaveAchievementProgress(AchievementType id, int progress)
    {
        if (!isInitialize) return;
        Changed = true;
        MirraSDK.Data.SetInt(id.ToString(), progress);
    }

    public override int LoadAchievementProgress(AchievementType id)
    {
        if (!isInitialize)
            return 0;
        return MirraSDK.Data.GetInt(id.ToString(), 0);
    }

    public override void SaveAchievementStatus(string id, bool rewarded)
    {
        if (!isInitialize) return;
        Changed = true;
        MirraSDK.Data.SetBool(id.ToString(), rewarded);
    }

    public override bool LoadAchievementStatus(string id)
    {
        if (!isInitialize)
            return false;
        return MirraSDK.Data.GetBool(id.ToString(), false);
    }


    public override void SaveLevelStatus(string key, bool unlocked)
    {
        if (!isInitialize) return;
        Changed = true;
        MirraSDK.Data.SetBool(key, unlocked);

        bool res = MirraSDK.Data.GetBool(key);
        Debug.Log("level " + key + " unlocked: " + res);
    }
    public override bool LoadLevelStatus(string key)
    {
        if (!isInitialize)
            return false;
        bool res = MirraSDK.Data.GetBool(key, false);
        //Debug.Log("level " + key + " unlocked: " + res);
        return res;
    }

    public override void SaveInventory(List<ItemData> items) {
        if (!isInitialize) return;
        Changed = true;

        ListSaver listSaver = new();

        foreach (ItemData item in items)
        {
            listSaver.list.Add(item.Name);
        }

        MirraSDK.Data.SetObject<ListSaver>(SaveKey.InventoryList.ToString(), listSaver);
    }
    public override void SavePlayerStats(int coin, float health)
    {
        if (!isInitialize) return;
        Changed = true;
        MirraSDK.Data.SetInt(SaveKey.Coins.ToString(), coin);
        MirraSDK.Data.SetFloat(SaveKey.Health.ToString(), health);
    }
    public override void SaveGameCoin(double coin)
    {
        if (!isInitialize) return;
        Changed = true;
        MirraSDK.Data.SetString(SaveKey.Coins.ToString(), coin.ToString(CultureInfo.InvariantCulture));
    }
    public override void SavePlayerHealth(float health)
    {
        if (!isInitialize) return;
        Changed = true;
        MirraSDK.Data.SetFloat(SaveKey.Health.ToString(), health);
    }
    public override void SavePlayerExperience(int exp, int level)
    {
        if (!isInitialize) return;
        Changed = true;
        MirraSDK.Data.SetInt(SaveKey.Exp.ToString(), exp);
        MirraSDK.Data.SetInt(SaveKey.Level.ToString(), level);
    }
    public override (int, int) LoadPlayerExperience()
    {
        if (isInitialize)
        {
            return (MirraSDK.Data.GetInt(SaveKey.Exp.ToString()), MirraSDK.Data.GetInt(SaveKey.Level.ToString()));
        }
        return (0, 0);
    }
    public override double LoadGameCoin()
    {
        double res = -1;
        if (isInitialize)
        {
            string coinsStr = MirraSDK.Data.GetString(SaveKey.Coins.ToString(), "-1");
            if (!double.TryParse(coinsStr, NumberStyles.Float, CultureInfo.InvariantCulture, out res))
                double.TryParse(coinsStr, out res);
        }
        return res;
    }
    public override float LoadPlayerHealth()
    {
        if (isInitialize)
        {
            return MirraSDK.Data.GetFloat(SaveKey.Health.ToString());
        }
        return 100;
    }
    public override (int, float) LoadPlayerStats()
    {
        if (isInitialize)
        {
            return (MirraSDK.Data.GetInt(SaveKey.Coins.ToString()), MirraSDK.Data.GetFloat(SaveKey.Health.ToString()));
        }
        return (0, 0);
    }
    public override List<string> LoadInventory()
    {
        ListSaver items = new();
        if (isInitialize)
        {
            items = MirraSDK.Data.GetObject<ListSaver>(SaveKey.InventoryList.ToString(), new ListSaver());
        }
        return items.list;
    }

    public override void SetSave(bool save)
    {
        if (!isInitialize) return;
        Changed = true;
        MirraSDK.Data.SetBool(SaveKey.Save.ToString(), save);
    }

    public override void SaveAmmo(WeaponType type, int amount)
    {
        if (!isInitialize) return;
        Changed = true;
        MirraSDK.Data.SetInt(SaveKey.Ammo_.ToString() + type.ToString(), amount);
    }
    public override int LoadAmmo(WeaponType type)
    {
        int ammo = 0;
        if (isInitialize)
        {
            ammo = MirraSDK.Data.GetInt(SaveKey.Ammo_.ToString() + type.ToString());
        }
        return ammo;
    }

    public override void SaveWins(int wins)
    {
        if (!isInitialize) return;
        Changed = true;
        MirraSDK.Data.SetInt(SaveKey.Wins.ToString(), wins);
    }
    public override int LoadWins()
    {
        int wins = 0;
        if (isInitialize)
        {
            wins = MirraSDK.Data.GetInt(SaveKey.Wins.ToString());
        }
        return wins;
    }


    public override void SaveLevelId(int id)
    {
        if (!isInitialize) return;
        Changed = true;
        MirraSDK.Data.SetInt(SaveKey.LevelId.ToString(), id);
    }

    public override int LoadLevelId()
    {
        if (!isInitialize) return 0;

        int id = MirraSDK.Data.GetInt(SaveKey.LevelId.ToString(), 0);
        return id;
    }

    public override void SaveRouletteDate(DateTime date)
    {
        if (!isInitialize) return;
        Changed = true;
        MirraSDK.Data.SetString(SaveKey.RouletteLastDate.ToString(), date.ToString("O", CultureInfo.InvariantCulture));
        Debug.Log("Date saved: " + date.ToString());
    }

    public override DateTime LoadRouletteDate()
    {
        if (!isInitialize) return DateTime.Today.AddDays(-1);

        string date = MirraSDK.Data.GetString(SaveKey.RouletteLastDate.ToString());
        //Debug.Log("Date loaded: " + date);
        if (date.Length == 0)
        {
            return DateTime.Today.AddDays(-1);
        }
        if (DateTime.TryParse(date, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsedDate))
            return parsedDate;
        if (DateTime.TryParse(date, out parsedDate))
            return parsedDate;
        return DateTime.Today.AddDays(-1);
    }

    public override void SaveOfflineRewardLastSeenUnix(long unix)
    {
        if (!isInitialize) return;
        Changed = true;
        MirraSDK.Data.SetString(
            OfflineRewardLastSeenKey,
            Math.Max(0L, unix).ToString(CultureInfo.InvariantCulture));
    }

    public override long LoadOfflineRewardLastSeenUnix()
    {
        if (!isInitialize) return 0L;
        string raw = MirraSDK.Data.GetString(OfflineRewardLastSeenKey, "0");
        return long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out long unix)
            ? Math.Max(0L, unix)
            : 0L;
    }




    /* Big Pet */

    public override void SaveBigPetXP(int xp)
    {
        if (!isInitialize) return;
        Changed = true;
        MirraSDK.Data.SetInt("BigPetXP", xp);
    }
    public override void SaveBigPetLvl(int lvl)
    {
        if (!isInitialize) return;
        Changed = true;
        MirraSDK.Data.SetInt("BigPetLvl", lvl);
    }
    public override void SaveBigPetId(int id)
    {
        if (!isInitialize) return;
        Changed = true;
        MirraSDK.Data.SetInt("BigPetId", id);
    }
    public override void SaveBigPetIncomeTime(string incomeTime)
    {
        if (!isInitialize) return;
        Changed = true;
        MirraSDK.Data.SetString("BigPetIncomeTime", incomeTime);
    }
    public override int LoadBigPetXP()
    {
        if (!isInitialize) return 0;

        int res = MirraSDK.Data.GetInt("BigPetXP", 0);
        return res;
    }
    public override int LoadBigPetLvl()
    {
        if (!isInitialize) return 0;

        int res = MirraSDK.Data.GetInt("BigPetLvl", 1);
        return res;
    }
    public override int LoadBigPetId()
    {
        if (!isInitialize) return 0;

        int res = MirraSDK.Data.GetInt("BigPetId", 0);
        return res;
    }
    public override string LoadBigPetIncomeTime()
    {
        if (!isInitialize) return "";

        string res = MirraSDK.Data.GetString("BigPetIncomeTime", "");
        return res;
    }


    public override void SaveBigPetStatus(bool purchased)
    {
        if (!isInitialize) return;
        Changed = true;
        MirraSDK.Data.SetBool("BigPetStatus", purchased);
    }

    public override bool LoadBigPetStatus()
    {
        if (!isInitialize) return false;

        bool res = MirraSDK.Data.GetBool("BigPetStatus", false);
        return res;
    }



    /// <summary>
    /// Conveyor data
    /// </summary>
    /// <param name="id"></param>
    public override void SaveConveyorCurrentLevel(int id)
    {
        if (!isInitialize) return;

        Changed = true;
        MirraSDK.Data.SetInt("ConveyorCurrentLvl", id);
    }
    public override void SaveConveyorUnlockedLevel(int id)
    {
        if (!isInitialize) return;

        Changed = true;
        MirraSDK.Data.SetInt("ConveyorUnlockedLvl", id);
    }
    public override int LoadConveyorCurrentLevel()
    {
        if (!isInitialize) return 0 ;

        int res = MirraSDK.Data.GetInt("ConveyorCurrentLvl", 0);
        return res;
    }
    public override int LoadConveyorUnlockedLevel()
    {
        if (!isInitialize) return 0;

        int res = MirraSDK.Data.GetInt("ConveyorUnlockedLvl", 0);
        return res;
    }

    public override void SaveFieldUnblockStatus(int fieldId, bool unblocked)
    {
        if (!isInitialize) return;

        Changed = true;
        MirraSDK.Data.SetBool(SaveKey.Field.ToString() + fieldId, unblocked);

    }
    public override bool LoadFieldUnblockStatus(int fieldId)
    {
        if (!isInitialize) return false;

        bool res = MirraSDK.Data.GetBool(SaveKey.Field.ToString() + fieldId, false);
        return res;
    }

    public override void SaveCellData(string key, CellSaveData data)
    {
        if (!isInitialize) return;

        Changed = true;
        MirraSDK.Data.SetObject<CellSaveData>(key, data);
    }
    public override CellSaveData LoadCellData(string key)
    {
        if (!isInitialize) return null;

        CellSaveData res = MirraSDK.Data.GetObject<CellSaveData>(key, null);
        return res;
    }

    public override void SaveItemsList(Item type, string json)
    {
        if (!isInitialize) return;
        Changed = true;
        MirraSDK.Data.SetString(SaveKey.InventoryList + type.ToString(), json);
    }
    public override List<ItemSaveData> LoadItemsList(Item type)
    {
        if (!isInitialize) return new List<ItemSaveData>();
        string json = MirraSDK.Data.GetString(SaveKey.InventoryList + type.ToString(), "");
        if (string.IsNullOrWhiteSpace(json))
            return new List<ItemSaveData>();

        try
        {
            List<ItemSaveData> res = JsonConvert.DeserializeObject<List<ItemSaveData>>(json);
            return res != null ? res : new List<ItemSaveData>();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[MirraSDKSaveProvider] Failed to load inventory '{type}': {ex.Message}");
            GameAnalytics.Track(AnalyticsEventNames.SaveError, GameAnalytics.Params(
                "operation", "load_inventory",
                "item_type", type.ToString().ToLowerInvariant(),
                "save_provider", "mirra_sdk",
                "exception_type", ex.GetType().Name,
                "source", "save_provider",
                "result", "recovered",
                "failure_reason", "invalid_serialized_inventory"),
                AnalyticsPriority.Diagnostic,
                "load_inventory:" + type);
            return new List<ItemSaveData>();
        }
    }
    public override void SaveBackendProfile(string playerId, string friendCode, string displayName)
    {
        if (!isInitialize) return;
        Changed = true;
        MirraSDK.Data.SetString("BackendPlayerId", playerId ?? "");
        MirraSDK.Data.SetString("BackendFriendCode", friendCode ?? "");
        MirraSDK.Data.SetString("BackendDisplayName", displayName ?? "");
    }

    public override (string playerId, string friendCode, string displayName) LoadBackendProfile()
    {
        if (!isInitialize) return ("", "", "");
        var pid = MirraSDK.Data.GetString("BackendPlayerId", "");
        var code = MirraSDK.Data.GetString("BackendFriendCode", "");
        var name = MirraSDK.Data.GetString("BackendDisplayName", "");
        return (pid, code, name);
    }


}
