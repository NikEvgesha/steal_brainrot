using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class TutorialV1EditorTools
{
    private const string LocalizationPath = "Assets/Igrodelnya2.0/Localization/LocalizationData.asset";
    private const string ViewPrefabPath = "Assets/Resources/Tutorial/TutorialView.prefab";

    private static readonly Dictionary<string, string[]> Translations = new(StringComparer.Ordinal)
    {
        ["UI/Tutorial/Progress"] = new[] { "Обучение {0}/{1}", "Tutorial {0}/{1}" },
        ["UI/Tutorial/Skip"] = new[] { "Пропустить", "Skip" },
        ["UI/Tutorial/SkipTask"] = new[] { "Пропустить задание", "Skip task" },
        ["UI/Tutorial/SkipAll"] = new[] { "Пропустить всё", "Skip all" },
        ["UI/Tutorial/SkipTaskTitle"] = new[] { "Пропустить это задание?", "Skip this task?" },
        ["UI/Tutorial/SkipTaskDescription"] = new[]
        {
            "Будет пропущено только это задание. Следующее доступное обучение всё ещё сможет появиться.",
            "Only this task will be skipped. The next available lesson can still appear."
        },
        ["UI/Tutorial/SkipAllTitle"] = new[] { "Пропустить все текущие задания?", "Skip all current lessons?" },
        ["UI/Tutorial/SkipAllDescription"] = new[]
        {
            "Все известные сейчас задания будут пропущены. Новое обучение из будущих обновлений всё ещё может появиться.",
            "All currently known lessons will be skipped. New lessons added later may still appear."
        },
        ["UI/Tutorial/Collapse"] = new[] { "Свернуть", "Collapse" },
        ["UI/Tutorial/Expand"] = new[] { "Развернуть", "Expand" },
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
            "Подойдите к отмеченному яйцу на конвейере и удерживайте E. Первое яйцо бесплатно.",
            "Approach the marked egg on the conveyor and hold E to take it. Your first egg is free."
        },
        ["UI/Tutorial/Step/acquire_starter_egg/Touch"] = new[]
        {
            "Подойдите к отмеченному яйцу на конвейере и удерживайте кнопку действия. Первое яйцо бесплатно.",
            "Approach the marked egg on the conveyor and hold the action button. Your first egg is free."
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
        ValidateSaveMigration(errors);
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

    [MenuItem("Tools/Tutorial V2/Rebuild Upper-Right View Prefab")]
    public static void RebuildUpperRightViewPrefab()
    {
        TMP_FontAsset font = TMP_Settings.defaultFontAsset;
        if (font == null)
            throw new BuildFailedException("TMP default font asset is missing.");

        var root = new GameObject("TutorialView", typeof(RectTransform), typeof(TutorialView));
        root.layer = LayerMask.NameToLayer("UI");
        RectTransform rootRect = root.GetComponent<RectTransform>();
        SetRect(rootRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        GameObject viewport = CreateRectObject("PanelViewport", root.transform);
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        SetRect(viewportRect, Vector2.one, Vector2.one, Vector2.one, new Vector2(-24f, -108f), new Vector2(560f, 250f));
        viewport.AddComponent<RectMask2D>();

        GameObject panel = CreateImageObject("TutorialPanel", viewport.transform, new Color(0.88f, 0.84f, 0.76f, 0.98f));
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        SetRect(panelRect, Vector2.one, Vector2.one, Vector2.one, Vector2.zero, new Vector2(560f, 250f));
        Outline panelOutline = panel.AddComponent<Outline>();
        panelOutline.effectColor = new Color(0.05f, 0.04f, 0.03f, 1f);
        panelOutline.effectDistance = new Vector2(4f, -4f);

        TMP_Text progress = CreateText("Label_Name", panel.transform, font, 27f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        SetRect(progress.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(-32f, 46f));
        progress.color = new Color(0.08f, 0.17f, 0.28f, 1f);
        progress.enableAutoSizing = true;
        progress.fontSizeMin = 18f;
        progress.fontSizeMax = 28f;
        progress.text = "Обучение 1/13";

        TMP_Text message = CreateText("Text_Message", panel.transform, font, 27f, FontStyles.Bold, TextAlignmentOptions.TopLeft);
        SetRect(message.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(-32f, -112f));
        message.color = new Color(0.08f, 0.1f, 0.13f, 1f);
        message.enableAutoSizing = true;
        message.fontSizeMin = 18f;
        message.fontSizeMax = 28f;
        message.textWrappingMode = TextWrappingModes.Normal;
        message.text = "Текущее задание";

        Button skipTask = CreateButton("Button_SkipTask", panel.transform, font, "Пропустить задание", new Color(0.22f, 0.47f, 0.78f, 1f));
        SetRect(skipTask.transform as RectTransform, Vector2.right, Vector2.right, Vector2.right, new Vector2(-190f, 14f), new Vector2(174f, 48f));

        Button skipAll = CreateButton("Button_SkipAll", panel.transform, font, "Пропустить всё", new Color(0.58f, 0.3f, 0.22f, 1f));
        SetRect(skipAll.transform as RectTransform, Vector2.right, Vector2.right, Vector2.right, new Vector2(-14f, 14f), new Vector2(160f, 48f));

        Button collapse = CreateButton("Button_Collapse", root.transform, font, "<", new Color(0.22f, 0.47f, 0.78f, 1f));
        SetRect(collapse.transform as RectTransform, Vector2.one, Vector2.one, Vector2.one, new Vector2(-14f, -116f), new Vector2(48f, 48f));
        collapse.transform.SetAsLastSibling();

        GameObject arrow = CreateImageObject("Icon_Arrow", root.transform, Color.white);
        RectTransform arrowRect = arrow.GetComponent<RectTransform>();
        SetRect(arrowRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(88f, 80f));
        Image arrowImage = arrow.GetComponent<Image>();
        arrowImage.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/_Sprites/GUI-CasualFantasy/ResourcesData/Sprites/Components/Frame/SpeechFrame01_DemoIcon.png");
        arrowImage.preserveAspect = true;
        arrowImage.raycastTarget = false;
        Outline arrowOutline = arrow.AddComponent<Outline>();
        arrowOutline.effectColor = new Color(1f, 0.78f, 0.05f, 1f);
        arrowOutline.effectDistance = new Vector2(6f, -6f);
        arrow.SetActive(false);

        PrefabUtility.SaveAsPrefabAsset(root, ViewPrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[TutorialV2] Rebuilt editable upper-right tutorial view at {ViewPrefabPath}.");
    }

    private static GameObject CreateRectObject(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        return go;
    }

    private static GameObject CreateImageObject(string name, Transform parent, Color color)
    {
        GameObject go = CreateRectObject(name, parent);
        go.AddComponent<CanvasRenderer>();
        Image image = go.AddComponent<Image>();
        image.color = color;
        return go;
    }

    private static TMP_Text CreateText(
        string name,
        Transform parent,
        TMP_FontAsset font,
        float fontSize,
        FontStyles style,
        TextAlignmentOptions alignment)
    {
        GameObject go = CreateRectObject(name, parent);
        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = alignment;
        text.raycastTarget = false;
        return text;
    }

    private static Button CreateButton(string name, Transform parent, TMP_FontAsset font, string label, Color color)
    {
        GameObject go = CreateImageObject(name, parent, color);
        Button button = go.AddComponent<Button>();
        button.targetGraphic = go.GetComponent<Image>();
        Outline outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0.04f, 0.04f, 0.04f, 1f);
        outline.effectDistance = new Vector2(3f, -3f);

        TMP_Text text = CreateText("Text", go.transform, font, 22f, FontStyles.Bold, TextAlignmentOptions.Center);
        SetRect(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-12f, -8f));
        text.color = Color.white;
        text.enableAutoSizing = true;
        text.fontSizeMin = 14f;
        text.fontSizeMax = 22f;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.text = label;
        return button;
    }

    private static void SetRect(
        RectTransform rect,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 sizeDelta)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
        rect.localScale = Vector3.one;
    }

    private static void ValidateCatalog(List<string> errors)
    {
        var stableIds = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < TutorialStepCatalog.Steps.Length; i++)
        {
            TutorialStepDefinition step = TutorialStepCatalog.Steps[i];
            if (step == null || string.IsNullOrWhiteSpace(step.stableId))
                errors.Add($"Step {i} has no stable id.");
            else if (!stableIds.Add(step.stableId))
                errors.Add($"Duplicate stable id '{step.stableId}'.");
            if (step != null && string.IsNullOrWhiteSpace(step.packId))
                errors.Add($"Step '{step.stableId}' has no pack id.");
            if (step != null && step.definitionRevision < 1)
                errors.Add($"Step '{step.stableId}' has invalid definition revision {step.definitionRevision}.");
            if (step != null && step.progressTarget <= 0d)
                errors.Add($"Step '{step.stableId}' has no positive progress target.");
            if (step != null && step.completionTrigger == TutorialCompletionTrigger.ReachHintTarget &&
                step.hintTarget == TutorialHintTarget.None)
            {
                errors.Add($"Step '{step.stableId}' completes at a target but has no hint target resolver.");
            }
            if (step != null && step.skipPolicy == TutorialSkipPolicy.EnsureStarterEggInInventory &&
                step.completionTrigger != TutorialCompletionTrigger.StarterEggAcquired)
            {
                errors.Add($"Step '{step.stableId}' has a starter-egg skip policy with an incompatible completion trigger.");
            }
            if (step != null && step.skipPolicy == TutorialSkipPolicy.EnsureStarterEggPlaced &&
                step.completionTrigger != TutorialCompletionTrigger.StarterEggPlaced)
            {
                errors.Add($"Step '{step.stableId}' has a placement skip policy with an incompatible completion trigger.");
            }
            if (step != null && step.skipPolicy == TutorialSkipPolicy.EnsureStarterAnimalHatched &&
                step.completionTrigger != TutorialCompletionTrigger.StarterAnimalHatched)
            {
                errors.Add($"Step '{step.stableId}' has a hatch skip policy with an incompatible completion trigger.");
            }
        }

        for (int i = 0; i < TutorialStepCatalog.Steps.Length; i++)
        {
            TutorialStepDefinition step = TutorialStepCatalog.Steps[i];
            if (step == null || step.prerequisiteStableIds == null)
                continue;
            for (int j = 0; j < step.prerequisiteStableIds.Length; j++)
            {
                string prerequisite = step.prerequisiteStableIds[j];
                if (string.Equals(prerequisite, step.stableId, StringComparison.Ordinal))
                    errors.Add($"Step '{step.stableId}' depends on itself.");
                else if (!stableIds.Contains(prerequisite))
                    errors.Add($"Step '{step.stableId}' has missing prerequisite '{prerequisite}'.");
            }
        }

        var aliasSources = new HashSet<string>(StringComparer.Ordinal);
        TutorialStableIdAlias[] aliases = TutorialStepCatalog.StableIdAliases;
        if (aliases == null)
            return;
        for (int i = 0; i < aliases.Length; i++)
        {
            TutorialStableIdAlias alias = aliases[i];
            if (alias == null || string.IsNullOrWhiteSpace(alias.oldStableId) || string.IsNullOrWhiteSpace(alias.newStableId))
            {
                errors.Add($"Stable-id alias {i} is incomplete.");
                continue;
            }
            if (!aliasSources.Add(alias.oldStableId))
                errors.Add($"Duplicate stable-id alias source '{alias.oldStableId}'.");
            if (stableIds.Contains(alias.oldStableId))
                errors.Add($"Stable-id alias source '{alias.oldStableId}' is still used by a current definition.");
            if (!stableIds.Contains(alias.newStableId))
                errors.Add($"Stable-id alias target '{alias.newStableId}' does not exist in the catalog.");
        }
    }

    private static void ValidateSaveMigration(List<string> errors)
    {
        var legacy = new TutorialSaveData
        {
            version = 1,
            perStepInitialized = false,
            stepId = "place_starter_egg",
            stepIndex = 5,
            startedUnix = 100,
            stepStartedUnix = 200
        };
        legacy.Normalize(false);
        for (int i = 0; i < TutorialStepCatalog.Steps.Length; i++)
        {
            TutorialTaskSaveData state = legacy.GetTaskState(TutorialStepCatalog.Steps[i].stableId);
            TutorialTaskStatus expected = i < 5
                ? TutorialTaskStatus.Completed
                : i == 5 ? TutorialTaskStatus.Active : TutorialTaskStatus.Unseen;
            if (state == null || state.status != expected)
                errors.Add($"Legacy active-step migration failed for '{TutorialStepCatalog.Steps[i].stableId}'.");
        }

        var legacyCompleted = new TutorialSaveData
        {
            version = 1,
            perStepInitialized = false,
            completed = true,
            completedUnix = 300
        };
        legacyCompleted.Normalize(true);
        if (!legacyCompleted.AreAllKnownStepsTerminal())
            errors.Add("Completed V1 save did not migrate all known tasks to terminal states.");

        var futureDefinition = new TutorialStepDefinition(
            TutorialStepId.ContinueIndependently,
            "future_lesson_validation",
            "Future lesson",
            "Future lesson",
            priority: 999,
            packId: "future_validation");
        TutorialTaskSaveData future = legacyCompleted.GetOrCreateTaskState(futureDefinition);
        if (future == null || future.status != TutorialTaskStatus.Unseen)
            errors.Add("A new stable id is not eligible as unseen for a previously completed player.");

        var aliasMigration = new TutorialSaveData
        {
            version = TutorialSaveData.CurrentVersion,
            perStepInitialized = true,
            activeStepId = "validation_old_id",
            taskStates = new List<TutorialTaskSaveData>
            {
                new()
                {
                    stableId = "validation_old_id",
                    status = TutorialTaskStatus.Active,
                    progressValue = 3.5d,
                    progressJson = "{\"stage\":2}",
                    rewardGranted = true,
                    startedUnix = 400,
                    updatedUnix = 500
                },
                new()
                {
                    stableId = "validation_new_id",
                    status = TutorialTaskStatus.Unseen,
                    definitionRevision = 2
                }
            }
        };
        if (!aliasMigration.MigrateStableIdAlias("validation_old_id", "validation_new_id"))
            errors.Add("Stable-id alias migration did not report a migrated state.");
        TutorialTaskSaveData aliasResult = aliasMigration.GetTaskState("validation_new_id");
        if (aliasMigration.GetTaskState("validation_old_id") != null || aliasResult == null ||
            aliasResult.status != TutorialTaskStatus.Active || aliasResult.progressValue != 3.5d ||
            !aliasResult.rewardGranted || aliasMigration.activeStepId != "validation_new_id")
        {
            errors.Add("Stable-id alias migration did not preserve active status, progress, reward or active id.");
        }

        var reorderedState = TutorialSaveData.CreateNew();
        reorderedState.Normalize(false);
        reorderedState.MarkTerminal("learn_movement", wasSkipped: false, now: 600);
        reorderedState.MarkTerminal("find_home", wasSkipped: true, now: 601);
        reorderedState.taskStates.Reverse();
        reorderedState.stepIndex = TutorialStepCatalog.Steps.Length - 1;
        reorderedState.Normalize(false);
        if (reorderedState.GetTaskState("learn_movement")?.status != TutorialTaskStatus.Completed ||
            reorderedState.GetTaskState("find_home")?.status != TutorialTaskStatus.Skipped)
        {
            errors.Add("Per-step state changed after persisted task order and legacy index were rearranged.");
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

        string[] requiredChildren =
        {
            "PanelViewport", "TutorialPanel", "Text_Message", "Label_Name",
            "Button_SkipTask", "Button_SkipAll", "Button_Collapse", "Icon_Arrow"
        };
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
