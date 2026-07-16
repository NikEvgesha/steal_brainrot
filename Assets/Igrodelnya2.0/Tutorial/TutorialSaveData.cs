using System;

[Serializable]
public sealed class TutorialSaveData
{
    public const int CurrentVersion = 1;

    public int version = CurrentVersion;
    public string stepId = string.Empty;
    public int stepIndex;
    public bool completed;
    public bool skipped;
    public bool starterEggGranted;
    public bool starterAnimalGranted;
    public long startedUnix;
    public long stepStartedUnix;
    public long completedUnix;

    public static TutorialSaveData CreateNew()
    {
        return new TutorialSaveData
        {
            version = CurrentVersion,
            stepId = TutorialStepCatalog.Steps[0].stableId,
            stepIndex = 0
        };
    }

    public void Normalize()
    {
        version = CurrentVersion;
        stepIndex = TutorialStepCatalog.FindIndex(stepId, stepIndex);
        stepId = TutorialStepCatalog.Steps[stepIndex].stableId;
    }
}
