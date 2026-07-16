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
    public string desktopTextKey;
    public string touchTextKey;
    public string desktopFallback;
    public string touchFallback;

    public TutorialStepDefinition(
        TutorialStepId id,
        string stableId,
        string desktopFallback,
        string touchFallback)
    {
        this.id = id;
        this.stableId = stableId;
        desktopTextKey = $"UI/Tutorial/Step/{stableId}/Desktop";
        touchTextKey = $"UI/Tutorial/Step/{stableId}/Touch";
        this.desktopFallback = desktopFallback;
        this.touchFallback = touchFallback;
    }
}

public static class TutorialStepCatalog
{
    public static readonly TutorialStepDefinition[] Steps =
    {
        new(TutorialStepId.LearnMovement, "learn_movement",
            "Use WASD to walk a few meters. Hold the right mouse button to look around.",
            "Move the left joystick to walk. Swipe the right side to look around."),
        new(TutorialStepId.FindHome, "find_home",
            "Follow the arrow to the large marker above your home.",
            "Follow the arrow to the large marker above your home."),
        new(TutorialStepId.ReachConveyor, "reach_conveyor",
            "Go to the conveyor on your base.",
            "Go to the conveyor on your base."),
        new(TutorialStepId.AcquireStarterEgg, "acquire_starter_egg",
            "Approach the marked egg on the conveyor and hold E to take it. Your first egg is free.",
            "Approach the marked egg on the conveyor and hold the action button. Your first egg is free."),
        new(TutorialStepId.ReturnHome, "return_home",
            "Bring the egg back to your base. Follow the arrow.",
            "Bring the egg back to your base. Follow the arrow."),
        new(TutorialStepId.PlaceStarterEgg, "place_starter_egg",
            "Stand by the highlighted free cell and hold E to place the egg.",
            "Stand by the highlighted free cell and hold the action button to place the egg."),
        new(TutorialStepId.HatchStarterEgg, "hatch_starter_egg",
            "Wait for the short timer, then hold E by the egg to hatch it.",
            "Wait for the short timer, then hold the action button by the egg to hatch it."),
        new(TutorialStepId.MeetStarterAnimal, "meet_starter_animal",
            "Great! Your first animal is guaranteed and already lives in this cell.",
            "Great! Your first animal is guaranteed and already lives in this cell."),
        new(TutorialStepId.WaitForFirstIncome, "wait_first_income",
            "Wait a moment while your animal earns its first coins.",
            "Wait a moment while your animal earns its first coins."),
        new(TutorialStepId.CollectFirstIncome, "collect_first_income",
            "Walk up to your animal to collect the coins it earned.",
            "Walk up to your animal to collect the coins it earned."),
        new(TutorialStepId.MakeFirstExpansion, "make_first_expansion",
            "Buy another egg, unlock a cell, or purchase a conveyor upgrade.",
            "Buy another egg, unlock a cell, or purchase a conveyor upgrade."),
        new(TutorialStepId.ClaimAlbumReward, "claim_album_reward",
            "Press C to open the album, select your discovery, and claim its first reward.",
            "Open the album, select your discovery, and claim its first reward."),
        new(TutorialStepId.ContinueIndependently, "continue_independently",
            "Your zoo is running! Keep collecting coins and work toward the next conveyor level.",
            "Your zoo is running! Keep collecting coins and work toward the next conveyor level.")
    };

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
            return 0;
        if (fallbackIndex >= Steps.Length)
            return Steps.Length - 1;
        return fallbackIndex;
    }
}
