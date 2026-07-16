using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

public static class TutorialV1EditorTools
{
    private const string LocalizationPath = "Assets/Igrodelnya2.0/Localization/LocalizationData.asset";
    private const string ViewPrefabPath = "Assets/Resources/Tutorial/TutorialView.prefab";

    private static readonly Dictionary<string, string[]> Translations = new(StringComparer.Ordinal)
    {
        ["UI/Tutorial/Progress"] = new[] { "Обучение {0}/{1}", "Tutorial {0}/{1}" },
        ["UI/Tutorial/Skip"] = new[] { "Пропустить", "Skip" },
        ["UI/Tutorial/Done"] = new[] { "Готово", "Done" },
        ["UI/Tutorial/SkipTitle"] = new[] { "Пропустить обучение?", "Skip tutorial?" },
        ["UI/Tutorial/SkipDescription"] = new[]
        {
            "Можно продолжить без подсказок. Стартовая награда не будет выдана повторно.",
            "You can continue without hints. The starter reward will not be issued again."
        },
        ["UI/Tutorial/SkipConfirm"] = new[] { "Пропустить", "Skip" },
        ["UI/Tutorial/SkipCancel"] = new[] { "Продолжить", "Continue" },
        ["UI/Tutorial/Step/learn_movement/Desktop"] = new[]
        {
            "Используйте WASD, чтобы пройти несколько метров. Удерживайте правую кнопку мыши для обзора.",
            "Use WASD to walk a few meters. Hold the right mouse button to look around."
        },
        ["UI/Tutorial/Step/learn_movement/Touch"] = new[]
        {
            "Двигайте левый джойстик. Проводите пальцем по правой стороне экрана для обзора.",
            "Move the left joystick to walk. Swipe the right side to look around."
        },
        ["UI/Tutorial/Step/find_home/Desktop"] = new[]
        {
            "Следуйте за стрелкой к большому маркеру над своим домом.",
            "Follow the arrow to the large marker above your home."
        },
        ["UI/Tutorial/Step/find_home/Touch"] = new[]
        {
            "Следуйте за стрелкой к большому маркеру над своим домом.",
            "Follow the arrow to the large marker above your home."
        },
        ["UI/Tutorial/Step/reach_conveyor/Desktop"] = new[]
        {
            "Подойдите к конвейеру на своей базе.",
            "Go to the conveyor on your base."
        },
        ["UI/Tutorial/Step/reach_conveyor/Touch"] = new[]
        {
            "Подойдите к конвейеру на своей базе.",
            "Go to the conveyor on your base."
        },
        ["UI/Tutorial/Step/acquire_starter_egg/Desktop"] = new[]
        {
            "Получите бесплатное стартовое яйцо. Оно уже выбрано на панели быстрого доступа.",
            "Take your free starter egg. It is already selected in the quick bar."
        },
        ["UI/Tutorial/Step/acquire_starter_egg/Touch"] = new[]
        {
            "Получите бесплатное стартовое яйцо. Оно уже выбрано на панели быстрого доступа.",
            "Take your free starter egg. It is already selected in the quick bar."
        },
        ["UI/Tutorial/Step/return_home/Desktop"] = new[]
        {
            "Отнесите яйцо обратно на свою базу. Следуйте за стрелкой.",
            "Bring the egg back to your base. Follow the arrow."
        },
        ["UI/Tutorial/Step/return_home/Touch"] = new[]
        {
            "Отнесите яйцо обратно на свою базу. Следуйте за стрелкой.",
            "Bring the egg back to your base. Follow the arrow."
        },
        ["UI/Tutorial/Step/place_starter_egg/Desktop"] = new[]
        {
            "Встаньте у подсвеченной свободной клетки и удерживайте E, чтобы разместить яйцо.",
            "Stand by the highlighted free cell and hold E to place the egg."
        },
        ["UI/Tutorial/Step/place_starter_egg/Touch"] = new[]
        {
            "Встаньте у подсвеченной свободной клетки и удерживайте кнопку действия, чтобы разместить яйцо.",
            "Stand by the highlighted free cell and hold the action button to place the egg."
        },
        ["UI/Tutorial/Step/hatch_starter_egg/Desktop"] = new[]
        {
            "Дождитесь короткого таймера, затем удерживайте E рядом с яйцом, чтобы вылупить его.",
            "Wait for the short timer, then hold E by the egg to hatch it."
        },
        ["UI/Tutorial/Step/hatch_starter_egg/Touch"] = new[]
        {
            "Дождитесь короткого таймера, затем удерживайте кнопку действия рядом с яйцом.",
            "Wait for the short timer, then hold the action button by the egg to hatch it."
        },
        ["UI/Tutorial/Step/meet_starter_animal/Desktop"] = new[]
        {
            "Отлично! Первое животное гарантировано и уже живёт в этой клетке.",
            "Great! Your first animal is guaranteed and already lives in this cell."
        },
        ["UI/Tutorial/Step/meet_starter_animal/Touch"] = new[]
        {
            "Отлично! Первое животное гарантировано и уже живёт в этой клетке.",
            "Great! Your first animal is guaranteed and already lives in this cell."
        },
        ["UI/Tutorial/Step/wait_first_income/Desktop"] = new[]
        {
            "Подождите немного, пока животное заработает первые монеты.",
            "Wait a moment while your animal earns its first coins."
        },
        ["UI/Tutorial/Step/wait_first_income/Touch"] = new[]
        {
            "Подождите немного, пока животное заработает первые монеты.",
            "Wait a moment while your animal earns its first coins."
        },
        ["UI/Tutorial/Step/collect_first_income/Desktop"] = new[]
        {
            "Подойдите к животному, чтобы забрать заработанные монеты.",
            "Walk up to your animal to collect the coins it earned."
        },
        ["UI/Tutorial/Step/collect_first_income/Touch"] = new[]
        {
            "Подойдите к животному, чтобы забрать заработанные монеты.",
            "Walk up to your animal to collect the coins it earned."
        },
        ["UI/Tutorial/Step/make_first_expansion/Desktop"] = new[]
        {
            "Купите ещё одно яйцо, откройте клетку или приобретите улучшение конвейера.",
            "Buy another egg, unlock a cell, or purchase a conveyor upgrade."
        },
        ["UI/Tutorial/Step/make_first_expansion/Touch"] = new[]
        {
            "Купите ещё одно яйцо, откройте клетку или приобретите улучшение конвейера.",
            "Buy another egg, unlock a cell, or purchase a conveyor upgrade."
        },
        ["UI/Tutorial/Step/claim_album_reward/Desktop"] = new[]
        {
            "Нажмите C, откройте альбом, выберите открытие и заберите первую награду.",
            "Press C to open the album, select your discovery, and claim its first reward."
        },
        ["UI/Tutorial/Step/claim_album_reward/Touch"] = new[]
        {
            "Откройте альбом, выберите своё открытие и заберите первую награду.",
            "Open the album, select your discovery, and claim its first reward."
        },
        ["UI/Tutorial/Step/continue_independently/Desktop"] = new[]
        {
            "Зоопарк работает! Собирайте монеты и двигайтесь к следующему уровню конвейера.",
            "Your zoo is running! Keep collecting coins and work toward the next conveyor level."
        },
        ["UI/Tutorial/Step/continue_independently/Touch"] = new[]
        {
            "Зоопарк работает! Собирайте монеты и двигайтесь к следующему уровню конвейера.",
            "Your zoo is running! Keep collecting coins and work toward the next conveyor level."
        }
    };

