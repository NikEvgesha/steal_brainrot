using System.Collections.Generic;

public sealed class GameAudioCueDefinition
{
    public GameAudioCueDefinition(
        string[] clipNames,
        GameAudioBus bus = GameAudioBus.UI,
        bool spatial = false,
        bool layered = false,
        bool loop = false,
        float volume = 1f,
        float pitchMin = 1f,
        float pitchMax = 1f,
        float minInterval = 0f,
        float minDistance = 2f,
        float maxDistance = 22f)
    {
        ClipNames = clipNames;
        Bus = bus;
        Spatial = spatial;
        Layered = layered;
        Loop = loop;
        Volume = volume;
        PitchMin = pitchMin;
        PitchMax = pitchMax;
        MinInterval = minInterval;
        MinDistance = minDistance;
        MaxDistance = maxDistance;
    }

    public string[] ClipNames { get; }
    public GameAudioBus Bus { get; }
    public bool Spatial { get; }
    public bool Layered { get; }
    public bool Loop { get; }
    public float Volume { get; }
    public float PitchMin { get; }
    public float PitchMax { get; }
    public float MinInterval { get; }
    public float MinDistance { get; }
    public float MaxDistance { get; }
}

public static class GameAudioCatalog
{
    private static readonly string[] StepGrass =
    {
        "step_grass_01", "step_grass_02", "step_grass_03",
        "step_grass_04", "step_grass_05", "step_grass_06"
    };

    private static readonly string[] StepHard =
    {
        "step_hard_01", "step_hard_02", "step_hard_03",
        "step_hard_04", "step_hard_05", "step_hard_06"
    };

    private static readonly string[] HammerSwing =
    {
        "hammer_swing_01", "hammer_swing_02", "hammer_swing_03", "hammer_swing_04"
    };

    private static readonly string[] HammerImpact =
    {
        "hammer_impact_01", "hammer_impact_02", "hammer_impact_03", "hammer_impact_04"
    };

    private static readonly string[] AnimalIdle =
    {
        "animal_bark_01", "animal_bark_02", "animal_bark_03",
        "animal_grunt_01", "animal_grunt_02", "animal_grunt_03",
        "animal_hatchling_01", "animal_frog", "animal_bird"
    };

    private static readonly string[] Chew =
    {
        "chew_01", "chew_02", "chew_03", "chew_04"
    };

    private static readonly string[] WaterMove =
    {
        "water_move", "water_move_02", "water_move_03", "water_move_04"
    };

