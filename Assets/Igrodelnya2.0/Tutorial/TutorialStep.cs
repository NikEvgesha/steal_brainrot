using System;

public enum TutorialStepId
{
    LearnMovement = 0,
    FindHome = 1,
    ReachConveyor = 2,
    AcquireStarterEgg = 3,
    ReturnHome = 4,
    PlaceStarterEgg = 5,
    HatchStarterEgg = 6,
    MeetStarterAnimal = 7,
    WaitForFirstIncome = 8,
    CollectFirstIncome = 9,
    MakeFirstExpansion = 10,
    ClaimAlbumReward = 11,
    ContinueIndependently = 12
}

public enum TutorialActivationTrigger
{
    PlayerReady = 0,
    LocalHomeReady = 1,
    LocalConveyorReady = 2,
    StarterOfferReady = 3,
    StarterProgressItemPresent = 4,
    FreeLocalCellReady = 5,
    StarterEggPlaced = 6,
    StarterAnimalPresent = 7,
    CollectibleIncomeReady = 8,
    ExpansionTargetReady = 9,
    AlbumReady = 10,
    PrerequisitesTerminal = 11
}

public enum TutorialStartAction
{
    None = 0,
    EnsureStarterEggOffer = 1
}

public enum TutorialProgressType
{
    BooleanFact = 0,
    DistanceTravelled = 1,
    TargetReached = 2,
    GameplaySignal = 3,
    ManualConfirmation = 4
}

public enum TutorialCompletionTrigger
{
    MovementDistance = 0,
    ReachHintTarget = 1,
    StarterEggAcquired = 2,
    StarterEggPlaced = 3,
    StarterAnimalHatched = 4,
    StarterAnimalObserved = 5,
    FirstIncomeReady = 6,
    FirstIncomeCollected = 7,
    FirstExpansionMade = 8,
    AlbumRewardClaimed = 9,
    ManualConfirmation = 10
}

public enum TutorialHintTarget
{
    None = 0,
    LocalHome = 1,
    LocalConveyor = 2,
    StarterEggOffer = 3,
    FreeLocalCell = 4,
    LocalEggCell = 5,
    LocalAnimalCell = 6,
    CollectibleIncomeCell = 7,
    ExpansionTarget = 8,
    AlbumTarget = 9
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
    public double progressTarget;
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
        int completionRewardGems = 1,
        string prerequisiteStableId = null,
        int definitionRevision = 1,
        string packId = TutorialStepCatalog.CorePackId,
        TutorialActivationTrigger activationTrigger = TutorialActivationTrigger.PrerequisitesTerminal,
        TutorialStartAction startAction = TutorialStartAction.None,
        TutorialProgressType progressType = TutorialProgressType.BooleanFact,
        double progressTarget = 1d,
        TutorialCompletionTrigger completionTrigger = TutorialCompletionTrigger.ManualConfirmation,
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
        this.progressTarget = Math.Max(0d, progressTarget);
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
    public const string CorePackId = "core_v1";

    // Populate only when a shipped stable id is intentionally renamed. Aliases are
    // one-way migrations and must never be reused for a different lesson meaning.
    public static readonly TutorialStableIdAlias[] StableIdAliases = Array.Empty<TutorialStableIdAlias>();

