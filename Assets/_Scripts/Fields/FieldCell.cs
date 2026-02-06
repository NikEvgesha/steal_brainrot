using System;
using UnityEngine;
using UnityEngine.Events;


[Serializable]
public class CellSaveData : ItemSaveData
{
    public long HatchingTimestamp;
    public long IncomeLastTime;
}
public class FieldCell : MonoBehaviour
{

    [SerializeField] private string _id;
    [SerializeField] private GameObject _triggerIndicator;
    [SerializeField] private GameObject _takeButton;
    [SerializeField] private GameObject _dropButton;
    [SerializeField] private GameObject _addSpeedButton;
    [SerializeField] private GameObject _hatchButton;

    [HideInInspector] public UnityEvent PlayerEnter;
    [HideInInspector] public UnityEvent PlayerExit;
    [HideInInspector] public UnityEvent TakeBrainrot;
    [HideInInspector] public UnityEvent SpeedBoost;
    [HideInInspector] public UnityEvent HatchEgg;

    private Item _inField;
    private bool _locked;
    private Egg _currentEgg;
    private Brainrot _currentPet;
    private Coroutine _saveCoroutine;

    private bool _playerOnCell;
    public string Id { get { return _id; } }
    private void Awake()
    {
        // Если id ещё не назначен — генерируем новый
        //if (string.IsNullOrEmpty(_id))
        //{
        //    _id = System.Guid.NewGuid().ToString();    
        //}
    }

    public void _OnPlayerEnter()
    {
        if (_locked) return;
        //TestBackpackBrainrot.Instance.SwichItem.AddListener(CheckPlayer);
        G.QuickAccess.SwitchActiveItem.AddListener(CheckPlayer);
        //TestBackpackBrainrot.Instance.PlaceItem.AddListener(UpdateFieldItem);
        G.QuickAccess.PlaceItem.AddListener(UpdateFieldItem);
        
        _playerOnCell = true;
        CheckPlayer();
        PlayerEnter?.Invoke();
    }
    public void CheckPlayer(InventoryItem item=null)
    {
        _dropButton.SetActive(false);
        _addSpeedButton.SetActive(false);
        _triggerIndicator.SetActive(false);
        _takeButton.SetActive(false);
        _hatchButton.SetActive(false);
        if (!_playerOnCell) return;
        switch (_inField)
        {
            case Item.Free:
                FieldFree();
                break;
            case Item.Egg:
                switch (_currentEgg.Status)
                {
                    case EggStatus.Maturing:
                        _addSpeedButton.SetActive(true);
                        break;
                    case EggStatus.ReadyToHatch:
                        _hatchButton.SetActive(true);
                        break;
                    default:
                        break;
                }
                
                break;
            case Item.Brainrot:
                if (G.QuickAccess.CheckHand() == Item.Hamer)
                {
                    _takeButton.SetActive(true);
                }
                break;
            default:
                break;
        }
    }
    private void FieldFree()
    {
        switch (G.QuickAccess.CheckHand())
        {
            case Item.Egg:
                _triggerIndicator.SetActive(true);
                _dropButton.SetActive(true);
                break;
            case Item.Brainrot:
                _triggerIndicator.SetActive(true);
                _dropButton.SetActive(true);
                break;
            default:
                break;
        }
    }
    public void UpdateFieldItem(Item item)
    {
        _inField = item;
        switch (_inField)
        {
            case Item.Egg:
                _currentEgg = GetComponentInChildren<Egg>();
                _currentPet = null;
                break;
            case Item.Brainrot:
                _currentEgg = null;
                _currentPet = GetComponentInChildren<Brainrot>();
                break;
            default:
                _currentEgg = null;
                _currentPet = null;
                break;
        }
        SaveData();
        BaseDirtyTracker.MarkDirty();

        CheckPlayer();
    }
    public void _OnPlayerExit()
    {
        G.QuickAccess.PlaceItem.RemoveListener(UpdateFieldItem);
        G.QuickAccess.SwitchActiveItem.RemoveListener(CheckPlayer);
        _dropButton.SetActive(false);
        _addSpeedButton.SetActive(false);
        _takeButton.SetActive(false);
        _triggerIndicator.SetActive(false);
        _playerOnCell = false;
        PlayerExit?.Invoke();
    }
    public void _Take()
    {
        TakeBrainrot?.Invoke();
    }
    public void _Drop()
    {
        G.QuickAccess.DropCurrent(this);
        //TestBackpackBrainrot.Instance.Drop(this);
    }
    public void _AddSpeed()
    {
        SpeedBoost?.Invoke();
    }

    public void _Hatch()
    {
        HatchEgg?.Invoke();
    }

    public void LockCell(bool locked)
    {
        _locked = locked;
        //if (_locked)
            _OnPlayerExit();
    }

    public void SaveData()
    {
        CellSaveData data = new CellSaveData();
        data.Status = _inField;
        switch (_inField)
        {
            case Item.Egg:
               data.DinamicData = _currentEgg.Data.DinamicData;
                data.ID = _currentEgg.Name;
                data.HatchingTimestamp = _currentEgg.HatchingTime;
                break;
            case Item.Brainrot:
                data.DinamicData = _currentPet.DinamicData;
                data.ID = _currentPet.Name;
                data.IncomeLastTime = _currentPet.LastIncomeCollectTime;
                break;
            default:
                break;
        }
        G.Save.SaveCellData(_id, data);
    }

    public void SetLoadedData(string id)
    {
        _id = id;
        CellSaveData data = G.Save.LoadCellData(_id);

        if (data == null || data.Status == Item.Free) return;
        switch (data.Status)
        {
            case Item.Egg:
                Egg prefabEgg = G.Storage.GetEgg(data.ID);
                if (prefabEgg != null)
                {
                    Egg egg = Instantiate(prefabEgg, transform);
                    egg.SetData(data.DinamicData);
                    egg.InitTimer(this, DateTimeOffset.FromUnixTimeSeconds(data.HatchingTimestamp));
                }

                break;
            case Item.Brainrot:
                Brainrot prefabPet = G.Storage.GetPet(data.ID);
                if (prefabPet != null)
                {
                    Brainrot pet = Instantiate(prefabPet, transform);
                    pet.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    pet.Init(data.DinamicData, this, data.IncomeLastTime);
                }
                break;
            default:
                break;
        }
        UpdateFieldItem(data.Status);

    }

    public void SetId(string id)
    {
        _id = id;
    }
}
