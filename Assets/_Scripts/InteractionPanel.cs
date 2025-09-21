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
    [SerializeField] public UnityEvent InteractionComplete;

    private float _progress;
    private bool _interactionInProgress;
    private bool _interactionHold;

    

    private void Start()
    {
        _hintTouch.SetActive(ControlManager.Instance.UseTouchControl);
        _hintDesctop.SetActive(!ControlManager.Instance.UseTouchControl);
    }

    private void OnDisable()
    {
        StopAllCoroutines();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        _interactionHold = true;
        StartInteraction();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        _interactionHold = false;
    }


    private void Update()
    {
        _interactionHold = PlayerInput.Instance.InteractionHold;
        if (_interactionInProgress) return;

        if (PlayerInput.Instance.Interaction)
        {
            StartInteraction();
        }
    }
    private void StartInteraction()
    {
        _interactionInProgress = true;
        _progress = 0;
        StartCoroutine(InteractionProcess());
    }

    private IEnumerator InteractionProcess()
    {
        while (_interactionHold && _progress < 1f)
        {
            _progress += Time.deltaTime * _speed;
            _fillImg.fillAmount = _progress;
            yield return null;
        }

        if (_progress >= 1f)
        {
            InteractionComplete.Invoke();
        }

        _progress = 0;
        _fillImg.fillAmount = 0;
        _interactionInProgress = false;
    }


    public void SetInfo(string actionText, string price = null)
    {
        _actionText.text = actionText;
        _priceText.gameObject.SetActive(price != null);
        if (price != null)
        {
            _priceText.text = "$" + price;
        }
    }
}
