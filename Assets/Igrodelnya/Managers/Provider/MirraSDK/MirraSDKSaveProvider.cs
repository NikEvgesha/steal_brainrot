using UnityEngine;
using MirraGames.SDK;
using System.Collections.Generic;
using System;  // доступ к MirraSDK.Data

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

    public override void SaveGems(int amount)
    {
        if (!isInitialize) return;
        Changed = true;
        MirraSDK.Data.SetInt(SaveKey.Gems.ToString(), amount);
    }

    public override int LoadGems()
    {
        if (!isInitialize)
            return 0;
        return MirraSDK.Data.GetInt(SaveKey.Gems.ToString(), 0);
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
    public override void SaveGameCoin(int coin)
    {
        if (!isInitialize) return;
        Changed = true;
        MirraSDK.Data.SetInt(SaveKey.Coins.ToString(), coin);
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
    public override int LoadGameCoin()
    {
        if (isInitialize)
        {
            return MirraSDK.Data.GetInt(SaveKey.Coins.ToString());
        }
        return 0;
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
        MirraSDK.Data.SetString(SaveKey.RouletteLastDate.ToString(), date.Date.ToString());
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
        return DateTime.Parse(date);
    }

}
