using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InteractionPanel : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private GameObject _hintDesctop;
    [SerializeField] private GameObject _hintTouch;
    [SerializeField] private Image _fillImg;
    [SerializeField] private Text _priceText;
    [SerializeField] private Text _actionText;
    [SerializeField] private float _speed = 1f;
    [SerializeField] private float _inputDropGraceSec = 0.08f;
    [SerializeField] private float _nearCompleteThreshold = 0.99f;
    [SerializeField] public UnityEvent InteractionStarted;
    [SerializeField] public UnityEvent InteractionComplete;

    private float _progress;
    private bool _interactionInProgress;
    private bool _interactionHold;
    private bool _pointerHold;
    private LocalizedText _actionLocalizedText;
    private LocalizedText _priceLocalizedText;

    public bool IsInteracting => _interactionInProgress;

    
    private void Awake()
    {
        InteractionStarted ??= new UnityEvent();
        InteractionComplete ??= new UnityEvent();
        _inputDropGraceSec = Mathf.Max(0f, _inputDropGraceSec);
        _nearCompleteThreshold = Mathf.Clamp(_nearCompleteThreshold, 0.9f, 1f);

        if (_actionText != null)
        {
            _actionLocalizedText = _actionText.GetComponent<LocalizedText>();
            if (_actionLocalizedText != null)
                _actionLocalizedText.enabled = false;
        }

        if (_priceText != null)
        {
            _priceLocalizedText = _priceText.GetComponent<LocalizedText>();
            if (_priceLocalizedText != null)
                _priceLocalizedText.enabled = false;
        }
    }

    private void Start()
    {
        if (_hintTouch != null)
            _hintTouch.SetActive(G.Control.UseTouchControl);
        if (_hintDesctop != null)
            _hintDesctop.SetActive(!G.Control.UseTouchControl);
    }

    private void OnDisable()
    {
        ResetProgress();
        _interactionHold = false;
        _pointerHold = false;
        StopAllCoroutines();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        _pointerHold = true;
        _interactionHold = true;
        StartInteraction();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        _pointerHold = false;
    }


    private void Update()
    {
        _interactionHold = _pointerHold || G.Input.InteractionHold;
        if (_interactionInProgress) return;

        if (G.Input.Interaction)
        {
            StartInteraction();
        }
    }
    private void StartInteraction()
    {
        if (_interactionInProgress)
            return;

        _interactionInProgress = true;
        _progress = 0;
        InteractionStarted?.Invoke();
        StartCoroutine(InteractionProcess());
    }

    private IEnumerator InteractionProcess()
    {
        var completed = false;
        var lostHoldSec = 0f;
        while (_progress < 1f)
        {
            if (!_interactionHold)
            {
                if (_progress >= _nearCompleteThreshold)
                {
                    _progress = 1f;
                    if (_fillImg != null)
                        _fillImg.fillAmount = 1f;
                    InteractionComplete?.Invoke();
                    completed = true;
                    break;
                }

                lostHoldSec += Time.deltaTime;
                if (lostHoldSec > _inputDropGraceSec)
                    break;

                yield return null;
                continue;
            }

            lostHoldSec = 0f;
            _progress += Time.deltaTime * _speed;
            if (_progress > 1f)
                _progress = 1f;

            if (_fillImg != null)
                _fillImg.fillAmount = _progress;

            if (_progress >= 1f)
            {
                InteractionComplete?.Invoke();
                completed = true;
                break;
            }

            yield return null;
        }

        if (!completed && _progress >= 1f)
        {
            InteractionComplete?.Invoke();
        }

        ResetProgress();
    }
    private void ResetProgress()
    {
        _progress = 0;
        if (_fillImg != null)
            _fillImg.fillAmount = 0;
        _interactionInProgress = false;
    }

    public void SetInfo(string actionText, string price = null)
    {
        if (_actionLocalizedText != null && _actionLocalizedText.enabled)
            _actionLocalizedText.enabled = false;
        if (_priceLocalizedText != null && _priceLocalizedText.enabled)
            _priceLocalizedText.enabled = false;

        if (_actionText != null)
            _actionText.text = actionText;

        if (_priceText != null)
        {
            _priceText.gameObject.SetActive(price != null);
            if (price != null)
                _priceText.text = "$" + price;
        }
    }
}
