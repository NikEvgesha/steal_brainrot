using System;

public enum TutorialStepId
{
    LearnMovement = 0,
    BuyFirstEgg = 1,
    PlaceFirstEgg = 2,
    UseFreeEggSpeedup = 3,
    HatchReadyEgg = 4,
    ClaimFirstAlbumRewards = 5,
    BuyBigPet = 6,
    FeedBigPet = 7,
    UnlockTerritory = 8,
    UpgradeConveyor = 9
}

public enum TutorialActivationTrigger
{
    PlayerReady = 0,
    AffordableEggAvailable = 1,
    OwnedEggAvailable = 2,
    MaturingEggAvailable = 3,
    ReadyEggAvailable = 4,
    AlbumRewardsReady = 5,
    BigPetReady = 6,
    FoodLessonReady = 7,
    TerritoryReady = 8,
    ConveyorUpgradeReady = 9
}

public enum TutorialStartAction
{
    None = 0,
    EnsureAffordableEgg = 1
}

public enum TutorialProgressType
{
    BooleanFact = 0,
    GameplaySignal = 1,
    DynamicCurrencyGoal = 2,
    DynamicContext = 3
}

public enum TutorialCompletionTrigger
{
    MovementStarted = 0,
    EggPurchased = 1,
    EggPlaced = 2,
    EggSpeedupUsed = 3,
    EggHatched = 4,
    AlbumRewardsClaimed = 5,
    BigPetPurchased = 6,
    BigPetFed = 7,
    TerritoryUnlocked = 8,
    ConveyorUpgraded = 9
}

public enum TutorialHintTarget
{
    None = 0,
    AffordableEgg = 1,
    PlaceEggContext = 2,
    MaturingEgg = 3,
    ReadyEgg = 4,
    AlbumRewards = 5,
    BigPetPurchase = 6,
    FeedBigPetContext = 7,
    TerritoryPurchase = 8,
    ConveyorUpgrade = 9
}

[Serializable]
public sealed class TutorialStableIdAlias
{
    public string oldStableId;
    public string newStableId;

    public TutorialStableIdAlias(string oldStableId, string newStableId)
    {
        this.oldStableId = oldStableId;
        this.newStableId = newStableId;
    }
}

[Serializable]
public sealed class TutorialStepDefinition
{
    public TutorialStepId id;
    public string stableId;
    public string packId;
    public int definitionRevision;
    public int priority;
    public string[] prerequisiteStableIds;
    public TutorialActivationTrigger activationTrigger;
    public TutorialStartAction startAction;
    public TutorialProgressType progressType;
    public TutorialCompletionTrigger completionTrigger;
    public TutorialHintTarget hintTarget;
    public int completionRewardGems;
    public string desktopTextKey;
    public string touchTextKey;
    public string desktopFallback;
    public string touchFallback;

    public TutorialStepDefinition(
        TutorialStepId id,
        string stableId,
        string desktopFallback,
        string touchFallback,
        int priority,
        int completionRewardGems,
        string prerequisiteStableId = null,
        int definitionRevision = 1,
        string packId = TutorialStepCatalog.CorePackId,
        TutorialActivationTrigger activationTrigger = TutorialActivationTrigger.PlayerReady,
        TutorialStartAction startAction = TutorialStartAction.None,
        TutorialProgressType progressType = TutorialProgressType.BooleanFact,
        TutorialCompletionTrigger completionTrigger = TutorialCompletionTrigger.MovementStarted,
        TutorialHintTarget hintTarget = TutorialHintTarget.None)
    {
        this.id = id;
        this.stableId = stableId;
        this.packId = packId;
        this.definitionRevision = Math.Max(1, definitionRevision);
        this.priority = priority;
        prerequisiteStableIds = string.IsNullOrWhiteSpace(prerequisiteStableId)
            ? Array.Empty<string>()
            : new[] { prerequisiteStableId };
        this.activationTrigger = activationTrigger;
        this.startAction = startAction;
        this.progressType = progressType;
        this.completionTrigger = completionTrigger;
        this.hintTarget = hintTarget;
        this.completionRewardGems = Math.Max(0, completionRewardGems);
        desktopTextKey = $"UI/Tutorial/Step/{stableId}/Desktop";
        touchTextKey = $"UI/Tutorial/Step/{stableId}/Touch";
        this.desktopFallback = desktopFallback;
        this.touchFallback = touchFallback;
    }
}

public static class TutorialStepCatalog
{
    public const string CorePackId = "core_v2";

    // Populate only when a shipped stable id is intentionally renamed. Aliases are
    // one-way migrations and must never be reused for a different lesson meaning.
    public static readonly TutorialStableIdAlias[] StableIdAliases = Array.Empty<TutorialStableIdAlias>();

