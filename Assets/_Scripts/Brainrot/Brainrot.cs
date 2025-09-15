using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using static UnityEditor.Experimental.GraphView.GraphView;

public class Brainrot : MonoBehaviour
{
    [SerializeField] private float _speed;
    [SerializeField] private Transform _modelPoint;
    [SerializeField] private BuyTouchHandler _touchHandler;
    [SerializeField] private Text _interactionText;
    [SerializeField] private Image _buyProgress;

    private BrainrotData _data;
    private Rarity _rarity;
    private BrainrotStatus _status;
    private BrainrotInfoUI _canvas;
    private bool _isMoving;
    private Transform _destinationPoint;
    private GameObject _model;
    private Animator _animatorModel;
    private bool _playerInTrigger;
    private float _progress;
    private bool _buyInProgress;
    private IEnumerator _buyCoroutine;
    private BaseOwner _currentBuyer;
    private BaseOwner _owner;

    public BrainrotStatus Status => _status;
    public BrainrotData Data => _data;
    public Rarity Rarity => _rarity;

    public Action Selled;
    public Action Stealed;

    private void Awake()
    {
        _canvas = GetComponentInChildren<BrainrotInfoUI>();
        _touchHandler.transform.parent.gameObject.SetActive(false);
    }

    public void Init(BrainrotData data, Rarity rarity)
    {
        _data = data;
        _rarity = rarity;
        _status = BrainrotStatus.Conveyer;
        _canvas.SetInfo(data, rarity);
        _model = Instantiate(_data.Model,_modelPoint);
        _animatorModel = _model.GetComponent<Animator>();
        //Set model from _data;
        //Set rarity material;
    }

    public void SetDestination(Transform destination)
    {
        if (destination == null)
            return;

        _destinationPoint = destination;
        StopAllCoroutines();
        StartCoroutine(MoveToDestination());
    }

    public void SetStatus(BrainrotStatus status)
    {
        _status = status;
        switch (status)
        {
            case BrainrotStatus.Conveyer:
                _animatorModel.SetBool("Move", true);
                _interactionText.text = "Buy";
                break;
            case BrainrotStatus.Moving:
                _animatorModel.SetBool("Move", true);
                _interactionText.text = "Steal";
                break;
            case BrainrotStatus.Base:
                _animatorModel.SetBool("Move", false);
                _interactionText.text = "Sell";
                break;
        }
    }

    private IEnumerator MoveToDestination()
    {
        while (transform.position != _destinationPoint.position)
        {
            transform.LookAt(_destinationPoint);
            transform.position = Vector3.MoveTowards(transform.position, _destinationPoint.position, _speed);
            yield return new WaitForFixedUpdate();
        }
        _destinationPoint = null;

        switch (_status)
        {
            case BrainrotStatus.Conveyer:
                Destroy(gameObject);
                break;
            case BrainrotStatus.Moving:
                _owner.Base.onBrainrotArrival(this);
                break;
            default:
                break;
        }
    }


    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            _currentBuyer = other.GetComponent<BaseOwner>();
            _touchHandler.transform.parent.gameObject.SetActive(true);
            _playerInTrigger = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            _currentBuyer = null;
            _touchHandler.transform.parent.gameObject.SetActive(false);
            _playerInTrigger = false;
            if (_buyCoroutine != null)
            {
                StopCoroutine(_buyCoroutine);
                _progress = 0;
                _buyProgress.fillAmount = _progress;
                _buyInProgress = false;
            }
                

        }
    }

    private void Update()
    {
        if (!_playerInTrigger || _buyInProgress) return;

        if (PlayerInput.Instance.Interaction)
        {
            switch (_status)
            {
                case BrainrotStatus.Conveyer:
                    TryBuy();
                    break;
                case BrainrotStatus.Base:
                    TrySell();
                    break;
                case BrainrotStatus.Moving:
                    break;
            }
            
        }
    }

    private void TrySell()
    {
        _buyInProgress = true;
        _progress = 0;
        _buyCoroutine = InteractionProcess(true);
        StartCoroutine(_buyCoroutine);
    }

    private void TryBuy(BaseOwner buyer = null)
    {
        if  (buyer != null)
        {
            /* Покупка ботом */
            _currentBuyer = buyer;
        } else
        {
            /* Покупка игроком */
            bool enoughMoney = CurrencyManager.Instance.CheckEnoughCurrency(CurrencyType.Coins, _data.BuyPrice);
            if (!enoughMoney)
                // Показать подсказку что нет денег;
                return;
        }
        
        if (_currentBuyer.Base.GetEmptyPlatform() != null)
        {
            _buyInProgress = true;
            _progress = 0;
            _buyCoroutine = InteractionProcess(buyer == null);
            StartCoroutine(_buyCoroutine);
        }

        
    }


    private IEnumerator InteractionProcess(bool isPlayerBuying)
    {
        if (!isPlayerBuying)
        {
            _progress = 1f;
        }

        while ((_touchHandler.Hold || PlayerInput.Instance.InteractionHold) && _progress < 1f)
        {
            _progress += Time.deltaTime;
            _buyProgress.fillAmount = _progress;
            yield return null;
        }
        if (_progress >= 1f)
        {            
            switch (_status)
            {
                case BrainrotStatus.Conveyer:
                    if (isPlayerBuying)
                        CurrencyManager.Instance.RemoveCurrency(CurrencyType.Coins, _data.BuyPrice);
                    OnBuySuccess(_currentBuyer);
                    break;
                case BrainrotStatus.Base:
                    OnSellSuccess();
                    break;
                case BrainrotStatus.Moving:
                    break;
            }
            
        }
        _progress = 0;
        _buyProgress.fillAmount = _progress;
        _buyInProgress = false;

    }

    private void OnBuySuccess(BaseOwner owner)
    {
        _owner = owner;
        SetStatus(BrainrotStatus.Moving);
        owner.Base.GetEmptyPlatform().SetBrainrot(this);
        SetDestination(owner.Base.EntryPoint);
    }

    private void OnSellSuccess()
    {
        Selled?.Invoke();
    }

}
