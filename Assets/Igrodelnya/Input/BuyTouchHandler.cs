using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class BuyTouchHandler : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private GameObject _hintDesctop;
    [SerializeField] private GameObject _hintTouch;

    public Action PointerDown;
    public bool Hold { get; private set; }

    private void Start()
    {
        _hintTouch.SetActive(ControlManager.Instance.UseTouchControl);
        _hintDesctop.SetActive(!ControlManager.Instance.UseTouchControl);
    }
    public void OnPointerDown(PointerEventData eventData)
    {
        Hold = true;
        PointerDown?.Invoke();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        Hold = false;
    }
}
