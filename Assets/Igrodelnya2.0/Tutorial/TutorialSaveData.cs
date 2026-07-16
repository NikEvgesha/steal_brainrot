using System;
using System.Collections.Generic;

public enum TutorialTaskStatus
{
    Unseen = 0,
    Available = 1,
    Active = 2,
    Completed = 3,
    Skipped = 4
}

[Serializable]
public sealed class TutorialTaskSaveData
{
    public string stableId = string.Empty;
    public int definitionRevision = 1;
    public TutorialTaskStatus status = TutorialTaskStatus.Unseen;
    public string progressJson = string.Empty;
    public double progressValue;
    public bool rewardGranted;
    public long startedUnix;
    public long updatedUnix;
    public long completedUnix;

    public bool IsTerminal => status == TutorialTaskStatus.Completed || status == TutorialTaskStatus.Skipped;
}

[Serializable]
public sealed class TutorialSaveData
{
    public const int CurrentVersion = 2;

    public int version = CurrentVersion;

    // V2 state. Catalog order is deliberately not persisted as identity.
    public bool perStepInitialized;
    public string activeStepId = string.Empty;
    public List<TutorialTaskSaveData> taskStates = new();

    // Legacy V1 projection. Keep these fields for save compatibility and one-way migration.
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
            perStepInitialized = false,
            stepId = TutorialStepCatalog.Steps[0].stableId,
            stepIndex = 0
        };
    }

    public bool Normalize(bool legacyTutorialCompleted = false)
    {
        bool changed = false;
        if (taskStates == null)
        {
            taskStates = new List<TutorialTaskSaveData>();
            changed = true;
        }

        if (!perStepInitialized || version < CurrentVersion)
        {
            MigrateLegacy(legacyTutorialCompleted);
            changed = true;
        }

        changed |= RemoveInvalidAndDuplicateStates();
        changed |= SynchronizeCatalog();
        changed |= NormalizeActiveState();

        version = CurrentVersion;
        perStepInitialized = true;
        RefreshLegacyProjection();
        return changed;
    }

    public TutorialTaskSaveData GetTaskState(string stableId)
    {
        if (string.IsNullOrWhiteSpace(stableId) || taskStates == null)
            return null;

        for (int i = 0; i < taskStates.Count; i++)
        {
            TutorialTaskSaveData state = taskStates[i];
            if (state != null && string.Equals(state.stableId, stableId, StringComparison.Ordinal))
                return state;
        }

        return null;
    }

    public TutorialTaskSaveData GetOrCreateTaskState(TutorialStepDefinition definition)
    {
        if (definition == null || string.IsNullOrWhiteSpace(definition.stableId))
            return null;

        TutorialTaskSaveData state = GetTaskState(definition.stableId);
        if (state != null)
        {
            state.definitionRevision = Math.Max(state.definitionRevision, definition.definitionRevision);
            return state;
        }

        state = new TutorialTaskSaveData
        {
            stableId = definition.stableId,
            definitionRevision = definition.definitionRevision
        };
        taskStates.Add(state);
        return state;
    }

    public TutorialTaskSaveData Activate(TutorialStepDefinition definition, long now)
    {
        TutorialTaskSaveData next = GetOrCreateTaskState(definition);
        if (next == null || next.IsTerminal)
            return null;

        for (int i = 0; i < taskStates.Count; i++)
        {
            TutorialTaskSaveData state = taskStates[i];
            if (state != null && state != next && state.status == TutorialTaskStatus.Active)
                state.status = TutorialTaskStatus.Available;
        }

        next.status = TutorialTaskStatus.Active;
        if (next.startedUnix <= 0)
            next.startedUnix = now;
        next.updatedUnix = now;
        activeStepId = next.stableId;
        stepStartedUnix = next.startedUnix;
        RefreshLegacyProjection();
        return next;
    }

    public void SetAvailable(TutorialStepDefinition definition, long now)
    {
        TutorialTaskSaveData state = GetOrCreateTaskState(definition);
        if (state == null || state.IsTerminal || state.status == TutorialTaskStatus.Active)
            return;
        state.status = TutorialTaskStatus.Available;
        state.updatedUnix = now;
    }

    public void SetProgress(string stableId, double value, string json, long now)
    {
        TutorialTaskSaveData state = GetTaskState(stableId);
        if (state == null || state.IsTerminal)
            return;
        state.progressValue = value;
        state.progressJson = json ?? string.Empty;
        state.updatedUnix = now;
    }

    public void MarkTerminal(string stableId, bool wasSkipped, long now)
    {
        TutorialTaskSaveData state = GetTaskState(stableId);
        if (state == null)
            return;

        state.status = wasSkipped ? TutorialTaskStatus.Skipped : TutorialTaskStatus.Completed;
        state.updatedUnix = now;
        state.completedUnix = now;
        if (string.Equals(activeStepId, stableId, StringComparison.Ordinal))
            activeStepId = string.Empty;
        RefreshLegacyProjection();
    }

    public int CountTerminalKnownSteps()
    {
        int count = 0;
        for (int i = 0; i < TutorialStepCatalog.Steps.Length; i++)
        {
            TutorialTaskSaveData state = GetTaskState(TutorialStepCatalog.Steps[i].stableId);
            if (state != null && state.IsTerminal)
                count++;
        }

        return count;
    }

    public bool AreAllKnownStepsTerminal()
    {
        if (TutorialStepCatalog.Steps.Length == 0)
            return true;
        return CountTerminalKnownSteps() == TutorialStepCatalog.Steps.Length;
    }

    private void MigrateLegacy(bool legacyTutorialCompleted)
    {
        int legacyIndex = TutorialStepCatalog.FindIndex(stepId, stepIndex);
        bool legacyDone = legacyTutorialCompleted || completed;
        bool legacySkipped = legacyDone && skipped;

        taskStates.Clear();
        for (int i = 0; i < TutorialStepCatalog.Steps.Length; i++)
        {
            TutorialStepDefinition definition = TutorialStepCatalog.Steps[i];
            var state = new TutorialTaskSaveData
            {
                stableId = definition.stableId,
                definitionRevision = definition.definitionRevision
            };

            if (legacyDone)
            {
                state.status = legacySkipped ? TutorialTaskStatus.Skipped : TutorialTaskStatus.Completed;
                state.completedUnix = completedUnix;
                state.updatedUnix = completedUnix;
            }
            else if (i < legacyIndex)
            {
                state.status = TutorialTaskStatus.Completed;
                state.completedUnix = stepStartedUnix > 0 ? stepStartedUnix : startedUnix;
                state.updatedUnix = state.completedUnix;
            }
            else if (i == legacyIndex)
            {
                state.status = TutorialTaskStatus.Active;
                state.startedUnix = stepStartedUnix > 0 ? stepStartedUnix : startedUnix;
                state.updatedUnix = state.startedUnix;
                activeStepId = state.stableId;
            }

            taskStates.Add(state);
        }

        if (legacyDone)
            activeStepId = string.Empty;
        perStepInitialized = true;
        version = CurrentVersion;
    }

    private bool SynchronizeCatalog()
    {
        bool changed = false;
        for (int i = 0; i < TutorialStepCatalog.Steps.Length; i++)
        {
            TutorialStepDefinition definition = TutorialStepCatalog.Steps[i];
            TutorialTaskSaveData state = GetTaskState(definition.stableId);
            if (state == null)
            {
                taskStates.Add(new TutorialTaskSaveData
                {
                    stableId = definition.stableId,
                    definitionRevision = definition.definitionRevision
                });
                changed = true;
            }
            else if (state.definitionRevision != definition.definitionRevision)
            {
                // Definition changes never replay a terminal task by default.
                state.definitionRevision = definition.definitionRevision;
                changed = true;
            }
        }

        return changed;
    }

    private bool NormalizeActiveState()
    {
        bool changed = false;
        TutorialTaskSaveData selected = GetTaskState(activeStepId);
        if (selected != null && selected.status != TutorialTaskStatus.Active)
            selected = null;

        for (int i = 0; i < taskStates.Count; i++)
        {
            TutorialTaskSaveData state = taskStates[i];
            if (state == null || state.status != TutorialTaskStatus.Active)
                continue;
            if (selected == null)
            {
                selected = state;
                activeStepId = state.stableId;
                changed = true;
            }
            else if (state != selected)
            {
                state.status = TutorialTaskStatus.Available;
                changed = true;
            }
        }

        if (selected == null && !string.IsNullOrEmpty(activeStepId))
        {
            activeStepId = string.Empty;
            changed = true;
        }

        return changed;
    }

    private bool RemoveInvalidAndDuplicateStates()
    {
        bool changed = false;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int i = taskStates.Count - 1; i >= 0; i--)
        {
            TutorialTaskSaveData state = taskStates[i];
            if (state == null || string.IsNullOrWhiteSpace(state.stableId) || !seen.Add(state.stableId))
            {
                taskStates.RemoveAt(i);
                changed = true;
            }
        }

        return changed;
    }

    private void RefreshLegacyProjection()
    {
        TutorialTaskSaveData active = GetTaskState(activeStepId);
        if (active != null && active.status == TutorialTaskStatus.Active)
        {
            stepId = active.stableId;
            stepIndex = TutorialStepCatalog.FindIndex(active.stableId, stepIndex);
            stepStartedUnix = active.startedUnix;
        }
        else if (TutorialStepCatalog.Steps.Length > 0)
        {
            int fallback = TutorialStepCatalog.FindIndex(stepId, stepIndex);
            stepIndex = fallback;
            stepId = TutorialStepCatalog.Steps[fallback].stableId;
        }

        bool anySkipped = false;
        long latestCompletion = 0;
        for (int i = 0; i < TutorialStepCatalog.Steps.Length; i++)
        {
            TutorialTaskSaveData state = GetTaskState(TutorialStepCatalog.Steps[i].stableId);
            if (state == null)
                continue;
            anySkipped |= state.status == TutorialTaskStatus.Skipped;
            latestCompletion = Math.Max(latestCompletion, state.completedUnix);
        }

        completed = AreAllKnownStepsTerminal();
        skipped = completed && anySkipped;
        completedUnix = completed ? latestCompletion : 0;
    }
}
