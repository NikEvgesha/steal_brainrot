using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Animator))]
public class HoverUIAnimation : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private Animator _animator;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
    }
    public void OnPointerEnter(PointerEventData data)
    {
        _animator.SetBool("Hover", true);
    }

    public void OnPointerExit(PointerEventData data)
    {
        _animator.SetBool("Hover", false);
    }
}