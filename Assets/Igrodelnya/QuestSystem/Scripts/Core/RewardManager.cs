using UnityEngine;

public class RewardManager : MonoBehaviour
{
    public static RewardManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            //DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    /// <summary>
    /// Вызывается из QuestInstance.CompleteQuest(), когда квест завершён.
    /// Просто передадим сюда структуру RewardData.
    /// </summary>
    public void GiveReward(RewardData rewardData)
    {
        if (rewardData == null) return;

        // Пример:
        if (rewardData.coins > 0)
        {
            
            // Ваш код: добавить монеты
            Debug.Log($"[RewardManager] Игрок получил {rewardData.coins} монет.");
        }

        if (rewardData.experience > 0)
        {
            // Ваш код: добавить опыт
            Debug.Log($"[RewardManager] Игрок получил {rewardData.experience} опыта.");
        }

        if (!string.IsNullOrEmpty(rewardData.itemId))
        {
            // Ваш код: дать предмет по ID
            Debug.Log($"[RewardManager] Игрок получил предмет: {rewardData.itemId}.");
        }

        // И так далее — какие угодно дополнительные награды.
    }
}