    [MenuItem("Tools/Tutorial V1/Synchronize RU-EN Localization")]
    public static void SynchronizeLocalization()
    {
        LocalizationData data = AssetDatabase.LoadAssetAtPath<LocalizationData>(LocalizationPath);
        if (data == null)
            throw new BuildFailedException($"LocalizationData was not found at {LocalizationPath}.");

        if (data.Languages.Count < 2 || data.Languages[0] != "Ru" || data.Languages[1] != "En")
            throw new BuildFailedException("Tutorial localization expects Ru and En as the first two languages.");

        var byKey = new Dictionary<string, LocalizationEntry>(StringComparer.Ordinal);
        foreach (LocalizationEntry entry in data.Entries)
        {
            if (entry != null && !string.IsNullOrWhiteSpace(entry.Key))
                byKey[entry.Key] = entry;
        }

        foreach (KeyValuePair<string, string[]> pair in Translations)
        {
            if (!byKey.TryGetValue(pair.Key, out LocalizationEntry entry))
            {
                entry = new LocalizationEntry { Key = pair.Key };
                data.Entries.Add(entry);
                byKey.Add(pair.Key, entry);
            }

            while (entry.Translations.Count < data.Languages.Count)
                entry.Translations.Add(string.Empty);
            entry.Translations[0] = pair.Value[0];
            entry.Translations[1] = pair.Value[1];
        }

        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[TutorialV1] Synchronized {Translations.Count} RU/EN localization keys.");
    }