    private static readonly IReadOnlyDictionary<GameAudioId, GameAudioCueDefinition> Cues =
        new Dictionary<GameAudioId, GameAudioCueDefinition>
        {
            [GameAudioId.SFX_UI_CLICK] = Cue("ui_click", volume: 0.65f, pitchMin: 0.98f, pitchMax: 1.03f, minInterval: 0.025f),
            [GameAudioId.SFX_UI_OPEN] = Cue("ui_open", volume: 0.55f, minInterval: 0.08f),
            [GameAudioId.SFX_UI_CLOSE] = Cue("ui_close", volume: 0.5f, minInterval: 0.08f),
            [GameAudioId.SFX_UI_TAB] = Cue("ui_tab", volume: 0.5f, pitchMin: 0.97f, pitchMax: 1.04f, minInterval: 0.04f),
            [GameAudioId.SFX_UI_CONFIRM] = Cue("ui_confirm", volume: 0.8f, minInterval: 0.06f),
            [GameAudioId.SFX_UI_CANCEL] = Cue("ui_cancel", volume: 0.65f, minInterval: 0.06f),
            [GameAudioId.SFX_UI_ERROR] = Cue("ui_error", volume: 0.75f, minInterval: 0.14f),
            [GameAudioId.SFX_UI_LOCKED] = Cue("ui_locked", volume: 0.7f, minInterval: 0.12f),
            [GameAudioId.SFX_UI_NOTIFICATION] = Cue("notification", volume: 0.75f, minInterval: 0.2f),
            [GameAudioId.SFX_UI_READY] = Cue("ready", volume: 0.7f, minInterval: 0.2f),
            [GameAudioId.SFX_UI_TOGGLE] = Cue("toggle", volume: 0.55f, pitchMin: 0.96f, pitchMax: 1.04f, minInterval: 0.06f),
            [GameAudioId.SFX_UI_SLIDER] = Cue("slider", volume: 0.3f, pitchMin: 0.98f, pitchMax: 1.05f, minInterval: 0.075f),
            [GameAudioId.SFX_INTERACT_START] = Cue("interact_start", volume: 0.65f, minInterval: 0.08f),
            [GameAudioId.SFX_INTERACT_LOOP] = Cue("interact_loop", volume: 0.42f, loop: true),
            [GameAudioId.SFX_INTERACT_COMPLETE] = Cue("ui_confirm", volume: 0.8f, minInterval: 0.08f),
            [GameAudioId.SFX_INTERACT_CANCEL] = Cue("interact_cancel", volume: 0.55f, minInterval: 0.08f),
            [GameAudioId.SFX_STEP_GRASS] = Pool(StepGrass, spatial: true, volume: 0.42f, pitchMin: 0.88f, pitchMax: 1.12f, minInterval: 0.075f),
            [GameAudioId.SFX_STEP_HARD] = Pool(StepHard, spatial: true, volume: 0.4f, pitchMin: 0.88f, pitchMax: 1.12f, minInterval: 0.075f),
            [GameAudioId.SFX_PLAYER_JUMP] = Spatial("jump", volume: 0.55f, pitchMin: 0.97f, pitchMax: 1.04f, minInterval: 0.12f),
            [GameAudioId.SFX_PLAYER_LAND] = Spatial("land", volume: 0.58f, pitchMin: 0.93f, pitchMax: 1.05f, minInterval: 0.12f),
            [GameAudioId.SFX_WATER_SPLASH] = Spatial("water_splash", volume: 0.7f, pitchMin: 0.94f, pitchMax: 1.04f, minInterval: 0.18f),
            [GameAudioId.SFX_WATER_MOVE] = Pool(WaterMove, spatial: true, volume: 0.32f, pitchMin: 0.92f, pitchMax: 1.08f, minInterval: 0.3f),
            [GameAudioId.SFX_WATER_JUMP] = Spatial("water_splash", volume: 0.62f, pitchMin: 0.96f, pitchMax: 1.06f, minInterval: 0.2f),
            [GameAudioId.SFX_TELEPORT] = Spatial("teleport", volume: 0.75f, minInterval: 0.3f, maxDistance: 28f),
            [GameAudioId.SFX_ITEM_EQUIP] = Cue("equip", volume: 0.65f, pitchMin: 0.96f, pitchMax: 1.05f, minInterval: 0.07f),
            [GameAudioId.SFX_ITEM_UNEQUIP] = Cue("equip", volume: 0.48f, pitchMin: 0.82f, pitchMax: 0.9f, minInterval: 0.07f),
            [GameAudioId.SFX_ITEM_PICKUP] = Cue("pickup", volume: 0.65f, pitchMin: 0.96f, pitchMax: 1.05f, minInterval: 0.08f),
            [GameAudioId.SFX_ITEM_PLACE] = Spatial("place", volume: 0.65f, pitchMin: 0.94f, pitchMax: 1.05f, minInterval: 0.08f),
            [GameAudioId.SFX_ITEM_SELL] = Cue("ui_open", volume: 0.42f, pitchMin: 1.04f, pitchMax: 1.08f, minInterval: 0.12f),
            [GameAudioId.SFX_EGG_PLACE] = Spatial("place", volume: 0.7f, pitchMin: 0.94f, pitchMax: 1.03f, minInterval: 0.1f),
            [GameAudioId.SFX_ANIMAL_PLACE] = Spatial("feed_offer", volume: 0.55f, pitchMin: 0.96f, pitchMax: 1.08f, minInterval: 0.1f),
            [GameAudioId.SFX_COIN_SPEND] = Cue("coin_spend", volume: 0.65f, minInterval: 0.08f),
            [GameAudioId.SFX_COIN_GAIN] = Cue("coin_gain", volume: 0.82f, pitchMin: 0.98f, pitchMax: 1.02f, minInterval: 0.075f),
            [GameAudioId.SFX_COIN_GAIN_SOFT] = Cue("coin_spend", volume: 0.25f, pitchMin: 1.05f, pitchMax: 1.15f, minInterval: 0.35f),
            [GameAudioId.SFX_GEM_SPEND] = Cue("gem_spend", volume: 0.62f, minInterval: 0.1f),
            [GameAudioId.SFX_CURRENCY_FAIL] = Layered(new[] { "ui_error", "ui_click" }, volume: 0.6f, minInterval: 0.18f),
            [GameAudioId.SFX_TIMER_START] = Layered(new[] { "clock_tick", "notification" }, spatial: true, volume: 0.45f, minInterval: 0.2f),
            [GameAudioId.SFX_EGG_READY] = Spatial("egg_ready", volume: 0.7f, minInterval: 0.35f, maxDistance: 26f),
            [GameAudioId.SFX_EGG_SPEEDUP] = Layered(new[] { "jump", "major_reward" }, spatial: true, volume: 0.65f, minInterval: 0.25f),
            [GameAudioId.SFX_EGG_BELT_DROP] = Spatial("belt_drop", volume: 0.48f, pitchMin: 0.92f, pitchMax: 1.08f, minInterval: 0.12f),
            [GameAudioId.SFX_HATCH_START] = Spatial("major_reward", volume: 0.45f, pitchMin: 0.88f, pitchMax: 0.94f, minInterval: 0.25f),
            [GameAudioId.SFX_HATCH_TICK] = Spatial("hatch_tick", volume: 0.45f, pitchMin: 0.96f, pitchMax: 1.14f, minInterval: 0.04f),
            [GameAudioId.SFX_EGG_CRACK] = Spatial("egg_crack", volume: 0.8f, pitchMin: 0.96f, pitchMax: 1.03f, minInterval: 0.2f),
            [GameAudioId.SFX_HATCH_REVEAL] = Spatial("animal_bird", volume: 0.62f, pitchMin: 0.96f, pitchMax: 1.08f, minInterval: 0.25f),
            [GameAudioId.SFX_RARE_REVEAL] = Cue("major_reward", volume: 0.9f, minInterval: 0.3f),
            [GameAudioId.SFX_ELEMENT_FIRE] = Spatial("element_fire", volume: 0.55f, minInterval: 0.2f),
            [GameAudioId.SFX_ELEMENT_ELECTRIC] = Spatial("element_electric", volume: 0.62f, minInterval: 0.2f),
            [GameAudioId.SFX_ELEMENT_GOLD] = Spatial("coin_gain", volume: 0.6f, pitchMin: 1.03f, pitchMax: 1.08f, minInterval: 0.2f),
            [GameAudioId.SFX_ELEMENT_DIAMOND] = Layered(new[] { "gem_spend", "egg_ready" }, spatial: true, volume: 0.5f, minInterval: 0.2f),
            [GameAudioId.SFX_OFFLINE_INCOME] = Layered(new[] { "coin_gain", "happy_plucks" }, volume: 0.65f, minInterval: 0.5f),
            [GameAudioId.SFX_ANIMAL_IDLE_POOL] = Pool(AnimalIdle, spatial: true, volume: 0.35f, pitchMin: 0.92f, pitchMax: 1.08f, minInterval: 0.7f, maxDistance: 16f),
            [GameAudioId.SFX_CLAIM_ALL] = Cue("coin_gain", volume: 0.9f, pitchMin: 0.96f, pitchMax: 1.04f, minInterval: 0.25f),
            [GameAudioId.SFX_CLAIM_ALL_X2] = Cue("coin_gain", volume: 1f, pitchMin: 1.08f, pitchMax: 1.12f, minInterval: 0.35f),
            [GameAudioId.SFX_UNLOCK_MAJOR] = Cue("major_reward", volume: 0.85f, minInterval: 0.4f),
            [GameAudioId.SFX_PET_SELECT] = Cue("animal_bird", volume: 0.6f, pitchMin: 0.92f, pitchMax: 1.08f, minInterval: 0.12f),
            [GameAudioId.SFX_FEED_OFFER] = Spatial("feed_offer", volume: 0.55f, pitchMin: 0.94f, pitchMax: 1.06f, minInterval: 0.12f),
            [GameAudioId.SFX_CHEW_LOOP] = Pool(Chew, spatial: true, volume: 0.32f, pitchMin: 0.94f, pitchMax: 1.08f, minInterval: 0.45f, maxDistance: 14f),
            [GameAudioId.SFX_XP_PULSE] = Cue("ui_tab", volume: 0.24f, pitchMin: 1.04f, pitchMax: 1.12f, minInterval: 0.3f),
            [GameAudioId.SFX_LEVEL_UP] = Cue("major_reward", volume: 0.9f, minInterval: 0.4f),
            [GameAudioId.SFX_SHOP_RESTOCK] = Layered(new[] { "board_reset", "ready" }, volume: 0.58f, minInterval: 0.3f),
            [GameAudioId.SFX_HAMMER_SWING] = Pool(HammerSwing, spatial: true, volume: 0.58f, pitchMin: 0.94f, pitchMax: 1.05f, minInterval: 0.12f),
            [GameAudioId.SFX_HAMMER_IMPACT] = Pool(HammerImpact, spatial: true, volume: 0.72f, pitchMin: 0.93f, pitchMax: 1.06f, minInterval: 0.12f),
            [GameAudioId.SFX_TERRITORY_UNLOCK] = Layered(new[] { "board_reset", "major_reward" }, spatial: true, volume: 0.68f, minInterval: 0.35f),
            [GameAudioId.SFX_CONVEYOR_UPGRADE] = Spatial("conveyor_upgrade", volume: 0.72f, minInterval: 0.25f),
            [GameAudioId.SFX_CONVEYOR_ACTIVATE] = Spatial("conveyor_upgrade", volume: 0.66f, pitchMin: 1.04f, pitchMax: 1.08f, minInterval: 0.25f),
            [GameAudioId.SFX_ALBUM_OPEN] = Layered(new[] { "album_open", "egg_ready" }, volume: 0.45f, minInterval: 0.2f),
            [GameAudioId.SFX_ALBUM_CARD] = Cue("album_card", volume: 0.55f, pitchMin: 0.96f, pitchMax: 1.05f, minInterval: 0.06f),
            [GameAudioId.SFX_ALBUM_DISCOVERY] = Layered(new[] { "egg_ready", "ui_confirm" }, volume: 0.65f, minInterval: 0.25f),
            [GameAudioId.SFX_REWARD_READY] = Cue("notification", volume: 0.7f, minInterval: 0.3f),
            [GameAudioId.SFX_REWARD_CLAIM] = Layered(new[] { "coin_gain", "ready" }, volume: 0.65f, minInterval: 0.25f),
            [GameAudioId.SFX_REWARD_CLAIM_MAJOR] = Cue("major_reward", volume: 0.9f, minInterval: 0.35f),
            [GameAudioId.SFX_SHOP_PURCHASE] = Layered(new[] { "coin_gain", "ui_confirm" }, volume: 0.62f, minInterval: 0.2f),
            [GameAudioId.SFX_SHOP_PURCHASE_MAJOR] = Cue("happy_plucks", volume: 0.85f, minInterval: 0.3f),
            [GameAudioId.SFX_BOOST_ACTIVATE] = Cue("major_reward", volume: 0.75f, pitchMin: 1.02f, pitchMax: 1.08f, minInterval: 0.3f),
            [GameAudioId.SFX_ROULETTE_START] = Cue("roulette_start", volume: 0.7f, minInterval: 0.3f),
            [GameAudioId.SFX_ROULETTE_TICK] = Cue("belt_drop", volume: 0.38f, pitchMin: 0.96f, pitchMax: 1.1f, minInterval: 0.045f),
            [GameAudioId.SFX_ROULETTE_STOP] = Cue("ui_locked", volume: 0.75f, minInterval: 0.25f),
            [GameAudioId.SFX_TASK_START] = Cue("notification", volume: 0.65f, minInterval: 0.3f),
            [GameAudioId.SFX_TASK_COMPLETE] = Cue("ready", volume: 0.85f, minInterval: 0.3f),
            [GameAudioId.SFX_SOCIAL_INBOX] = Cue("social_ring", volume: 0.68f, minInterval: 0.4f),
            [GameAudioId.SFX_FRIEND_ACCEPT] = Cue("happy_plucks", volume: 0.78f, minInterval: 0.3f),
            [GameAudioId.SFX_GIFT_SEND] = Cue("gift_send", volume: 0.72f, minInterval: 0.3f),
            [GameAudioId.SFX_GIFT_RECEIVE] = Cue("egg_ready", volume: 0.75f, minInterval: 0.35f),
            [GameAudioId.SFX_GIFT_OPEN] = Cue("gift_open", volume: 0.68f, minInterval: 0.3f),
            [GameAudioId.SFX_GIFT_RETURN] = Layered(new[] { "ui_close", "notification" }, volume: 0.55f, minInterval: 0.35f),
            [GameAudioId.SFX_LIKE] = Cue("feed_offer", volume: 0.62f, pitchMin: 1.04f, pitchMax: 1.12f, minInterval: 0.15f),
            [GameAudioId.SFX_NETWORK_LOST] = Cue("interact_cancel", volume: 0.65f, minInterval: 0.6f),
            [GameAudioId.SFX_NETWORK_RESTORED] = Cue("notification", volume: 0.7f, pitchMin: 1.02f, pitchMax: 1.06f, minInterval: 0.6f),
            [GameAudioId.SFX_AD_SUCCESS] = Cue("ready", volume: 0.78f, minInterval: 0.35f),
            [GameAudioId.SFX_CHEST_OPEN] = Layered(new[] { "album_open", "egg_ready" }, spatial: true, volume: 0.6f, minInterval: 0.35f),
            [GameAudioId.MUS_RARE_REVEAL_STINGER] = Cue("major_reward", volume: 0.85f, minInterval: 0.35f),
            [GameAudioId.AMB_ZOO_DAY] = Loop("zoo_day", GameAudioBus.Environment, spatial: false, volume: 0.2f),
            [GameAudioId.AMB_WIND_SOFT] = Loop("wind_soft", GameAudioBus.Environment, spatial: false, volume: 0.12f),
            [GameAudioId.AMB_WATER_LOOP] = Loop("water_loop", GameAudioBus.Environment, spatial: true, volume: 0.14f, maxDistance: 32f),
            [GameAudioId.AMB_CONVEYOR_LOOP] = Loop("conveyor_loop", GameAudioBus.Environment, spatial: true, volume: 0.2f, maxDistance: 18f),
            [GameAudioId.AMB_ANIMAL_DISTANT] = Loop("animal_distant", GameAudioBus.Environment, spatial: true, volume: 0.14f, maxDistance: 30f),
            [GameAudioId.AMB_FOOD_SHOP] = Loop("food_shop", GameAudioBus.Environment, spatial: true, volume: 0.11f, maxDistance: 18f)
        };