    public static readonly TutorialStepDefinition[] Steps =
    {
        new(TutorialStepId.LearnMovement, "learn_movement",
            "Use WASD to walk a few meters. Hold the right mouse button to look around.",
            "Move the left joystick to walk. Swipe the right side to look around.",
            priority: 0,
            completionRewardGems: 1,
            activationTrigger: TutorialActivationTrigger.PlayerReady,
            progressType: TutorialProgressType.DistanceTravelled,
            progressTarget: 5d,
            completionTrigger: TutorialCompletionTrigger.MovementDistance),
        new(TutorialStepId.FindHome, "find_home",
            "Follow the arrow to the large marker above your home.",
            "Follow the arrow to the large marker above your home.",
            priority: 10,
            completionRewardGems: 1,
            prerequisiteStableId: "learn_movement",
            activationTrigger: TutorialActivationTrigger.LocalHomeReady,
            progressType: TutorialProgressType.TargetReached,
            completionTrigger: TutorialCompletionTrigger.ReachHintTarget,
            hintTarget: TutorialHintTarget.LocalHome),
        new(TutorialStepId.ReachConveyor, "reach_conveyor",
            "Go to the conveyor on your base.",
            "Go to the conveyor on your base.",
            priority: 20,
            completionRewardGems: 1,
            prerequisiteStableId: "find_home",
            activationTrigger: TutorialActivationTrigger.LocalConveyorReady,
            progressType: TutorialProgressType.TargetReached,
            completionTrigger: TutorialCompletionTrigger.ReachHintTarget,
            hintTarget: TutorialHintTarget.LocalConveyor),
        new(TutorialStepId.AcquireStarterEgg, "acquire_starter_egg",
            "Approach the marked egg on the conveyor and hold E to take it. Your first egg is free.",
            "Approach the marked egg on the conveyor and hold the action button. Your first egg is free.",
            priority: 30,
            completionRewardGems: 1,
            prerequisiteStableId: "reach_conveyor",
            activationTrigger: TutorialActivationTrigger.StarterOfferReady,
            startAction: TutorialStartAction.EnsureStarterEggOffer,
            progressType: TutorialProgressType.BooleanFact,
            completionTrigger: TutorialCompletionTrigger.StarterEggAcquired,
            hintTarget: TutorialHintTarget.StarterEggOffer),
        new(TutorialStepId.ReturnHome, "return_home",
            "Bring the egg back to your base. Follow the arrow.",
            "Bring the egg back to your base. Follow the arrow.",
            priority: 40,
            completionRewardGems: 2,
            prerequisiteStableId: "acquire_starter_egg",
            activationTrigger: TutorialActivationTrigger.StarterProgressItemPresent,
            progressType: TutorialProgressType.TargetReached,
            completionTrigger: TutorialCompletionTrigger.ReachHintTarget,
            hintTarget: TutorialHintTarget.LocalHome),
        new(TutorialStepId.PlaceStarterEgg, "place_starter_egg",
            "Stand by the highlighted free cell and hold E to place the egg.",
            "Stand by the highlighted free cell and hold the action button to place the egg.",
            priority: 50,
            completionRewardGems: 2,
            prerequisiteStableId: "return_home",
            activationTrigger: TutorialActivationTrigger.FreeLocalCellReady,
            progressType: TutorialProgressType.BooleanFact,
            completionTrigger: TutorialCompletionTrigger.StarterEggPlaced,
            hintTarget: TutorialHintTarget.FreeLocalCell),
        new(TutorialStepId.HatchStarterEgg, "hatch_starter_egg",
            "Wait for the short timer, then hold E by the egg to hatch it.",
            "Wait for the short timer, then hold the action button by the egg to hatch it.",
            priority: 60,
            completionRewardGems: 2,
            prerequisiteStableId: "place_starter_egg",
            activationTrigger: TutorialActivationTrigger.StarterEggPlaced,
            progressType: TutorialProgressType.BooleanFact,
            completionTrigger: TutorialCompletionTrigger.StarterAnimalHatched,
            hintTarget: TutorialHintTarget.LocalEggCell),
        new(TutorialStepId.MeetStarterAnimal, "meet_starter_animal",
            "Great! Your first animal is guaranteed and already lives in this cell.",
            "Great! Your first animal is guaranteed and already lives in this cell.",
            priority: 70,
            completionRewardGems: 2,
            prerequisiteStableId: "hatch_starter_egg",
            activationTrigger: TutorialActivationTrigger.StarterAnimalPresent,
            progressType: TutorialProgressType.BooleanFact,
            completionTrigger: TutorialCompletionTrigger.StarterAnimalObserved,
            hintTarget: TutorialHintTarget.LocalAnimalCell),
        new(TutorialStepId.WaitForFirstIncome, "wait_first_income",
            "Wait a moment while your animal earns its first coins.",
            "Wait a moment while your animal earns its first coins.",
            priority: 80,
            completionRewardGems: 2,
            prerequisiteStableId: "meet_starter_animal",
            activationTrigger: TutorialActivationTrigger.StarterAnimalPresent,
            progressType: TutorialProgressType.BooleanFact,
            completionTrigger: TutorialCompletionTrigger.FirstIncomeReady,
            hintTarget: TutorialHintTarget.LocalAnimalCell),
        new(TutorialStepId.CollectFirstIncome, "collect_first_income",
            "Walk up to your animal to collect the coins it earned.",
            "Walk up to your animal to collect the coins it earned.",
            priority: 90,
            completionRewardGems: 3,
            prerequisiteStableId: "wait_first_income",
            activationTrigger: TutorialActivationTrigger.CollectibleIncomeReady,
            progressType: TutorialProgressType.GameplaySignal,
            completionTrigger: TutorialCompletionTrigger.FirstIncomeCollected,
            hintTarget: TutorialHintTarget.CollectibleIncomeCell),
        new(TutorialStepId.MakeFirstExpansion, "make_first_expansion",
            "Buy another egg, unlock a cell, or purchase a conveyor upgrade.",
            "Buy another egg, unlock a cell, or purchase a conveyor upgrade.",
            priority: 100,
            completionRewardGems: 3,
            prerequisiteStableId: "collect_first_income",
            activationTrigger: TutorialActivationTrigger.ExpansionTargetReady,
            progressType: TutorialProgressType.GameplaySignal,
            completionTrigger: TutorialCompletionTrigger.FirstExpansionMade,
            hintTarget: TutorialHintTarget.ExpansionTarget),
        new(TutorialStepId.ClaimAlbumReward, "claim_album_reward",
            "Press C to open the album, select your discovery, and claim its first reward.",
            "Open the album, select your discovery, and claim its first reward.",
            priority: 110,
            completionRewardGems: 3,
            prerequisiteStableId: "make_first_expansion",
            activationTrigger: TutorialActivationTrigger.AlbumReady,
            progressType: TutorialProgressType.BooleanFact,
            completionTrigger: TutorialCompletionTrigger.AlbumRewardClaimed,
            hintTarget: TutorialHintTarget.AlbumTarget),
        new(TutorialStepId.ContinueIndependently, "continue_independently",
            "Your zoo is running! Keep collecting coins and work toward the next conveyor level.",
            "Your zoo is running! Keep collecting coins and work toward the next conveyor level.",
            priority: 120,
            completionRewardGems: 3,
            prerequisiteStableId: "claim_album_reward",
            activationTrigger: TutorialActivationTrigger.PrerequisitesTerminal,
            progressType: TutorialProgressType.ManualConfirmation,
            completionTrigger: TutorialCompletionTrigger.ManualConfirmation,
            hintTarget: TutorialHintTarget.LocalConveyor)
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