    [MenuItem("Tools/Tutorial V1/Validate Setup")]
    public static void ValidateSetup()
    {
        var errors = new List<string>();
        ValidateCatalog(errors);
        ValidateViewPrefab(errors);
        ValidateLocalization(errors);

        if (errors.Count > 0)
            throw new BuildFailedException("[TutorialV1] Validation failed:\n- " + string.Join("\n- ", errors));

        Debug.Log($"[TutorialV1] Validation passed: {TutorialStepCatalog.Steps.Length} steps, prefab and {Translations.Count} RU/EN keys are ready.");
    }

    public static void BatchSynchronizeAndValidate()
    {
        SynchronizeLocalization();
        ValidateSetup();
    }

    private static void ValidateCatalog(List<string> errors)
    {
        if (TutorialStepCatalog.Steps.Length != 13)
            errors.Add($"Expected 13 tutorial steps, got {TutorialStepCatalog.Steps.Length}.");

        var stableIds = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < TutorialStepCatalog.Steps.Length; i++)
        {
            TutorialStepDefinition step = TutorialStepCatalog.Steps[i];
            if (step == null || string.IsNullOrWhiteSpace(step.stableId))
                errors.Add($"Step {i} has no stable id.");
            else if (!stableIds.Add(step.stableId))
                errors.Add($"Duplicate stable id '{step.stableId}'.");
            if (step != null && (int)step.id != i)
                errors.Add($"Step '{step.stableId}' enum value {(int)step.id} does not match index {i}.");
        }
    }

    private static void ValidateViewPrefab(List<string> errors)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ViewPrefabPath);
        if (prefab == null)
        {
            errors.Add($"Tutorial view prefab is missing at {ViewPrefabPath}.");
            return;
        }

        string[] requiredChildren = { "Text_Message", "Label_Name", "Button_SkipText", "Button_SkipIcon", "Icon_Arrow" };
        Transform[] children = prefab.GetComponentsInChildren<Transform>(true);
        foreach (string childName in requiredChildren)
        {
            bool found = Array.Exists(children, child => child != null && child.name == childName);
            if (!found)
                errors.Add($"Tutorial view prefab has no '{childName}' child.");
        }
    }

    private static void ValidateLocalization(List<string> errors)
    {
        LocalizationData data = AssetDatabase.LoadAssetAtPath<LocalizationData>(LocalizationPath);
        if (data == null)
        {
            errors.Add($"LocalizationData is missing at {LocalizationPath}.");
            return;
        }

        var byKey = new Dictionary<string, LocalizationEntry>(StringComparer.Ordinal);
        foreach (LocalizationEntry entry in data.Entries)
        {
            if (entry != null && !string.IsNullOrWhiteSpace(entry.Key))
                byKey[entry.Key] = entry;
        }

        foreach (string key in Translations.Keys)
        {
            if (!byKey.TryGetValue(key, out LocalizationEntry entry))
            {
                errors.Add($"Localization key '{key}' is missing.");
                continue;
            }

            if (entry.Translations.Count < 2 || string.IsNullOrWhiteSpace(entry.Translations[0]) ||
                string.IsNullOrWhiteSpace(entry.Translations[1]) || entry.Translations[0].Contains("�"))
            {
                errors.Add($"Localization key '{key}' has incomplete or invalid RU/EN values.");
            }
        }
    }
}
