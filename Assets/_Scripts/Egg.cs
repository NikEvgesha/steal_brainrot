using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;


[System.Serializable]
public struct EggData
{
    public string Name;
    public int Luck;
    public RareType RareType;
    public float Price;
    public int SecondsToHatching;
    public List<BrainrotData> Brainrots;
    public BrainrotDinamicData DinamicData;
}
public class Egg : MonoBehaviour
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

    private int _totalDurationSec;      // Полная длительность вылупления
    private DateTime _endUtc;           // Момент окончания (UTC)
    private Coroutine _ticker;
    private string SaveKey => $"egg_endUtc_{_currentCell.Id}";//если таймеров несколько — добавьте id клетки/яйца)

    public EggData Data { get { return _data; } }
    [HideInInspector] public UnityEvent<Egg> EggPurchased;

    private void Awake()
    {
        _data.DinamicData.Type = ElementTypeMultiplaer.Init.GetRandomWeighted();
        _infoUI = GetComponentInChildren<EggInfoUI>();
        _buyPanel = GetComponentInChildren<InteractionPanel>();
        _buyPanel.gameObject.SetActive(false);
        _infoUI.SetInfo(Data);
        _status = EggStatus.Conveyer;
        _infoUI.SetStatus(_status);
        SetTypeVisual();
    }
    private void SetTypeVisual()
    {
        switch (_data.DinamicData.Type)
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
        if (CurrencyManager.Instance.CheckEnoughCurrency(CurrencyType.Coins, _data.Price * ElementTypeMultiplaer.Init.GetMultiplaer(_data.DinamicData.Type)))
        {
            CurrencyManager.Instance.RemoveCurrency(CurrencyType.Coins, _data.Price * ElementTypeMultiplaer.Init.GetMultiplaer(_data.DinamicData.Type)); // запихнуть в CheckEnoughCurrency

            // положить в инвентарь, остальное заглушка

            TestBackpackBrainrot.Instance.TakeEgg(this);
            EggPurchased.Invoke(this);
            _status = EggStatus.Purchased;
            _infoUI.SetStatus(_status);
            //Destroy(_infoUI.gameObject);
            Destroy(_buyPanel.gameObject);
        }
        else
        {
            // Show currency shop запихнуть в CheckEnoughCurrency
        }
    }
    public void InitTimer(FieldCell field)
    {
        _status = EggStatus.Maturing;
        _infoUI.SetStatus(_status);
        _currentCell = field;
        _totalDurationSec = Mathf.RoundToInt(
            _data.SecondsToHatching * ElementTypeMultiplaer.Init.GetMultiplaer(_data.DinamicData.Type)
        );

        // Подписка на бусты скорости

        _currentCell.SpeedBoost.AddListener(SpeedBoostInstant);
        //_currentCell.SpeedBoost.AddListener(SpeedBoostAd);     // пример: -30 мин
        //_currentCell.InstantHatch.AddListener(SpeedBoostInstant); // пример: мгновенно

        /* Тут нужно будет в зависимости от сохранения запускать таймер
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
        _endUtc = DateTime.UtcNow.AddSeconds(_totalDurationSec); // временно без сохранения

        if (_ticker != null) StopCoroutine(_ticker);
        _ticker = StartCoroutine(Ticker());

    }
    /// <summary>
    /// Рекламный буст: минус 30 минут от оставшегося времени.
    /// </summary>
    public void SpeedBoostAd()
    {
        const int minusSeconds = 30 * 60;
        _endUtc = _endUtc.AddSeconds(-minusSeconds);

        // Не даём уйти «в прошлое» дальше текущего момента
        if (_endUtc < DateTime.UtcNow) _endUtc = DateTime.UtcNow;

        //SaveDeadline();
    }
    /// <summary>
    /// Платный буст: мгновенное вылупление.
    /// </summary>
    public void SpeedBoostInstant()
    {
        _endUtc = DateTime.UtcNow;
        //SaveDeadline();
    }
    public void SpeedBoost()
    {
        _currentHatchingTime = 0; //переделать на реальное время, добавить ветвление - за рекламу - 30 минут, за плату сразу
    }

    private IEnumerator Ticker()
    {
        // Можно тиковаться раз в 0.2–0.5с для плавности прогресса
        var wait = new WaitForSeconds(0.25f);

        while (true)
        {
            double remainingSec = (_endUtc - DateTime.UtcNow).TotalSeconds;

            if (remainingSec <= 0)
            {
                _infoUI.ShowTimeUI(0, 1f); // 100% прогресса
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
        PlayerPrefs.SetString(SaveKey, _endUtc.Ticks.ToString());
        PlayerPrefs.Save();
    }

    // Сохраняем при паузе/выходе (на случай, если endUtc изменили)
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
        //_currentCell.SpeedBoost.RemoveListener(SpeedBoostAd);     // пример: -30 мин
        //_currentCell.InstantHatch.RemoveListener(SpeedBoostInstant); // пример: мгновенно



        StartCoroutine(ShowAnimation());
    }
    private IEnumerator ShowAnimation()
    {
        _mesh.SetActive(false);
        float startTime = _animationHatchingTime;
        while (_animationHatchingTime > 0) 
        {
            //Листаем брейнротов Порпобовать барабан из пряток
            _animationHatchingTime -= Time.deltaTime;
            _animationCurve.Evaluate(_animationHatchingTime/ startTime);
            yield return null;
        }
        SpawnBrainrot();
    }
    private void SpawnBrainrot()
    {
        BrainrotData brainrotData = GetRandomBrainrot();
        _data.DinamicData.WeightMultiplier = UnityEngine.Random.Range(1, brainrotData.MaxWeightMult);
        Brainrot brainrot = Instantiate(TestBackpackBrainrot.Instance.BrainrotObj, _currentCell.transform);
        brainrot.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        brainrot.Init(brainrotData, _data.DinamicData, _currentCell);
        _currentCell.UpdateFieldItem(Item.Brainrot);
        Destroy(gameObject);
    }
    private BrainrotData GetRandomBrainrot()
    {
        int rand = UnityEngine.Random.Range(0, _data.Brainrots.Count);
        return _data.Brainrots[rand];
    }
}
