using UnityEngine;

public class QuestUIManager : MonoBehaviour
{
    [Header("—сылки на Canvas-контейнер")]
    public Transform questsUIContainer;    // куда клонировать каждый QuestUIItem
    public GameObject questUIItemPrefab;   // prefab с QuestUIItem

    private void Awake()
    {
        // ѕередаЄм в QuestManager, чтобы он знал куда ставить UI
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.questsUIContainer = questsUIContainer;
            QuestManager.Instance.questUIItemPrefab = questUIItemPrefab;
        }
    }
}