    public static readonly TutorialStepDefinition[] Steps =
    {
        new(TutorialStepId.LearnMovement, "learn_movement",
            "Use WASD to start moving. Hold the right mouse button to look around.",
            "Move the left joystick to start walking. Swipe the right side to look around.",
            priority: 0,
            completionRewardGems: 0,
            activationTrigger: TutorialActivationTrigger.PlayerReady,
            progressType: TutorialProgressType.BooleanFact,
            completionTrigger: TutorialCompletionTrigger.MovementStarted),
        new(TutorialStepId.BuyFirstEgg, "buy_first_egg",
            "Buy the marked egg for {0} coins.",
            "Buy the marked egg for {0} coins.",
            priority: 10,
            completionRewardGems: 3,
            prerequisiteStableId: "learn_movement",
            activationTrigger: TutorialActivationTrigger.AffordableEggAvailable,
            startAction: TutorialStartAction.EnsureAffordableEgg,
            progressType: TutorialProgressType.DynamicContext,
            completionTrigger: TutorialCompletionTrigger.EggPurchased,
            hintTarget: TutorialHintTarget.AffordableEgg),
        new(TutorialStepId.PlaceFirstEgg, "place_first_egg",
            "Place an egg on the nearest free cell.",
            "Place an egg on the nearest free cell.",
            priority: 20,
            completionRewardGems: 3,
            prerequisiteStableId: "buy_first_egg",
            activationTrigger: TutorialActivationTrigger.OwnedEggAvailable,
            progressType: TutorialProgressType.DynamicContext,
            completionTrigger: TutorialCompletionTrigger.EggPlaced,
            hintTarget: TutorialHintTarget.PlaceEggContext),
        new(TutorialStepId.UseFreeEggSpeedup, "use_first_egg_speedup",
            "Skip egg maturation now. The first speedup is free; later speedups require an ad.",
            "Skip egg maturation now. The first speedup is free; later speedups require an ad.",
            priority: 900,
            completionRewardGems: 5,
            prerequisiteStableId: "place_first_egg",
            activationTrigger: TutorialActivationTrigger.MaturingEggAvailable,
            progressType: TutorialProgressType.DynamicContext,
            completionTrigger: TutorialCompletionTrigger.EggSpeedupUsed,
            hintTarget: TutorialHintTarget.MaturingEgg),
        new(TutorialStepId.HatchReadyEgg, "hatch_first_egg",
            "The egg is ready. Approach it and hold E to hatch it.",
            "The egg is ready. Approach it and hold the action button to hatch it.",
            priority: 30,
            completionRewardGems: 0,
            prerequisiteStableId: "place_first_egg",
            activationTrigger: TutorialActivationTrigger.ReadyEggAvailable,
            progressType: TutorialProgressType.DynamicContext,
            completionTrigger: TutorialCompletionTrigger.EggHatched,
            hintTarget: TutorialHintTarget.ReadyEgg),
        new(TutorialStepId.ClaimFirstAlbumRewards, "claim_first_album_rewards",
            "Open the album and claim the remaining rewards for your egg and animal.",
            "Open the album and claim the remaining rewards for your egg and animal.",
            priority: 40,
            completionRewardGems: 0,
            prerequisiteStableId: "hatch_first_egg",
            activationTrigger: TutorialActivationTrigger.AlbumRewardsReady,
            progressType: TutorialProgressType.DynamicContext,
            completionTrigger: TutorialCompletionTrigger.AlbumRewardsClaimed,
            hintTarget: TutorialHintTarget.AlbumRewards),
        new(TutorialStepId.BuyBigPet, "buy_big_pet",
            "Save {0} coins to buy the big animal. Collect coins from placed animals.",
            "Save {0} coins to buy the big animal. Collect coins from placed animals.",
            priority: 50,
            completionRewardGems: 5,
            prerequisiteStableId: "claim_first_album_rewards",
            activationTrigger: TutorialActivationTrigger.BigPetReady,
            progressType: TutorialProgressType.DynamicCurrencyGoal,
            completionTrigger: TutorialCompletionTrigger.BigPetPurchased,
            hintTarget: TutorialHintTarget.BigPetPurchase),
        new(TutorialStepId.FeedBigPet, "feed_big_pet",
            "Buy a fruit and feed it to the big animal. The task completes after feeding. Late levels unlock an especially large income bonus.",
            "Buy a fruit and feed it to the big animal. The task completes after feeding. Late levels unlock an especially large income bonus.",
            priority: 60,
            completionRewardGems: 3,
            prerequisiteStableId: "buy_big_pet",
            activationTrigger: TutorialActivationTrigger.FoodLessonReady,
            progressType: TutorialProgressType.DynamicContext,
            completionTrigger: TutorialCompletionTrigger.BigPetFed,
            hintTarget: TutorialHintTarget.FeedBigPetContext),
        new(TutorialStepId.UnlockTerritory, "unlock_first_territory",
            "Save {0} coins and unlock the cheapest territory.",
            "Save {0} coins and unlock the cheapest territory.",
            priority: 70,
            completionRewardGems: 3,
            prerequisiteStableId: "feed_big_pet",
            activationTrigger: TutorialActivationTrigger.TerritoryReady,
            progressType: TutorialProgressType.DynamicCurrencyGoal,
            completionTrigger: TutorialCompletionTrigger.TerritoryUnlocked,
            hintTarget: TutorialHintTarget.TerritoryPurchase),
        new(TutorialStepId.UpgradeConveyor, "upgrade_first_conveyor",
            "Save {0} coins and purchase the next conveyor upgrade.",
            "Save {0} coins and purchase the next conveyor upgrade.",
            priority: 80,
            completionRewardGems: 10,
            prerequisiteStableId: "unlock_first_territory",
            activationTrigger: TutorialActivationTrigger.ConveyorUpgradeReady,
            progressType: TutorialProgressType.DynamicCurrencyGoal,
            completionTrigger: TutorialCompletionTrigger.ConveyorUpgraded,
            hintTarget: TutorialHintTarget.ConveyorUpgrade)
    };

    public static TutorialStepDefinition Find(string stableId)
    {
        int index = FindIndex(stableId, -1);
        return index >= 0 ? Steps[index] : null;
    }

    public static int FindIndex(string stableId, int fallbackIndex = 0)
    {
        if (!string.IsNullOrWhiteSpace(stableId))
        {
            for (int i = 0; i < Steps.Length; i++)
            {
                if (string.Equals(Steps[i].stableId, stableId, StringComparison.Ordinal))
                    return i;
            }
        }

        if (fallbackIndex < 0)
            return -1;
        if (fallbackIndex >= Steps.Length)
            return Steps.Length - 1;
        return fallbackIndex;
    }
}
