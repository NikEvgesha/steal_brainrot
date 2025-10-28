using System.Collections.Generic;
using UnityEngine;

public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance { get; private set; }

    [Header("Список квестов (префабы) в порядке выполнения")]
    [Tooltip("В Inspector перетащите: Quest_SellGoldBar_Prefab, Quest_BuyCoal_Prefab, Quest_LeaveTown_Prefab, Quest_Kill5AnyMobs_Prefab и т.д.")]
    public List<GameObject> questPrefabs = new List<GameObject>();
    public List<GameObject> tutorialQuestPrefabs = new List<GameObject>();

    [Header("UI (список квестов)")]
    [Tooltip("Panel или пустой контейнер с LayoutGroup — куда мы будем инстанцировать QuestUIItem")]
    public Transform questsUIContainer;
    [Tooltip("Prefab одного QuestUIItem")]
    public GameObject questUIItemPrefab;

    // Все заспавненные QuestInstance (условия у них слушают события сквозь всю игру)
    private List<QuestInstance> allQuests = new List<QuestInstance>();

    private int currentIndex = 0;        // индекс первого невыполненного квеста
    private QuestUIItem currentUIItem;   // UI-элемент для текущего квеста
    private bool isStart;
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
    private void Start()
    {
        currentIndex = G.Save.LoadQuestProgress();
        StartQuest();
    }
    public void StartQuest()
    {
        if (isStart)
            return;
        isStart = true;
        // 1) Спавним сразу все QuestInstance (но UI ещё не создаём)
        for (int i = currentIndex; i < questPrefabs.Count; i++)
        {
            GameObject prefab = questPrefabs[i];
            if (prefab == null)
            {
                Debug.LogError($"[QuestManager] questPrefabs[{i}] == null!");
                allQuests.Add(null);
                continue;
            }

            // Создаём экземпляр квеста
            GameObject obj = Instantiate(prefab, Vector3.zero, Quaternion.identity);
            QuestInstance qi = obj.GetComponent<QuestInstance>();
            if (qi == null)
            {
                Debug.LogError($"[QuestManager] {prefab.name} не содержит QuestInstance!");
                Destroy(obj);
                allQuests.Add(null);
                continue;
            }

            allQuests.Add(qi);

            // Подписываемся на Destroy этого QuestInstance, чтобы очистить список
            qi.OnDestroyed += OnQuestInstanceDestroyed;
        }

        // 2) Показываем первый невыполненный квест в UI
        ShowCurrentQuest();
    }

    /// <summary>
    /// Показывает текущий (allQuests[currentIndex]) квест в UI.
    /// Если квест уже выполнен  сразу покажет кнопку «Забрать», иначе — прогресс-бар.
    /// Если currentIndex вне диапазона  скрывает questsUIContainer (нет активных).
    /// </summary>
    private void ShowCurrentQuest()
    {
        // Сначала уничтожаем предыдущий UI-элемент (если есть)
        if (currentUIItem != null)
        {
            Destroy(currentUIItem.gameObject);
            currentUIItem = null;
        }

        // Проверяем, если список закончился
        if (currentIndex < 0 || currentIndex >= allQuests.Count)
        {
            if (questsUIContainer != null)
                questsUIContainer.gameObject.SetActive(false);
            return;
        }

        // Включаем контейнер UI, т.к. есть квест
        if (questsUIContainer != null)
            questsUIContainer.gameObject.SetActive(true);

        // Берём QuestInstance
        QuestInstance q = allQuests[currentIndex];
        if (q == null)
        {
            Debug.LogError($"[QuestManager] allQuests[{currentIndex}] == null!");
            return;
        }

        // Создаём UI-элемент
        if (questUIItemPrefab != null && questsUIContainer != null)
        {
            GameObject uiGO = Instantiate(questUIItemPrefab, questsUIContainer);
            currentUIItem = uiGO.GetComponent<QuestUIItem>();
            currentUIItem.Bind(q);

            // Подписываемся: когда на этом квесте нажмут «Забрать»
            q.OnQuestClaimed += OnQuestClaimed;
        }
        else
        {
            Debug.LogWarning("[QuestManager] questUIItemPrefab или questsUIContainer == null!");
        }
    }

    /// <summary>
    /// Вызывается, когда игрок нажал «Забрать награду» в текущем квесте.
    /// Удаляем сам QuestInstance и его UI, переходим к следующему квесту.
    /// </summary>
    private void OnQuestClaimed()
    {
        QuestInstance prevQuest = allQuests[currentIndex];
        if (prevQuest != null)
        {
            // Отпишемся от события
            prevQuest.OnQuestClaimed -= OnQuestClaimed;
            // Удалим объект из сцены
            Destroy(prevQuest.gameObject);
            allQuests[currentIndex] = null;
        }

        // Сдвигаем индекс к следующему квесту
        if (G.Game.isEndGame)
            return;

        G.Save.SaveQuestProgress(currentIndex);
        currentIndex++;
        ShowCurrentQuest();
    }

    /// <summary>
    /// Если кто-то в коде Destroy’ит QuestInstance «вне очереди»,
    /// очищаем ссылку в allQuests.
    /// </summary>
    private void OnQuestInstanceDestroyed(QuestInstance qi)
    {
        int idx = allQuests.IndexOf(qi);
        if (idx >= 0)
            allQuests[idx] = null;
    }

    // Эти методы оставлены пустыми, потому что мы больше не используем их
    public void RegisterQuest(QuestInstance quest) { }
    public void UnregisterQuest(QuestInstance quest) { }
}
