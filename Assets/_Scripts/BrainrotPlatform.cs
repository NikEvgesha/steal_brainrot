using System.Collections;
using UnityEngine;

public class BrainrotPlatform : MonoBehaviour
{
    [SerializeField] private Transform _brainrotPoint;
    [SerializeField] private PushPlatform _incomePlatform;

    private AudioSource _audio;
    private bool _empty = true;
    private bool _brainrotOnPoint = false;
    private Brainrot _brainrot;
    private PlayerBase _playerBase;
    private float _currentIncome; 

    public bool Empty { get { return _empty; } }
    public Brainrot Brainrot { get { return _brainrot; } }
    public Transform BrainrotPoint { get { return _brainrotPoint; } }

    private void Awake()
    {
        _audio = GetComponent<AudioSource>();
    }

    private void OnEnable()
    {
        _incomePlatform.PlayerOnPlatform += onPlayerEnter;
    }

    private void OnDisable()
    {
        _incomePlatform.PlayerOnPlatform -= onPlayerEnter;
    }

    public void SetPlayerBase(PlayerBase playerBase)
    {
        _playerBase = playerBase;
    }

    public void SetBrainrot(Brainrot brainrot)
    {
        _brainrot = brainrot;
        _brainrotOnPoint = _brainrot != null;
        _empty = _brainrot == null;
    } 

    public void OnBrainrotArrival()
    {
        _brainrotOnPoint = true;
        _brainrot.transform.position = _brainrotPoint.position;
        _brainrot.transform.localEulerAngles = transform.localEulerAngles;
        _currentIncome = 0;
        _incomePlatform.SetVisible(true);
        StartCoroutine(ProduceIncome());
        _brainrot.Selled += OnSell;
    }

    private IEnumerator ProduceIncome()
    {
        while (_brainrotOnPoint)
        {
            yield return new WaitForSecondsRealtime(1);
            _currentIncome += (float)(_brainrot.Data.Income * ElementTypeMultiplaer.Init.GetMultiplaer(_brainrot.DinamicData.Type));
            _incomePlatform.SetText(_currentIncome);
        }
    }

    private void onPlayerEnter(bool onPlatform)
    {
        if (_brainrot == null) return;
        if (onPlatform)
        {
            GetIncome();
        }   
        //_brainrot.ShowSellHint(onPlatform);
    }

    private void GetIncome()
    {
        CurrencyManager.Instance.AddCurrency(CurrencyType.Coins, _currentIncome);
        _currentIncome = 0;
        _incomePlatform.SetText(_currentIncome);
        _audio.Play();
    }

    private void OnSell()
    {
        _empty = true;
        StopAllCoroutines();
        GetIncome();
        //CurrencyManager.Instance.AddCurrency(CurrencyType.Coins, _brainrot.Data.SellPrice);
        Destroy(_brainrot.gameObject);
        _audio.Play();
    }
}
