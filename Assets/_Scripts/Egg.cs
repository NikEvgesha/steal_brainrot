using System;
using System.Collections;
using System.Collections.Generic;
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
    public float Weight;
}
public class Egg : InventoryItem
{
    [SerializeField] private EggData _data;
    [SerializeField] private float _animationHatchingTime;
    [SerializeField] private AnimationCurve _animationCurve = new AnimationCurve();
    [SerializeField] private GameObject _mesh;
    private EggInfoUI _infoUI;
    private InteractionPanel _buyPanel;
    private EggStatus _status;
    private FieldCell _currentCell;
    private int _currentHatchingTime;

    private int _totalDurationSec;      // ������ ������������ ����������
    private DateTime _endUtc;           // ������ ��������� (UTC)
    private Coroutine _ticker;
    private string SaveKey => $"egg_endUtc_{_currentCell.Id}";//���� �������� ��������� � �������� id ������/����)

    public EggData Data { get { return _data; } }
    [HideInInspector] public UnityEvent<Egg> EggPurchased;

    private void Awake()
    {
        _data.DinamicData.ElementType = G.Elements.GetRandomWeighted();
        _infoUI = GetComponentInChildren<EggInfoUI>();
        _buyPanel = GetComponentInChildren<InteractionPanel>();
        _buyPanel.gameObject.SetActive(false);
        _infoUI.SetInfo(this);
        _status = EggStatus.Conveyer;
        _infoUI.SetStatus(_status);
        SetTypeVisual();
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
        if (G.Currency.CheckEnoughCurrency(CurrencyType.Coins, _data.Price * G.Elements.GetMultiplaer(_data.DinamicData.ElementType)))
        {
            G.Currency.RemoveCurrency(CurrencyType.Coins, _data.Price * G.Elements.GetMultiplaer(_data.DinamicData.ElementType)); // ��������� � CheckEnoughCurrency

            // �������� � ���������, ��������� ��������

            G.Inventory.Add(this);
            //G.QuickAccess.Add(this);

            //TestBackpackBrainrot.Instance.TakeEgg(this);
            EggPurchased.Invoke(this);
            _status = EggStatus.Purchased;
            _infoUI.SetStatus(_status);
            //Destroy(_infoUI.gameObject);
            Destroy(_buyPanel.gameObject);
        }
        else
        {
            // Show currency shop ��������� � CheckEnoughCurrency
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

        // �������� �� ����� ��������

        _currentCell.SpeedBoost.AddListener(SpeedBoostInstant);
        //_currentCell.SpeedBoost.AddListener(SpeedBoostAd);     // ������: -30 ���
        //_currentCell.InstantHatch.AddListener(SpeedBoostInstant); // ������: ���������

        /* ��� ����� ����� � ����������� �� ���������� ��������� ������
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
        _endUtc = DateTime.UtcNow.AddSeconds(_totalDurationSec); // �������� ��� ����������

        if (_ticker != null) StopCoroutine(_ticker);
        _ticker = StartCoroutine(Ticker());

    }
    /// <summary>
    /// ��������� ����: ����� 30 ����� �� ����������� �������.
    /// </summary>
    public void SpeedBoostAd()
    {
        const int minusSeconds = 30 * 60;
        _endUtc = _endUtc.AddSeconds(-minusSeconds);

        // �� ��� ���� �� ������� ������ �������� �������
        if (_endUtc < DateTime.UtcNow) _endUtc = DateTime.UtcNow;

        //SaveDeadline();
    }
    /// <summary>
    /// ������� ����: ���������� ����������.
    /// </summary>
    public void SpeedBoostInstant()
    {
        _endUtc = DateTime.UtcNow;
        //SaveDeadline();
    }
    public void SpeedBoost()
    {
        _currentHatchingTime = 0; //���������� �� �������� �����, �������� ��������� - �� ������� - 30 �����, �� ����� �����
    }

    private IEnumerator Ticker()
    {
        // ����� ���������� ��� � 0.2�0.5� ��� ��������� ���������
        var wait = new WaitForSeconds(0.25f);

        while (true)
        {
            double remainingSec = (_endUtc - DateTime.UtcNow).TotalSeconds;

            if (remainingSec <= 0)
            {
                _infoUI.ShowTimeUI(0, 1f); // 100% ���������
                Hatching();
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

    // ��������� ��� �����/������ (�� ������, ���� endUtc ��������)
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

        /*
        PlayerPrefs.DeleteKey(SaveKey);
        PlayerPrefs.Save();
        */

        _currentCell.SpeedBoost.RemoveListener(SpeedBoost);
        //_currentCell.SpeedBoost.RemoveListener(SpeedBoostAd);     // ������: -30 ���
        //_currentCell.InstantHatch.RemoveListener(SpeedBoostInstant); // ������: ���������



        StartCoroutine(ShowAnimation());
    }
    private IEnumerator ShowAnimation()
    {
        _mesh.SetActive(false);
        float startTime = _animationHatchingTime;
        while (_animationHatchingTime > 0) 
        {
            //������� ���������� ����������� ������� �� ������
            _animationHatchingTime -= Time.deltaTime;
            _animationCurve.Evaluate(_animationHatchingTime/ startTime);
            yield return null;
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
    }
    private Brainrot GetRandomBrainrot()
    {
        int rand = UnityEngine.Random.Range(0, _data.Brainrots.Count);
        return _data.Brainrots[rand];
    }
}
