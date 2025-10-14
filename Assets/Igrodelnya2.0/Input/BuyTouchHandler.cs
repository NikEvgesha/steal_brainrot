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
        _hintTouch.SetActive(G.Control.UseTouchControl);
        _hintDesctop.SetActive(!G.Control.UseTouchControl);
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
