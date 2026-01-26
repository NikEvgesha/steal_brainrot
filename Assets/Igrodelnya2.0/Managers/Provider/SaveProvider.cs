using System;
using System.Collections.Generic;
using UnityEngine;

public abstract class SaveProvider : MonoBehaviour
{
    public bool Changed;
    public abstract void Initialize();

    // Методы для работы с громкостью
    public abstract float[] LoadVolume();
    public abstract void SaveVolume(float musicVolume, float soundVolume);

    public abstract void SaveSensivity(float sens);

    public abstract float LoadSensivity();

    // Методы для работы со счётом
    public abstract void SaveScore(float score, int levelId);
    public abstract float LoadScore(int levelId);

    // Прогросс квестов
    public abstract bool GetTutorialProgress();
    public abstract void SaveTutorialProgress(bool endTutorial);
    public abstract void SaveQuestProgress(int step);
    public abstract int LoadQuestProgress();

    // Прочие методы (например, сохранение статуса уровней)
    public abstract void SaveLevelUnlock(int id, bool unlocked);
    public abstract void SaveLevelWin(int id, bool win);

    // Общий метод сохранения прогресса
    public abstract void SaveProgress();
    public abstract bool CheckProgress();

    // Сохранение валюты
    public abstract void SaveGems(double amount);
    public abstract double LoadGems();
    public abstract void SaveGameCoin(double coin);
    public abstract double LoadGameCoin();

    // Достижения
    public abstract void SaveAchievementProgress(AchievementType id, int progress);
    public abstract int LoadAchievementProgress(AchievementType id);

    public abstract void SaveAchievementStatus(string id, bool progress);
    public abstract bool LoadAchievementStatus(string id);

    // Статус уровней
    public abstract void SaveLevelStatus(string lvlName, bool unlocked);
    public abstract bool LoadLevelStatus(string lvlName);

    // статы игрока
    public abstract void SavePlayerStats(int coin, float hp);
    public abstract void SavePlayerHealth(float health);
    public abstract void SavePlayerExperience(int exp, int level);
    public abstract (int, int) LoadPlayerExperience();
    public abstract float LoadPlayerHealth();
    public abstract (int, float) LoadPlayerStats();

    // инвентарь
    public abstract void SaveInventory(List<ItemData> items);
    public abstract List<string> LoadInventory();

    // патроны
    public abstract void SaveAmmo(WeaponType type, int amount);
    public abstract int LoadAmmo(WeaponType type);

    // остальное
    public abstract void SetSave(bool save);

    public abstract void SaveWins(int wins);
    public abstract int LoadWins();

    public abstract void SaveLevelId(int id);

    public abstract int LoadLevelId();

   
    public abstract void SaveRouletteDate(DateTime date);

    public abstract DateTime LoadRouletteDate();


    public abstract void SaveBigPetXP(int xp);
    public abstract void SaveBigPetLvl(int lvl);
    public abstract void SaveBigPetId(int id);
    public abstract void SaveBigPetIncomeTime(string time);
    public abstract int LoadBigPetXP();
    public abstract int LoadBigPetLvl();
    public abstract int LoadBigPetId();
    public abstract string LoadBigPetIncomeTime();

    // Conveyor Data

    public abstract void SaveConveyorCurrentLevel(int id);
    public abstract void SaveConveyorUnlockedLevel(int id);
    public abstract int LoadConveyorCurrentLevel();
    public abstract int LoadConveyorUnlockedLevel();

    // Field Data

    public abstract void SaveFieldUnblockStatus(int fieldId,bool unblocked);
    public abstract bool LoadFieldUnblockStatus(int fieldId);

    public abstract void SaveCellData(string key, CellSaveData data);
    public abstract CellSaveData LoadCellData(string key);


}
