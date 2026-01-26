using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using MirraGames.SDK;
using System;

public class SaveManager : MonoBehaviour
{
    [SerializeField] private SaveProvider saveProvider; // Ќазначаем в инспекторе нужный провайдер (YG2SaveProvider, DebugSaveProvider и т.д.)
    [SerializeField] private bool _newPlayer;
    public bool IsNewPlayer => saveProvider.CheckProgress() == false;

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
            saveProvider.SaveProgress();
        }
    }

    public void SetSave(bool haveSave)
    {
        saveProvider.SetSave(haveSave);
    }

    // ѕример методов, которые делегируют работу провайдеру:
    public float[] GetVolume()
    {
        return saveProvider.LoadVolume();
    }
    public void SaveQuestProgress(int step = 0)
    {
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
        saveProvider.SaveTutorialProgress(endTutorial);
    }
    public void SaveGems(double amount)
    {
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
        saveProvider.SaveRouletteDate(date);
    }

    public DateTime LoadRouletteDate()
    {
        return saveProvider.LoadRouletteDate();
    }


    public void SaveBigPetXP(int xp)
    {
        saveProvider.SaveBigPetXP(xp);
    }
    public void SaveBigPetLvl(int lvl)
    {
        saveProvider.SaveBigPetLvl(lvl);
    }
    public void SaveBigPetId(int id)
    {
        saveProvider.SaveBigPetId(id);
    }
    public void SaveBigPetIncomeTime(string incomeTime)
    {
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
        saveProvider.SaveConveyorCurrentLevel(id);
    }
    public void SaveConveyorUnlockedLevel(int id)
    {
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
        saveProvider.SaveFieldUnblockStatus(fieldId, unblocked);
    }

    public bool LoadFieldUnblockStatus(int fieldId)
    {
        return saveProvider.LoadFieldUnblockStatus(fieldId);
    }


    public void SaveCellData(string id, CellSaveData data)
    {
        saveProvider.SaveCellData(id, data);
    }

    public CellSaveData LoadCellData(string id)
    {
        return saveProvider.LoadCellData(id);
    }

}
