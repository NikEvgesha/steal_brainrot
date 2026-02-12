using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;


[System.Serializable]
public struct EggData
{
    public int Luck;
    public float Price;
    public int SecondsToHatching;
    public List<Brainrot> Brainrots;
    public BrainrotDinamicData DinamicData;
    //public float Weight;
}
public class Egg : InventoryItem
{
    [SerializeField] private EggData _data;
    [SerializeField] private float _animationHatchingTime;
    [SerializeField] private float _animationHatchingSpeed;
    [SerializeField] private AnimationCurve _animationCurve = new AnimationCurve();
    [SerializeField] private GameObject _mesh;
    [SerializeField] private Transform _rouleteModelsParent;
    [SerializeField] private Material _rouleteMaterial;

    private List<GameObject> _rouleteObjects;
    private EggInfoUI _infoUI;
    private InteractionPanel _buyPanel;
    private EggStatus _status;
    private FieldCell _currentCell;
    //private int _currentHatchingTime;
    private long _hatchingTimectamp;
    private bool _initialized;
    private bool _remoteConveyorPurchase;
    private bool _purchaseInProgress;

    private int _totalDurationSec;      // РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р… РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р… РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…
    private DateTimeOffset _endUtc;           // РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р… РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р… (UTC)
    private Coroutine _ticker;
    private string SaveKey => $"egg_endUtc_{_currentCell.Id}";//РїС—Р…РїС—Р…РїС—Р…РїС—Р… РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р… РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р… РїС—Р… РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р… id РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…/РїС—Р…РїС—Р…РїС—Р…РїС—Р…)

    public long HatchingTime => _hatchingTimectamp;
    public EggStatus Status => _status;
    public EggData Data { get { return _data; } }
    [HideInInspector] public UnityEvent<Egg> EggPurchased;

    private void Awake()
    {
        if (!_initialized)
        {
            Init();
        }
    }
    private void Init()
    {
        _infoUI = GetComponentInChildren<EggInfoUI>();
        _buyPanel = GetComponentInChildren<InteractionPanel>();
        _buyPanel.gameObject.SetActive(false);
        _status = EggStatus.Conveyer;
        _infoUI.SetStatus(_status);
        _rouleteObjects = new List<GameObject>();
        SetRouletteModels();
        _initialized = true;
    }

    public void SetRandomData()
    {
        if (!_initialized)
        {
            Init();
        }
        _data.DinamicData.ElementType = G.Elements.GetRandomWeighted();
        SetTypeVisual();
        _infoUI.SetInfo(this);
    }

    public void SetData(BrainrotDinamicData data)
    {
        if (!_initialized)
        {
            Init();
        }
        _data.DinamicData = data;
        SetTypeVisual();
        _infoUI.SetInfo(this);
    }