    public static bool TryGet(GameAudioId id, out GameAudioCueDefinition definition)
    {
        return Cues.TryGetValue(id, out definition);
    }

    private static GameAudioCueDefinition Cue(
        string clip,
        float volume = 1f,
        float pitchMin = 1f,
        float pitchMax = 1f,
        float minInterval = 0f,
        bool loop = false)
    {
        return new GameAudioCueDefinition(
            new[] { clip },
            GameAudioBus.UI,
            loop: loop,
            volume: volume,
            pitchMin: pitchMin,
            pitchMax: pitchMax,
            minInterval: minInterval);
    }

    private static GameAudioCueDefinition Spatial(
        string clip,
        float volume = 1f,
        float pitchMin = 1f,
        float pitchMax = 1f,
        float minInterval = 0f,
        float maxDistance = 22f)
    {
        return new GameAudioCueDefinition(
            new[] { clip },
            GameAudioBus.Sound,
            spatial: true,
            volume: volume,
            pitchMin: pitchMin,
            pitchMax: pitchMax,
            minInterval: minInterval,
            maxDistance: maxDistance);
    }

    private static GameAudioCueDefinition Pool(
        string[] clips,
        bool spatial,
        float volume,
        float pitchMin,
        float pitchMax,
        float minInterval,
        float maxDistance = 22f)
    {
        return new GameAudioCueDefinition(
            clips,
            spatial ? GameAudioBus.Sound : GameAudioBus.UI,
            spatial: spatial,
            volume: volume,
            pitchMin: pitchMin,
            pitchMax: pitchMax,
            minInterval: minInterval,
            maxDistance: maxDistance);
    }

    private static GameAudioCueDefinition Layered(
        string[] clips,
        bool spatial = false,
        float volume = 1f,
        float minInterval = 0f)
    {
        return new GameAudioCueDefinition(
            clips,
            spatial ? GameAudioBus.Sound : GameAudioBus.UI,
            spatial: spatial,
            layered: true,
            volume: volume,
            minInterval: minInterval);
    }

    private static GameAudioCueDefinition Loop(
        string clip,
        GameAudioBus bus,
        bool spatial,
        float volume,
        float maxDistance = 28f)
    {
        return new GameAudioCueDefinition(
            new[] { clip },
            bus,
            spatial: spatial,
            loop: true,
            volume: volume,
            maxDistance: maxDistance);
    }
}
