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
    private bool _remoteMode;

    private bool _playerOnCell;
    public string Id { get { return _id; } }
    public Brainrot CurrentBrainrot => _currentPet;
    public bool IsRemoteMode
    {
        get
        {
            if (_remoteMode)
                return true;

            var field = GetComponentInParent<Field>();
            return field != null && field.IsRemoteMode;
        }
    }

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
        if (_locked || IsRemoteMode)
        {
            HideInteractionButtons();
            return;
        }

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
        HideInteractionButtons();
        if (!_playerOnCell || IsRemoteMode) return;
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
                        SetPanelRewardedAdBadge(_addSpeedButton, true);
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
        HideInteractionButtons();
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

    public void SetRemoteMode(bool remote)
    {
        _remoteMode = remote;
        if (_remoteMode)
        {
            _playerOnCell = false;
            HideInteractionButtons();
        }
        else
        {
            if (_playerOnCell)
                CheckPlayer();
        }
    }

    public void SaveData()
    {
        CellSaveData data = new CellSaveData();
        data.Status = _inField;
        switch (_inField)
        {
            case Item.Egg:
                if (_currentEgg == null)
                {
                    Debug.LogWarning($"[FieldCell] Cannot save egg cell '{_id}': current egg is missing.");
                    _inField = Item.Free;
                    data.Status = Item.Free;
                    break;
                }

                data.DinamicData = _currentEgg.Data.DinamicData;
                data.ID = _currentEgg.Name;
                data.HatchingTimestamp = _currentEgg.HatchingTime;
                break;
            case Item.Brainrot:
                if (_currentPet == null)
                {
                    Debug.LogWarning($"[FieldCell] Cannot save brainrot cell '{_id}': current pet is missing.");
                    _inField = Item.Free;
                    data.Status = Item.Free;
                    break;
                }

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
                if (prefabEgg == null)
                {
                    Debug.LogWarning($"[FieldCell] Skipping saved egg '{data.ID}' for cell '{_id}': prefab was not found.");
                    return;
                }

                Egg egg = Instantiate(prefabEgg, transform);
                egg.SetData(data.DinamicData);
                egg.InitTimer(this, DateTimeOffset.FromUnixTimeSeconds(data.HatchingTimestamp));
                break;
            case Item.Brainrot:
                Brainrot prefabPet = G.Storage.GetPet(data.ID);
                if (prefabPet == null)
                {
                    Debug.LogWarning($"[FieldCell] Skipping saved brainrot '{data.ID}' for cell '{_id}': prefab was not found.");
                    return;
                }

                Brainrot pet = Instantiate(prefabPet, transform);
                pet.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                pet.Init(data.DinamicData, this, data.IncomeLastTime);
                break;
            default:
                return;
        }
        UpdateFieldItem(data.Status);

    }

    public void SetId(string id)
    {
        _id = id;
    }

    public void ClearLoadedActors()
    {
        var eggs = GetComponentsInChildren<Egg>(true);
        foreach (var egg in eggs)
        {
            if (egg != null)
            {
                egg.gameObject.SetActive(false);
                Destroy(egg.gameObject);
            }
        }

        var pets = GetComponentsInChildren<Brainrot>(true);
        foreach (var pet in pets)
        {
            if (pet != null)
            {
                pet.gameObject.SetActive(false);
                Destroy(pet.gameObject);
            }
        }

        _currentEgg = null;
        _currentPet = null;
        _inField = Item.Free;
    }

    private void HideInteractionButtons()
    {
        if (_dropButton != null) _dropButton.SetActive(false);
        if (_addSpeedButton != null) _addSpeedButton.SetActive(false);
        if (_triggerIndicator != null) _triggerIndicator.SetActive(false);
        if (_takeButton != null) _takeButton.SetActive(false);
        if (_hatchButton != null) _hatchButton.SetActive(false);
    }

    private static void SetPanelRewardedAdBadge(GameObject buttonRoot, bool visible)
    {
        if (buttonRoot == null)
            return;

        var panel = buttonRoot.GetComponent<InteractionPanel>();
        if (panel == null)
            panel = buttonRoot.GetComponentInChildren<InteractionPanel>(true);

        if (panel != null)
            panel.SetRewardedAdBadgeVisible(visible);
    }
}