    public void SetConveyorPurchaseMode(bool remoteRewardPurchase)
    {
        _remoteConveyorPurchase = remoteRewardPurchase;
    }
    private void SetTypeVisual()
    {
        switch (_data.DinamicData.ElementType)
        {
            case ElementType.Gold:
                _mesh.GetComponent<Renderer>().material.color = Color.yellow;
                break;
            case ElementType.Diamond:
                _mesh.GetComponent<Renderer>().material.color = Color.blue;
                break;
            case ElementType.Electric:
                _mesh.GetComponent<Renderer>().material.color = Color.magenta;
                break;
            case ElementType.Fire:
                _mesh.GetComponent<Renderer>().material.color = Color.red;
                break;
            default:
                break;
        }


    }
    private void OnTriggerEnter(Collider other)
    {
        if (_status != EggStatus.Conveyer) return;
        if (other.CompareTag("Player"))
        {
            _buyPanel.gameObject.SetActive(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (_status != EggStatus.Conveyer) return;
        if (other.CompareTag("Player"))
        {
            _buyPanel.gameObject.SetActive(false);
        }
    }

    public void TryBuy()
    {
        if (_purchaseInProgress) return;

        if (_remoteConveyorPurchase)
        {
            if (G.Ad == null) return;

            _purchaseInProgress = true;
            G.Ad.ShowRewardedAd("ConveyorRemoteEgg", success =>
            {
                _purchaseInProgress = false;
                if (!success) return;
                G.Inventory.Add(this);
                EggPurchased.Invoke(this);
            });
            return;
        }

        if (G.Currency.RemoveCurrency(CurrencyType.Coins, _data.Price * G.Elements.GetMultiplaer(_data.DinamicData.ElementType)))
        {
            G.Inventory.Add(this);
            EggPurchased.Invoke(this);
        }
    }
    public void InitTimer(FieldCell field)
    {
        _status = EggStatus.Maturing;
        _infoUI.SetStatus(_status);
        _currentCell = field;
        _totalDurationSec = Mathf.RoundToInt(
            _data.SecondsToHatching * G.Elements.GetMultiplaer(_data.DinamicData.ElementType)
        );

        // РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р… РїС—Р…РїС—Р… РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р… РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…

        _currentCell.SpeedBoost.AddListener(SpeedBoostInstant);
        //_currentCell.SpeedBoost.AddListener(SpeedBoostAd);     // РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…: -30 РїС—Р…РїС—Р…РїС—Р…
        //_currentCell.InstantHatch.AddListener(SpeedBoostInstant); // РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…: РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…

        /* РїС—Р…РїС—Р…РїС—Р… РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р… РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р… РїС—Р… РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р… РїС—Р…РїС—Р… РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р… РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р… РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…
        if (PlayerPrefs.HasKey(SaveKey))
        {
            long ticks = long.Parse(PlayerPrefs.GetString(SaveKey));
            _endUtc = new DateTime(ticks, DateTimeKind.Utc);
        }
        else
        {
            _endUtc = DateTime.UtcNow.AddSeconds(_totalDurationSec);
            PlayerPrefs.SetString(SaveKey, _endUtc.Ticks.ToString());
            PlayerPrefs.Save();
        }
       */
        _endUtc = DateTimeOffset.UtcNow.AddSeconds(_totalDurationSec); // РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р… РїС—Р…РїС—Р…РїС—Р… РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…
        _hatchingTimectamp = _endUtc.ToUnixTimeSeconds();


        if (_ticker != null) StopCoroutine(_ticker);
        _ticker = StartCoroutine(Ticker());

    }
    /// <summary>
    /// РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р… РїС—Р…РїС—Р…РїС—Р…РїС—Р…: РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р… 30 РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р… РїС—Р…РїС—Р… РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р… РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р….
    /// </summary>
    public void SpeedBoostAd()
    {
        const int minusSeconds = 30 * 60;
        _endUtc = _endUtc.AddSeconds(-minusSeconds);

        // РїС—Р…РїС—Р… РїС—Р…РїС—Р…РїС—Р… РїС—Р…РїС—Р…РїС—Р…РїС—Р… РїС—Р…РїС—Р… РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р… РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р… РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р… РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…
        if (_endUtc < DateTime.UtcNow) _endUtc = DateTime.UtcNow;

        //SaveDeadline();
    }
    /// <summary>
    /// РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р… РїС—Р…РїС—Р…РїС—Р…РїС—Р…: РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р… РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р….
    /// </summary>
    public void SpeedBoostInstant()
    {
        _endUtc = DateTime.UtcNow;
        //SaveDeadline();
    }
    public void SpeedBoost()
    {
        //_currentHatchingTime = 0; //РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р… РїС—Р…РїС—Р… РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р… РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…, РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р… РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р… - РїС—Р…РїС—Р… РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р… - 30 РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…, РїС—Р…РїС—Р… РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р… РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…
    }

    public void InitTimer(FieldCell cell, DateTimeOffset endTime)
    {
        _status = EggStatus.Maturing;
        _infoUI.SetStatus(_status);
        _currentCell = cell;
        _currentCell.SpeedBoost.AddListener(SpeedBoostInstant);

        _totalDurationSec = Mathf.RoundToInt(
            _data.SecondsToHatching * G.Elements.GetMultiplaer(_data.DinamicData.ElementType)
        );
        DateTimeOffset now = DateTimeOffset.UtcNow;
        _endUtc = endTime < now ? now : endTime;
        _hatchingTimectamp = _endUtc.ToUnixTimeSeconds();
        if (_ticker != null) StopCoroutine(_ticker);
        _ticker = StartCoroutine(Ticker());
    }

    private IEnumerator Ticker()
    {
        // РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р… РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р… РїС—Р…РїС—Р…РїС—Р… РїС—Р… 0.2РїС—Р…0.5РїС—Р… РїС—Р…РїС—Р…РїС—Р… РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р… РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…
        var wait = new WaitForSeconds(0.25f);

        while (true)
        {
            double remainingSec = (_endUtc - DateTime.UtcNow).TotalSeconds;

            if (remainingSec <= 0)
            {
                _infoUI.ShowTimeUI(0, 1f); // 100% РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…
                _currentCell.HatchEgg.AddListener(Hatching);
                _status = EggStatus.ReadyToHatch;
                _currentCell.CheckPlayer();
                //Hatching();
                yield break;
            }
            float progress01 = 1f - Mathf.Clamp01((float)(remainingSec / _totalDurationSec));
            _infoUI.ShowTimeUI((int)Math.Ceiling(remainingSec), progress01);

            yield return wait;
        }
    }
    private void SaveDeadline()
    {
        if (_currentCell == null) return;
        PlayerPrefs.SetString(SaveKey, _endUtc.Ticks.ToString());
        PlayerPrefs.Save();
    }

    // РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р… РїС—Р…РїС—Р…РїС—Р… РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…/РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р… (РїС—Р…РїС—Р… РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…, РїС—Р…РїС—Р…РїС—Р…РїС—Р… endUtc РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…)
    private void OnApplicationPause(bool pause)
    {
        if (pause) SaveDeadline();
    }
    private void OnApplicationQuit()
    {
        SaveDeadline();
    }

    private void Hatching()
    {
        _status = EggStatus.Hatching;
        _infoUI.SetStatus(_status);
        _currentCell.CheckPlayer();
        /*
        PlayerPrefs.DeleteKey(SaveKey);
        PlayerPrefs.Save();
        */

        _currentCell.SpeedBoost.RemoveListener(SpeedBoost);
        //_currentCell.SpeedBoost.RemoveListener(SpeedBoostAd);     // РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…: -30 РїС—Р…РїС—Р…РїС—Р…
        //_currentCell.InstantHatch.RemoveListener(SpeedBoostInstant); // РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…: РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…


        _currentCell.LockCell(true);
        StartCoroutine(ShowAnimation());
    }
    private IEnumerator ShowAnimation()
    {
        _mesh.SetActive(false);
        float startTime = _animationHatchingTime;
        //float frameTime = _animationHatchingTime / _animationHatchingSpeed;
        int idx = -1;
        while (_animationHatchingTime > 0) 
        {
            //РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р… РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р… РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р… РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р… РїС—Р…РїС—Р… РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…
            if (idx>=0)
                _rouleteObjects[idx].SetActive(false);
            idx = (idx + 1) % _rouleteObjects.Count;
            _rouleteObjects[idx].SetActive(true);
            _animationHatchingTime -= _animationHatchingSpeed;
            _animationCurve.Evaluate(_animationHatchingTime/ startTime);
            yield return new WaitForSecondsRealtime(_animationHatchingSpeed);
        }
        SpawnBrainrot();
    }
    private void SpawnBrainrot()
    {
        Brainrot brainrotPrefab = GetRandomBrainrot();
        _data.DinamicData.WeightMultiplier = UnityEngine.Random.Range(1, brainrotPrefab.Data.MaxWeightMult);
        Brainrot brainrot = Instantiate(brainrotPrefab, _currentCell.transform);
        brainrot.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        brainrot.Init(_data.DinamicData, _currentCell);
        _currentCell.UpdateFieldItem(Item.Brainrot);
        Destroy(gameObject);
        _currentCell.LockCell(false);
    }
    private Brainrot GetRandomBrainrot()
    {
        int rand = UnityEngine.Random.Range(0, _data.Brainrots.Count);
        return _data.Brainrots[rand];
    }

    public override void OnInventoryAdd() {
        _status = EggStatus.Purchased;
        _infoUI.SetStatus(_status);
        Destroy(_buyPanel.gameObject);
    }


    private void SetRouletteModels()
    {
        foreach (var brainrot in _data.Brainrots)
        {
            GameObject obj = Instantiate(brainrot.Model, _rouleteModelsParent).gameObject;
            Renderer[] renderer = obj.GetComponentsInChildren<Renderer>();
            foreach (Renderer item in renderer)
            {
                item.material = _rouleteMaterial;          
            }
            obj.transform.localRotation = Quaternion.identity;
            _rouleteObjects.Add(obj);
            obj.SetActive(false);
        }
    }

}


