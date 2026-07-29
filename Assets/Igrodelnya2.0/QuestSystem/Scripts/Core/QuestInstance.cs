using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class QuestInstance : MonoBehaviour
{
    [Header("Ссылка на данные квеста (ScriptableObject)")]
    public QuestDefinition questDefinition;

    // Состояние
    private bool isCompleted = false;    // все условия выполнены, ждем ClaimReward
    private bool isClaimed = false;    // награда уже забрана
    private float _analyticsStartedAt;

    // Все компоненты, реализующие IQuestCondition
    private List<IQuestCondition> conditions = new List<IQuestCondition>();

    // Когда все условия выполнены: UI должен показать кнопку «Забрать»
    public event System.Action OnReadyToClaim;

    // Когда игрок нажал «Забрать»  QuestManager должен удалить сам QuestInstance
    public event System.Action OnQuestClaimed;

    // Когда QuestInstance уничтожен (например, вручную Destroy)
    public event System.Action<QuestInstance> OnDestroyed;

    private void Awake()
    {
        _analyticsStartedAt = Time.realtimeSinceStartup;
        // Собираем все IQuestCondition в этом GameObject и его потомках
        var all = GetComponentsInChildren<MonoBehaviour>();
        foreach (var mb in all)
        {
            if (mb is IQuestCondition cond)
            {
                conditions.Add(cond);
                cond.Initialize();
            }
        }

        // Сразу проверяем: может ли квест уже считаться выполненным?
        // (если условия выполнялись до момента Spawn'а)
        CheckImmediateCompletion();
    }

    private void Update()
    {
        // Если уже выполнено или награда забрана — никаких проверок не делаем
        if (isCompleted || isClaimed)
            return;

        // Проверяем каждое условие
        bool allTrue = true;
        foreach (var cond in conditions)
        {
            if (!cond.IsSatisfied)
            {
                allTrue = false;
                break;
            }
        }

        if (allTrue)
        {
            isCompleted = true;
            // Отписываем все условия
            foreach (var cond in conditions)
                cond.Dispose();

            // Оповещаем UI: «квест готов к получению»
            OnReadyToClaim?.Invoke();
            TrackQuestCompleted();
        }
        else
        {
            // Если условия ещё не все выполнены, можно обновлять прогресс в UI
            float progress = CalculateNormalizedProgress();
            OnProgressChanged?.Invoke(progress);
        }
    }

    /// <summary>
    /// Если все условия были выполнены до того, как этот объект появился,
    /// сразу вызываем OnReadyToClaim().
    /// </summary>
    private void CheckImmediateCompletion()
    {
        if (isCompleted || isClaimed)
            return;

        bool allTrue = true;
        foreach (var cond in conditions)
        {
            if (!cond.IsSatisfied)
            {
                allTrue = false;
                break;
            }
        }

        if (allTrue)
        {
            isCompleted = true;
            foreach (var cond in conditions)
                cond.Dispose();
            OnReadyToClaim?.Invoke();
            TrackQuestCompleted();
        }
    }

    /// <summary>
    /// Для UI: возвращаем текущий прогресс (0..1).
    /// </summary>
    public float GetCurrentProgress()
    {
        return CalculateNormalizedProgress();
    }

    private float CalculateNormalizedProgress()
    {
        if (conditions.Count == 0) return 1f;
        float sum = 0f;
        foreach (var cond in conditions)
            sum += cond.GetProgressNormalized();
        return sum / conditions.Count;
    }

    /// <summary>
    /// Публичный геттер, чтобы QuestManager знал, выполнен ли квест до показа UI.
    /// </summary>
    public bool IsCompleted => isCompleted;

    /// <summary>
    /// Вызывается, когда в UI нажали кнопку «Забрать награду».
    /// </summary>
    public void ClaimReward()
    {
        if (!isCompleted || isClaimed)
            return;

        isClaimed = true;

        // Выдать награду
        RewardManager.Instance.GiveReward(questDefinition.reward);
        // UnityEvent для любых дополнительных действий (открыть дверь и т.п.)
        questDefinition.onQuestCompleted?.Invoke();

        // Оповещаем менеджер: «квест окончательно забран»
        OnQuestClaimed?.Invoke();
        GameAnalytics.TrackCritical(AnalyticsEventNames.QuestRewardClaimed, GameAnalytics.Params(
            "quest_id", QuestId,
            "reward_coins", questDefinition != null && questDefinition.reward != null ? questDefinition.reward.coins : 0,
            "reward_experience", questDefinition != null && questDefinition.reward != null ? questDefinition.reward.experience : 0,
            "reward_item_id", questDefinition != null && questDefinition.reward != null ? questDefinition.reward.itemId : string.Empty,
            "source", "quest_panel",
            "result", "success"),
            QuestId);
    }

    private string QuestId => questDefinition != null
        ? questDefinition.questId.ToString().ToLowerInvariant()
        : gameObject.name;

    private void TrackQuestCompleted()
    {
        GameAnalytics.TrackCritical(AnalyticsEventNames.QuestCompleted, GameAnalytics.Params(
            "quest_id", QuestId,
            "completion_time_sec", Mathf.Max(0f, Time.realtimeSinceStartup - _analyticsStartedAt),
            "condition_count", conditions.Count,
            "source", "quest_conditions",
            "result", "success"),
            QuestId);
    }

    private void OnDestroy()
    {
        // Уведомляем, что QuestInstance удалён
        OnDestroyed?.Invoke(this);
    }

    // Событие, чтобы UI обновлялся при каждом изменении прогресса
    public event System.Action<float> OnProgressChanged;
}
