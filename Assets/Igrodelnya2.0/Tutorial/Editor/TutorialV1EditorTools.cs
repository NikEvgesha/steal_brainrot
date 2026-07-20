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
    private const string HandPointerPath = "Assets/Resources/Tutorial/UIHandPointer.png";
    private const string PanelTexturePath = "Assets/_Sprites/UIWindowTexture_SoftStuds_v1.png";
    private const string ButtonGradientPath = "Assets/_Sprites/Gradient2.png";

    private static readonly Dictionary<string, string[]> Translations = new(StringComparer.Ordinal)
    {
        ["UI/Tutorial/Progress"] = new[] { "Обучение {0}/{1}", "Tutorial {0}/{1}" },
        ["UI/Tutorial/Reward"] = new[] { "Награда: +{0}", "Reward: +{0}" },
        ["UI/Tutorial/Collapse"] = new[] { "Свернуть", "Collapse" },
        ["UI/Tutorial/Expand"] = new[] { "Развернуть", "Expand" },
        ["UI/Tutorial/Done"] = new[] { "Готово", "Done" },
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

    private static readonly Dictionary<string, string[]> V2Translations = new(StringComparer.Ordinal)
    {
        ["UI/Tutorial/ClaimReward"] = new[] { "Забрать +{0}", "Claim +{0}" },
        ["UI/Tutorial/Continue"] = new[] { "Продолжить", "Continue" },
        ["UI/Tutorial/RewardClaimed"] = new[] { "НАГРАДА ПОЛУЧЕНА!", "REWARD CLAIMED!" },
        ["UI/Tutorial/Progress"] = new[] { "Обучение {0}/{1}", "Tutorial {0}/{1}" },
        ["UI/Tutorial/Reward"] = new[] { "Награда: +{0}", "Reward: +{0}" },
        ["UI/Tutorial/NoReward"] = new[] { "Учебное задание", "Training task" },
        ["UI/Tutorial/Completed"] = new[] { "ЗАДАНИЕ ВЫПОЛНЕНО!", "TASK COMPLETE!" },

        ["UI/Tutorial/Step/learn_movement/Desktop"] = new[] { "Начни двигаться с помощью WASD. Удерживай правую кнопку мыши, чтобы осматриваться.", "Use WASD to start moving. Hold the right mouse button to look around." },
        ["UI/Tutorial/Step/learn_movement/Touch"] = new[] { "Начни двигаться левым джойстиком. Проводи пальцем по правой стороне экрана, чтобы осматриваться.", "Move the left joystick to start walking. Swipe the right side to look around." },
        ["UI/Tutorial/Step/buy_first_egg/Desktop"] = new[] { "Купи отмеченное яйцо за {0} монет.", "Buy the marked egg for {0} coins." },
        ["UI/Tutorial/Step/buy_first_egg/Touch"] = new[] { "Купи отмеченное яйцо за {0} монет.", "Buy the marked egg for {0} coins." },
        ["UI/Tutorial/Step/place_first_egg/Desktop"] = new[] { "Поставь яйцо на ближайшую свободную клетку.", "Place an egg on the nearest free cell." },
        ["UI/Tutorial/Step/place_first_egg/Touch"] = new[] { "Поставь яйцо на ближайшую свободную клетку.", "Place an egg on the nearest free cell." },
        ["UI/Tutorial/Step/use_first_egg_speedup/Desktop"] = new[] { "Ускорь созревание яйца. Первый раз бесплатно, дальше — за рекламу.", "Skip egg maturation now. The first speedup is free; later speedups require an ad." },
        ["UI/Tutorial/Step/use_first_egg_speedup/Touch"] = new[] { "Ускорь созревание яйца. Первый раз бесплатно, дальше — за рекламу.", "Skip egg maturation now. The first speedup is free; later speedups require an ad." },
        ["UI/Tutorial/Step/hatch_first_egg/Desktop"] = new[] { "Яйцо готово. Подойди и удерживай E, чтобы вылупить его.", "The egg is ready. Approach it and hold E to hatch it." },
        ["UI/Tutorial/Step/hatch_first_egg/Touch"] = new[] { "Яйцо готово. Подойди и удерживай кнопку действия, чтобы вылупить его.", "The egg is ready. Approach it and hold the action button to hatch it." },
        ["UI/Tutorial/Step/claim_first_album_rewards/Desktop"] = new[] { "Открой альбом и забери оставшиеся награды за яйцо и животное.", "Open the album and claim the remaining rewards for your egg and animal." },
        ["UI/Tutorial/Step/claim_first_album_rewards/Touch"] = new[] { "Открой альбом и забери оставшиеся награды за яйцо и животное.", "Open the album and claim the remaining rewards for your egg and animal." },
        ["UI/Tutorial/Step/buy_big_pet/Desktop"] = new[] { "Накопи {0} монет на большое животное. Собирай монеты у выставленных животных.", "Save {0} coins to buy the big animal. Collect coins from placed animals." },
        ["UI/Tutorial/Step/buy_big_pet/Touch"] = new[] { "Накопи {0} монет на большое животное. Собирай монеты у выставленных животных.", "Save {0} coins to buy the big animal. Collect coins from placed animals." },
        ["UI/Tutorial/Step/feed_big_pet/Desktop"] = new[] { "Купи первый фрукт и покорми большое животное. Каждый его уровень добавляет 10% к доходу фермы.", "Buy the first fruit and feed it to the big animal. Every big-animal level adds 10% farm income." },
        ["UI/Tutorial/Step/feed_big_pet/Touch"] = new[] { "Купи первый фрукт и покорми большое животное. Каждый его уровень добавляет 10% к доходу фермы.", "Buy the first fruit and feed it to the big animal. Every big-animal level adds 10% farm income." },
        ["UI/Tutorial/Step/unlock_first_territory/Desktop"] = new[] { "Накопи {0} монет и открой самую дешёвую территорию.", "Save {0} coins and unlock the cheapest territory." },
        ["UI/Tutorial/Step/unlock_first_territory/Touch"] = new[] { "Накопи {0} монет и открой самую дешёвую территорию.", "Save {0} coins and unlock the cheapest territory." },
        ["UI/Tutorial/Step/upgrade_first_conveyor/Desktop"] = new[] { "Накопи {0} монет и купи следующее улучшение конвейера.", "Save {0} coins and purchase the next conveyor upgrade." },
        ["UI/Tutorial/Step/upgrade_first_conveyor/Touch"] = new[] { "Накопи {0} монет и купи следующее улучшение конвейера.", "Save {0} coins and purchase the next conveyor upgrade." },

        ["UI/Tutorial/Context/BuyEgg"] = new[] { "Купи отмеченное яйцо за {0} монет.", "Buy the marked egg for {0} coins." },
        ["UI/Tutorial/Context/ReturnHomeEgg"] = new[] { "Вернись на свою ферму вместе с яйцом.", "Return to your farm with the egg." },
        ["UI/Tutorial/Context/ReacquireEgg"] = new[] { "Яйца больше нет. Купи другое доступное яйцо на своём конвейере.", "The egg is gone. Buy another affordable egg from your conveyor." },
        ["UI/Tutorial/Context/OpenInventoryEgg"] = new[] { "Открой инвентарь, чтобы найти яйцо.", "Open the inventory to find your egg." },
        ["UI/Tutorial/Context/ChooseEggTab"] = new[] { "Открой вкладку с яйцами.", "Choose the Eggs tab." },
        ["UI/Tutorial/Context/AddEggQuick"] = new[] { "Нажми на яйцо, чтобы добавить его в быстрый доступ.", "Tap the egg to add it to quick access." },
        ["UI/Tutorial/Context/EquipEgg"] = new[] { "Выбери яйцо на панели быстрого доступа.", "Select the egg in quick access." },
        ["UI/Tutorial/Context/NoFreeCell"] = new[] { "Освободи или открой клетку, чтобы поставить яйцо.", "Unlock or free a cell to place the egg." },
        ["UI/Tutorial/Context/ApproachFreeCell"] = new[] { "Подойди к подсвеченной свободной клетке.", "Go to the highlighted free cell." },
        ["UI/Tutorial/Context/PlaceEggActionDesktop"] = new[] { "Удерживай E, чтобы поставить яйцо сюда.", "Hold E to place the egg here." },
        ["UI/Tutorial/Context/PlaceEggActionTouch"] = new[] { "Удерживай кнопку действия, чтобы поставить яйцо сюда.", "Press the action button to place the egg here." },
        ["UI/Tutorial/Context/ApproachMaturingEgg"] = new[] { "Подойди к созревающему яйцу. Первое мгновенное созревание бесплатно.", "Approach the maturing egg. Your first instant maturation is free." },
        ["UI/Tutorial/Context/FreeSpeedupAction"] = new[] { "Заверши созревание сейчас. Первый раз бесплатно, дальше — за рекламу.", "Finish maturation now. The first time is free; later it requires an ad." },
        ["UI/Tutorial/Context/ApproachReadyEgg"] = new[] { "Подойди к готовому яйцу, чтобы вылупить его.", "Approach the ready egg to hatch it." },
        ["UI/Tutorial/Context/HatchActionDesktop"] = new[] { "Удерживай E, чтобы вылупить готовое яйцо.", "Hold E to hatch the ready egg." },
        ["UI/Tutorial/Context/HatchActionTouch"] = new[] { "Удерживай кнопку действия, чтобы вылупить готовое яйцо.", "Press the action button to hatch the ready egg." },
        ["UI/Tutorial/Context/Album"] = new[] { "Открой альбом и забери оставшиеся награды за яйцо и животное.", "Open the album and claim the remaining egg and animal rewards." },
        ["UI/Tutorial/Context/SaveBigPet"] = new[] { "Накопи {0} монет на большое животное ({1}/{0}). Собирай монеты у выставленных животных.", "Save {0} coins for the big animal ({1}/{0}). Collect coins from placed animals." },
        ["UI/Tutorial/Context/ReturnHomeBigPet"] = new[] { "Монет достаточно. Вернись домой и купи большое животное.", "You have enough coins. Return home to buy the big animal." },
        ["UI/Tutorial/Context/ApproachBigPet"] = new[] { "Подойди к месту большого животного на своей ферме.", "Go to the big-animal place on your farm." },
        ["UI/Tutorial/Context/BuyBigPetAction"] = new[] { "Купи большое животное за {0} монет.", "Buy the big animal for {0} coins." },
        ["UI/Tutorial/Context/SaveFood"] = new[] { "Накопи {0} монет на первый фрукт ({1}/{0}). Каждый уровень большого животного добавляет 10% к доходу фермы.", "Save {0} coins for the first fruit ({1}/{0}). Every big-animal level adds 10% farm income." },
        ["UI/Tutorial/Context/TravelFoodShop"] = new[] { "Отправляйся в магазин еды. Можно использовать кнопку телепорта FOOD.", "Go to the food shop. You can use the FOOD teleport button." },
        ["UI/Tutorial/Context/BuyFoodAction"] = new[] { "Купи первый фрукт за {0} монет.", "Buy the first fruit for {0} coins." },
        ["UI/Tutorial/Context/OpenInventoryFood"] = new[] { "Открой инвентарь, чтобы найти фрукт.", "Open the inventory to find the fruit." },
        ["UI/Tutorial/Context/ChooseFoodTab"] = new[] { "Открой вкладку с едой.", "Choose the Food tab." },
        ["UI/Tutorial/Context/AddFoodQuick"] = new[] { "Нажми на фрукт, чтобы добавить его в быстрый доступ.", "Tap the fruit to add it to quick access." },
        ["UI/Tutorial/Context/EquipFood"] = new[] { "Выбери фрукт на панели быстрого доступа.", "Select the fruit in quick access." },
        ["UI/Tutorial/Context/ReturnHomeFood"] = new[] { "Вернись домой с фруктом, чтобы покормить большое животное.", "Return home with the fruit to feed the big animal." },
        ["UI/Tutorial/Context/ApproachBigPetWithFood"] = new[] { "Отнеси фрукт большому животному.", "Bring the fruit to the big animal." },
        ["UI/Tutorial/Context/FeedAction"] = new[] { "Покорми большое животное. Каждый уровень добавляет 10% ко всему доходу фермы.", "Feed the fruit to the big animal. Each level adds 10% to all farm income." },
        ["UI/Tutorial/Context/SaveTerritory"] = new[] { "Накопи {0} монет на самую дешёвую территорию ({1}/{0}).", "Save {0} coins for the cheapest territory ({1}/{0})." },
        ["UI/Tutorial/Context/EquipHammer"] = new[] { "Выбери молот, чтобы открыть территорию.", "Select the hammer to unlock territory." },
        ["UI/Tutorial/Context/ApproachTerritory"] = new[] { "Подойди к подсвеченной самой дешёвой территории.", "Go to the highlighted cheapest territory." },
        ["UI/Tutorial/Context/BuyTerritoryAction"] = new[] { "Открой эту территорию за {0} монет.", "Unlock this territory for {0} coins." },
        ["UI/Tutorial/Context/SaveConveyor"] = new[] { "Накопи {0} монет на следующее улучшение конвейера ({1}/{0}).", "Save {0} coins for the next conveyor upgrade ({1}/{0})." },
        ["UI/Tutorial/Context/ApproachConveyorUpgrade"] = new[] { "Подойди к своему конвейеру, чтобы открыть улучшения.", "Go to your conveyor to open its upgrades." },
        ["UI/Tutorial/Context/BuyConveyorAction"] = new[] { "Купи это улучшение конвейера за {0} монет.", "Buy this conveyor upgrade for {0} coins." },
        ["UI/Income/BigPetLevelBonus"] = new[] { "Большое животное, уровень {0}: +{1}% к доходу фермы", "Big animal level {0}: +{1}% farm income" },
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

        foreach (KeyValuePair<string, string[]> pair in V2Translations)
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
        Debug.Log($"[TutorialV2] Synchronized {V2Translations.Count} RU/EN localization keys.");
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

        Debug.Log($"[TutorialV2] Validation passed: {TutorialStepCatalog.Steps.Length} steps, prefab and {V2Translations.Count} RU/EN keys are ready.");
    }

    public static void BatchSynchronizeAndValidate()
    {
        SynchronizeLocalization();
        ValidateSetup();
    }

    [MenuItem("Tools/Tutorial V2/Rebuild Upper-Right View Prefab")]
    public static void RebuildUpperRightViewPrefab()
    {
        ConfigureHandPointerImporter();
        TMP_FontAsset font = TMP_Settings.defaultFontAsset;
        if (font == null)
            throw new BuildFailedException("TMP default font asset is missing.");

        var root = new GameObject("TutorialView", typeof(RectTransform), typeof(TutorialView));
        root.layer = LayerMask.NameToLayer("UI");
        RectTransform rootRect = root.GetComponent<RectTransform>();
        SetRect(rootRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        var viewSerialized = new SerializedObject(root.GetComponent<TutorialView>());
        viewSerialized.FindProperty("_panelTexture").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<Sprite>(PanelTexturePath);
        viewSerialized.FindProperty("_buttonGradient").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<Sprite>(ButtonGradientPath);
        viewSerialized.ApplyModifiedPropertiesWithoutUndo();

        GameObject viewport = CreateRectObject("PanelViewport", root.transform);
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        SetRect(viewportRect, Vector2.one, Vector2.one, Vector2.one, new Vector2(0f, -108f), new Vector2(560f, 250f));
        viewport.AddComponent<RectMask2D>();

        GameObject panel = CreateImageObject("TutorialPanel", viewport.transform, new Color(0.015f, 0.018f, 0.024f, 0.82f));
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        SetRect(panelRect, Vector2.one, Vector2.one, Vector2.one, Vector2.zero, new Vector2(560f, 250f));
        BlockyUITheme.ApplyPanel(panel.GetComponent<Image>(), new Color(0.015f, 0.018f, 0.024f, 0.82f), studs: false);
        Outline panelOutline = panel.AddComponent<Outline>();
        panelOutline.effectColor = new Color(0.68f, 0.72f, 0.8f, 0.75f);
        panelOutline.effectDistance = new Vector2(4f, -4f);

        TMP_Text progress = CreateText("Label_Name", panel.transform, font, 27f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        SetRect(progress.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(-32f, 46f));
        progress.color = new Color(0.96f, 0.8f, 0.25f, 1f);
        progress.enableAutoSizing = true;
        progress.fontSizeMin = 18f;
        progress.fontSizeMax = 28f;
        progress.text = "Обучение 1/13";

        TMP_Text message = CreateText("Text_Message", panel.transform, font, 27f, FontStyles.Bold, TextAlignmentOptions.TopLeft);
        SetRect(message.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(-32f, -112f));
        message.color = Color.white;
        message.enableAutoSizing = true;
        message.fontSizeMin = 18f;
        message.fontSizeMax = 28f;
        message.textWrappingMode = TextWrappingModes.Normal;
        message.text = "Текущее задание";

        GameObject rewardBadge = CreateImageObject("RewardBadge", panel.transform, new Color(0.96f, 0.72f, 0.18f, 1f));
        SetRect(rewardBadge.transform as RectTransform, Vector2.right, Vector2.right, Vector2.right, new Vector2(-14f, 14f), new Vector2(190f, 48f));
        Button rewardButton = rewardBadge.AddComponent<Button>();
        rewardButton.targetGraphic = rewardBadge.GetComponent<Image>();
        rewardButton.interactable = false;
        Outline rewardOutline = rewardBadge.AddComponent<Outline>();
        rewardOutline.effectColor = new Color(0.18f, 0.1f, 0.03f, 1f);
        rewardOutline.effectDistance = new Vector2(3f, -3f);

        GameObject rewardIcon = CreateImageObject("Reward_Icon", rewardBadge.transform, Color.white);
        SetRect(rewardIcon.transform as RectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(36f, 36f));
        Image rewardIconImage = rewardIcon.GetComponent<Image>();
        rewardIconImage.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Sprites/ItemIcon_Gem_Pentagon_Purple.png");
        rewardIconImage.preserveAspect = true;
        rewardIconImage.raycastTarget = false;

        TMP_Text rewardText = CreateText("Reward_Text", rewardBadge.transform, font, 22f, FontStyles.Bold, TextAlignmentOptions.Center);
        SetRect(rewardText.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(20f, 0f), new Vector2(-52f, -8f));
        rewardText.color = new Color(0.16f, 0.08f, 0.02f, 1f);
        rewardText.enableAutoSizing = true;
        rewardText.fontSizeMin = 14f;
        rewardText.fontSizeMax = 22f;
        rewardText.textWrappingMode = TextWrappingModes.NoWrap;
        rewardText.text = "Награда: +1";

        Button done = CreateButton("Button_Done", panel.transform, font, "Готово", new Color(0.22f, 0.62f, 0.32f, 1f));
        SetRect(done.transform as RectTransform, Vector2.right, Vector2.right, Vector2.right, new Vector2(-214f, 14f), new Vector2(160f, 48f));
        done.gameObject.SetActive(false);

        Button collapse = CreateButton("Button_Collapse", root.transform, font, ">", new Color(0.22f, 0.47f, 0.78f, 1f));
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

    [MenuItem("Tools/Tutorial V2/Configure Hand Pointer Sprite")]
    public static void ConfigureHandPointerImporter()
    {
        AssetDatabase.ImportAsset(HandPointerPath, ImportAssetOptions.ForceUpdate);
        if (AssetImporter.GetAtPath(HandPointerPath) is not TextureImporter importer)
            throw new BuildFailedException($"Tutorial hand pointer is missing at {HandPointerPath}.");

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();
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
            if (step != null && (step.completionRewardGems < 0 || step.completionRewardGems > 10))
                errors.Add($"Step '{step.stableId}' has invalid completion reward {step.completionRewardGems}; expected 0..10 gems.");
            if (step != null && step.id != TutorialStepId.LearnMovement && step.hintTarget == TutorialHintTarget.None)
            {
                errors.Add($"Step '{step.stableId}' completes at a target but has no hint target resolver.");
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
        if (legacy.GetTaskState("learn_movement")?.status != TutorialTaskStatus.Completed ||
            !string.IsNullOrEmpty(legacy.activeStepId))
            errors.Add("Legacy progress did not preserve the retained movement lesson safely.");
        for (int i = 1; i < TutorialStepCatalog.Steps.Length; i++)
        {
            TutorialTaskSaveData state = legacy.GetTaskState(TutorialStepCatalog.Steps[i].stableId);
            if (state == null || state.status != TutorialTaskStatus.Unseen)
                errors.Add($"New V2 lesson '{TutorialStepCatalog.Steps[i].stableId}' was incorrectly settled by legacy progress.");
        }

        var legacyCompleted = new TutorialSaveData
        {
            version = 1,
            perStepInitialized = false,
            completed = true,
            completedUnix = 300
        };
        legacyCompleted.Normalize(true);
        if (legacyCompleted.AreAllKnownStepsTerminal() ||
            legacyCompleted.GetTaskState("learn_movement")?.status != TutorialTaskStatus.Completed ||
            !legacyCompleted.GetTaskState("learn_movement").completionRewardGranted)
            errors.Add("Completed V1 save did not retain movement while leaving new V2 lessons eligible.");

        var preRewardV2 = TutorialSaveData.CreateNew();
        preRewardV2.Normalize(false);
        preRewardV2.MarkTerminal("learn_movement", wasSkipped: false, now: 350);
        preRewardV2.version = 2;
        preRewardV2.GetTaskState("learn_movement").completionRewardGranted = false;
        preRewardV2.Normalize(false);
        TutorialTaskSaveData migratedReward = preRewardV2.GetTaskState("learn_movement");
        if (migratedReward == null || migratedReward.status != TutorialTaskStatus.Completed ||
            !migratedReward.completionRewardGranted)
        {
            errors.Add("V2 per-step reward migration replayed or left a historical completed task reward-eligible.");
        }

        var futureDefinition = new TutorialStepDefinition(
            TutorialStepId.UpgradeConveyor,
            "future_lesson_validation",
            "Future lesson",
            "Future lesson",
            priority: 999,
            completionRewardGems: 0,
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
                    completionRewardGranted = true,
                    objectiveCompleted = true,
                    objectiveAutoCompleted = true,
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
            !aliasResult.rewardGranted || !aliasResult.completionRewardGranted ||
            !aliasResult.objectiveCompleted || !aliasResult.objectiveAutoCompleted ||
            aliasMigration.activeStepId != "validation_new_id")
        {
            errors.Add("Stable-id alias migration did not preserve active status, progress, objective, rewards or active id.");
        }

        var reorderedState = TutorialSaveData.CreateNew();
        reorderedState.Normalize(false);
        reorderedState.MarkTerminal("learn_movement", wasSkipped: false, now: 600);
        reorderedState.MarkTerminal("buy_first_egg", wasSkipped: true, now: 601);
        reorderedState.taskStates.Reverse();
        reorderedState.stepIndex = TutorialStepCatalog.Steps.Length - 1;
        reorderedState.Normalize(false);
        if (reorderedState.GetTaskState("learn_movement")?.status != TutorialTaskStatus.Completed ||
            reorderedState.GetTaskState("buy_first_egg")?.status != TutorialTaskStatus.Skipped)
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
            "RewardBadge", "Reward_Icon", "Reward_Text", "Button_Done", "Button_Collapse", "Icon_Arrow"
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

        foreach (string key in V2Translations.Keys)
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
