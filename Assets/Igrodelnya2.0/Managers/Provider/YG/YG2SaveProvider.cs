#if YG_SDK_ENABLED
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using YG;

public class YG2SaveProvider : SaveProvider
{
    // Можно добавить какие-либо локальные флаги для отслеживания изменений
    private bool _isChanged;
    public override bool IsInitialized => YG2.isSDKEnabled;

    public override void Initialize()
    {
        // Инициализация YG2, если требуется
        Debug.Log("YG2SaveProvider initialized");
    }

    public override float[] LoadVolume()
    {
        float[] volumes = new float[] { 0.5f, 0.5f };
        if (YG2.isSDKEnabled)
        {
            volumes[0] = YG2.saves.musicVolume;
            volumes[1] = YG2.saves.soundVolume;
        }
        return volumes;
    }

    public override void SaveVolume(float musicVolume, float soundVolume)
    {
        if (YG2.isSDKEnabled)
        {
            YG2.saves.musicVolume = musicVolume;
            YG2.saves.soundVolume = soundVolume;
            _isChanged = true;
        }
    }

    public override void SaveScore(float score, int levelId)
    {
        if (YG2.isSDKEnabled)
        {
            // Предполагаем, что в YG2.saves.scores список уже инициализирован нужным размером
            YG2.saves.scores[levelId - 1] = score;
            string lbName = "lvl" + levelId.ToString();
            YG2.SetLBTimeConvert(lbName, score);
            _isChanged = true;
        }
    }

    public override float LoadScore(int levelId)
    {
        if (YG2.isSDKEnabled)
        {
            return YG2.saves.scores[levelId - 1];
        }
        return 0;
    }

    public override Dictionary<CurrencyType, int> LoadCurrency()
    {
        Dictionary<CurrencyType, int> res = new Dictionary<CurrencyType, int>();
        if (YG2.isSDKEnabled)
        {
            res.Add(CurrencyType.Cups, YG2.saves.cups);
            res.Add(CurrencyType.Gems, YG2.saves.gems);
        }
        else
        {
            res.Add(CurrencyType.Cups, 0);
            res.Add(CurrencyType.Gems, 0);
        }
        return res;
    }

    public override void SaveCurrency(CurrencyType type, int amount)
    {
        if (!YG2.isSDKEnabled) return;

        switch (type)
        {
            case CurrencyType.Cups:
                YG2.saves.cups = amount;
                // Можно обновлять лидерборд с задержкой, если нужно:
                StartCoroutine(UpdateLeaderboard(amount));
                break;
            case CurrencyType.Gems:
                YG2.saves.gems = amount;
                break;
        }
        _isChanged = true;
    }

    private IEnumerator UpdateLeaderboard(int amount)
    {
        yield return new WaitForSeconds(1);
        YG2.SetLeaderboard("main", amount);
    }

    public override void SaveColor(CustomizerColorData data, bool purchased)
    {
        // Предполагается, что структура цветов в YG2 уже инициализирована
        for (int i = 0; i < YG2.saves.colors_id.Length; i++)
        {
            if (YG2.saves.colors_id[i] == data.Identifier)
            {
                YG2.saves.colors_status[i] = purchased;
            }
        }
        _isChanged = true;
    }

    public override Dictionary<CustomizerColorData, bool> LoadColorsStatuses(List<CustomizerColorData> colors)
    {
        Dictionary<CustomizerColorData, bool> res = new Dictionary<CustomizerColorData, bool>();

        // Инициализация или загрузка статусов цветов
        if (YG2.saves.colors_id == null || YG2.saves.colors_id.Length == 0)
        {
            YG2.saves.colors_id = new string[colors.Count];
            YG2.saves.colors_status = new bool[colors.Count];

            for (int i = 0; i < colors.Count; i++)
            {
                YG2.saves.colors_id[i] = colors[i].Identifier;
                YG2.saves.colors_status[i] = colors[i].IsDefault;
                res.Add(colors[i], colors[i].IsDefault);
            }
        }
        else
        {
            for (int i = 0; i < YG2.saves.colors_id.Length; i++)
            {
                // Соответствие между colors[i] и YG2.saves.colors_id[i] должно быть корректно настроено
                var color = colors.FirstOrDefault(c => c.Identifier == YG2.saves.colors_id[i]);
                if (color != null)
                {
                    res.Add(color, YG2.saves.colors_status[i]);
                }
            }
        }
        return res;
    }

    public override void SaveLevelUnlock(int id, bool unlocked)
    {
        if (YG2.isSDKEnabled)
        {
            // Здесь предполагается, что YG2.saves.levels_status уже инициализирован массив нужной длины
            YG2.saves.levels_status[id - 1] = unlocked;
            _isChanged = true;
        }
    }

    public override void SaveLevelWin(int id, bool win)
    {
        if (YG2.isSDKEnabled)
        {
            YG2.saves.levels_win[id - 1] = win;
            _isChanged = true;
        }
    }

    public override Dictionary<LevelData, bool[]> GetLevelStatuses(List<LevelData> lvlsData)
    {
        // Пример реализации, аналогичный исходному коду
        Dictionary<LevelData, bool[]> res = new Dictionary<LevelData, bool[]>();

        // Если в YG2.saves уже есть данные, используем их, иначе инициализируем
        if (YG2.saves.levels_id != null && YG2.saves.levels_id.Count > 0)
        {
            for (int i = 0; i < YG2.saves.levels_id.Count; i++)
            {
                int lvlId = YG2.saves.levels_id[i];
                bool unlocked = YG2.saves.levels_status[i];
                bool win = YG2.saves.levels_win[i];
                var level = lvlsData.FirstOrDefault(l => l.ID == lvlId);
                if (level != null)
                {
                    res.Add(level, new bool[] { unlocked, win });
                }
            }
        }
        else
        {
            // Если данных нет, инициализируем их
            foreach (var lvl in lvlsData)
            {
                // Допустим, по умолчанию уровень не разблокен и не пройден
                res.Add(lvl, new bool[] { false, false });
                if (YG2.isSDKEnabled)
                {
                    YG2.saves.levels_id.Add(lvl.ID);
                    YG2.saves.levels_status.Add(false);
                    YG2.saves.levels_win.Add(false);
                }
            }
        }

        return res;
    }

    public override void SaveProgress()
    {
        if (_isChanged && YG2.isSDKEnabled)
        {
            YG2.SaveProgress();
            _isChanged = false;
        }
    }
}

#endif