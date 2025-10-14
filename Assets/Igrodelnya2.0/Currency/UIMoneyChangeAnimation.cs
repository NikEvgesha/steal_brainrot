using UnityEngine;
using UnityEngine.UI;

public class UIMoneyChangeAnimation : MonoBehaviour
{
    private Animator _animator;
    Text _text;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _text = GetComponent<Text>();
    }
    public void Config(string text, bool isPositive)
    {
           text = isPositive ? "+" + text : text;
        if (_animator != null)
            _animator.SetTrigger(isPositive ? "Add" : "Remove");
        _text.text = text;

    }

    private void OnDisable()
    {
        Destroy(this.gameObject);
    }
}
