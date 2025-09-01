using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
[DefaultExecutionOrder(-1)]
public class GameManager : MonoBehaviour
{
    private static GameManager _instance;
    public static GameManager Instance { get { return _instance; } private set { } }


    [SerializeField] private PlayerManager _player;
    [SerializeField] private Transform _playerSpawnPoint;
    [SerializeField] private int _reward;
    [SerializeField] private GameObject _startQuest;
    public PlayerManager Player { get { return _player; } }

    public bool isEndGame = false;

    public Action GameStart;
    public Action<int> GameResume;

    private bool _pause;

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            isEndGame = false;
            //DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    private void OnDestroy()
    {
        isEndGame = true;
    }
    private void Start()
    {
       
    }

    private void StartQuest()
    {
        if (_startQuest != null)
            QuestManager.Instance.StartQuest();
        else
            Debug.LogWarning("Не найден prefab Quest_Kill5Rats_Prefab");
    }

    public void EndGame(bool lobby,bool withAds = true)
    {

    }


    public void AddReward()
    {
        CurrencyManager.Instance.AddCurrency(CurrencyType.Gems, _reward);
    }
}
