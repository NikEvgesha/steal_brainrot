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

[Serializable]
public sealed class TutorialStepDefinition
{
    public TutorialStepId id;
    public string stableId;
    public string packId;
    public int definitionRevision;
    public int priority;
    public string[] prerequisiteStableIds;
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
        string prerequisiteStableId = null,
        int definitionRevision = 1,
        string packId = TutorialStepCatalog.CorePackId)
    {
        this.id = id;
        this.stableId = stableId;
        this.packId = packId;
        this.definitionRevision = Math.Max(1, definitionRevision);
        this.priority = priority;
        prerequisiteStableIds = string.IsNullOrWhiteSpace(prerequisiteStableId)
            ? Array.Empty<string>()
            : new[] { prerequisiteStableId };
        desktopTextKey = $"UI/Tutorial/Step/{stableId}/Desktop";
        touchTextKey = $"UI/Tutorial/Step/{stableId}/Touch";
        this.desktopFallback = desktopFallback;
        this.touchFallback = touchFallback;
    }
}

public static class TutorialStepCatalog
{
    public const string CorePackId = "core_v1";

    public static readonly TutorialStepDefinition[] Steps =
    {
        new(TutorialStepId.LearnMovement, "learn_movement",
            "Use WASD to walk a few meters. Hold the right mouse button to look around.",
            "Move the left joystick to walk. Swipe the right side to look around.",
            priority: 0),
        new(TutorialStepId.FindHome, "find_home",
            "Follow the arrow to the large marker above your home.",
            "Follow the arrow to the large marker above your home.",
            priority: 10,
            prerequisiteStableId: "learn_movement"),
        new(TutorialStepId.ReachConveyor, "reach_conveyor",
            "Go to the conveyor on your base.",
            "Go to the conveyor on your base.",
            priority: 20,
            prerequisiteStableId: "find_home"),
        new(TutorialStepId.AcquireStarterEgg, "acquire_starter_egg",
            "Approach the marked egg on the conveyor and hold E to take it. Your first egg is free.",
            "Approach the marked egg on the conveyor and hold the action button. Your first egg is free.",
            priority: 30,
            prerequisiteStableId: "reach_conveyor"),
        new(TutorialStepId.ReturnHome, "return_home",
            "Bring the egg back to your base. Follow the arrow.",
            "Bring the egg back to your base. Follow the arrow.",
            priority: 40,
            prerequisiteStableId: "acquire_starter_egg"),
        new(TutorialStepId.PlaceStarterEgg, "place_starter_egg",
            "Stand by the highlighted free cell and hold E to place the egg.",
            "Stand by the highlighted free cell and hold the action button to place the egg.",
            priority: 50,
            prerequisiteStableId: "return_home"),
        new(TutorialStepId.HatchStarterEgg, "hatch_starter_egg",
            "Wait for the short timer, then hold E by the egg to hatch it.",
            "Wait for the short timer, then hold the action button by the egg to hatch it.",
            priority: 60,
            prerequisiteStableId: "place_starter_egg"),
        new(TutorialStepId.MeetStarterAnimal, "meet_starter_animal",
            "Great! Your first animal is guaranteed and already lives in this cell.",
            "Great! Your first animal is guaranteed and already lives in this cell.",
            priority: 70,
            prerequisiteStableId: "hatch_starter_egg"),
        new(TutorialStepId.WaitForFirstIncome, "wait_first_income",
            "Wait a moment while your animal earns its first coins.",
            "Wait a moment while your animal earns its first coins.",
            priority: 80,
            prerequisiteStableId: "meet_starter_animal"),
        new(TutorialStepId.CollectFirstIncome, "collect_first_income",
            "Walk up to your animal to collect the coins it earned.",
            "Walk up to your animal to collect the coins it earned.",
            priority: 90,
            prerequisiteStableId: "wait_first_income"),
        new(TutorialStepId.MakeFirstExpansion, "make_first_expansion",
            "Buy another egg, unlock a cell, or purchase a conveyor upgrade.",
            "Buy another egg, unlock a cell, or purchase a conveyor upgrade.",
            priority: 100,
            prerequisiteStableId: "collect_first_income"),
        new(TutorialStepId.ClaimAlbumReward, "claim_album_reward",
            "Press C to open the album, select your discovery, and claim its first reward.",
            "Open the album, select your discovery, and claim its first reward.",
            priority: 110,
            prerequisiteStableId: "make_first_expansion"),
        new(TutorialStepId.ContinueIndependently, "continue_independently",
            "Your zoo is running! Keep collecting coins and work toward the next conveyor level.",
            "Your zoo is running! Keep collecting coins and work toward the next conveyor level.",
            priority: 120,
            prerequisiteStableId: "claim_album_reward")
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
