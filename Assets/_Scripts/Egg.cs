using System.Collections;
using System.Collections.Generic;
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
            Destroy(_infoUI.gameObject);
            Destroy(_buyPanel.gameObject);
        }
        else
        {
            // Show currency shop запихнуть в CheckEnoughCurrency
        }
    }
    public void InitTimer(FieldCell field)
    {
        _currentCell = field;
        _currentHatchingTime = _data.SecondsToHatching;
        _currentCell.SpeedBoost.AddListener(SpeedBoost);
        StartCoroutine(HatchingTimer());
    }
    public void SpeedBoost()
    {
        _currentHatchingTime = 0; //переделать на реальное время, добавить ветвление - за рекламу - 30 минут, за плату сразу
    }
    private IEnumerator HatchingTimer()
    {
        while (_currentHatchingTime > 0)
        {
            yield return new WaitForSecondsRealtime(1);
            _currentHatchingTime--; //переделать на реальное время
        }
        Hatching();
    }
    private void Hatching()
    {
        _currentCell.SpeedBoost.RemoveListener(SpeedBoost);
        StartCoroutine(ShowAnimation());
    }
    private void SpawnBrainrot()
    {
        BrainrotData brainrotData = GetRandomBrainrot();
        _data.DinamicData.WeightMultiplier = Random.Range(1, brainrotData.MaxWeightMult);
        Brainrot brainrot = Instantiate(TestBackpackBrainrot.Instance.BrainrotObj, _currentCell.transform);
        brainrot.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        brainrot.Init(brainrotData, _data.DinamicData, _currentCell);
        _currentCell.UpdateFieldItem(Item.Brainrot);
        Destroy(gameObject);
    }
    private BrainrotData GetRandomBrainrot()
    {
        int rand = Random.Range(0, _data.Brainrots.Count);
        return _data.Brainrots[rand];
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
}
