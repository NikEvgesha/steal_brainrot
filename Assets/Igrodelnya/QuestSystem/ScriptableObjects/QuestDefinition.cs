using UnityEngine;
using UnityEngine.Events;

[CreateAssetMenu(menuName = "Igrodelnya/QuestSystem/Quest Definition", fileName = "NewQuestDefinition")]
public class QuestDefinition : ScriptableObject
{
    [Header("Уникальный идентификатор квеста")]
    public QuestID questId = QuestID.Undefined;

    [Header("Ключи локализации (только для квестовых текстов)")]
    public QuestKeyTypeTitle titleKey = QuestKeyTypeTitle.None;
    public QuestKeyTypeDescription descriptionKey = QuestKeyTypeDescription.None;

    [Header("Иконка (опционально)")]
    public Sprite icon;

    [Header("Награда")]
    public RewardData reward;

    [Header("Событие при завершении (UnityEvent)")]
    public UnityEvent onQuestCompleted;

    /// <summary>
    /// Получает «название» квеста, подставляя текст через LocalizationManager.
    /// </summary>
    public string Title
    {
        get
        {
            if (titleKey == QuestKeyTypeTitle.None) return string.Empty;
            return LocalizationManager.Instance.LocalizationData.GetTranslation(titleKey.ToString(), LocalizationManager.Instance.CurrentLanguage, LocalizationKeyType.Quest.ToString());;
        }
    }

    /// <summary>
    /// Получает «описание» квеста (через LocalizationManager).
    /// </summary>
    public string Description
    {
        get
        {
            if (descriptionKey == QuestKeyTypeDescription.None) return string.Empty;
            return LocalizationManager.Instance.LocalizationData.GetTranslation(descriptionKey.ToString(), LocalizationManager.Instance.CurrentLanguage, LocalizationKeyType.Quest.ToString()); ;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Предупреждаем, если кто-то случайно оставил Undefined или None
        if (questId == QuestID.Undefined)
        {
            Debug.LogWarning($"QuestDefinition \"{name}\" имеет QuestID.Undefined. " +
                             $"Пожалуйста, установите уникальный QuestID через инспектор.", this);
        }

        if (titleKey == QuestKeyTypeTitle.None)
        {
            Debug.LogWarning($"QuestDefinition \"{name}\" не имеет установленного titleKey.", this);
        }

        if (descriptionKey == QuestKeyTypeDescription.None)
        {
            Debug.LogWarning($"QuestDefinition \"{name}\" не имеет установленного descriptionKey.", this);
        }
    }
#endif
}
