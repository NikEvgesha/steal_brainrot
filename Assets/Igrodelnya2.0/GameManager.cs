using System;
using UnityEngine;
public class GameManager : MonoBehaviour
{
    [SerializeField] private PlayerManager _player;
    [SerializeField] private Transform _playerSpawnPoint;
    [SerializeField] private int _reward;
    [SerializeField] private GameObject _startQuest;
    private Teleporter _teleporter;
    public Teleporter Teleporter => _teleporter;
    public PlayerManager Player { get { return _player; } }

    public bool isEndGame = false;

    public Action GameStart;
    public Action<int> GameResume;

    private bool _pause;

    private void Awake()
    {
        if (G.Game == null)
        {
            G.Game = this;
            isEndGame = false;
            DontDestroyOnLoad(gameObject);
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

    public void SetTeleporter(Teleporter teleporter)
    {
        _teleporter = teleporter;
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
        G.Currency.AddCurrency(CurrencyType.Gems, _reward);
    }
}
