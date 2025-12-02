using TMPro;
using UnityEngine;

public sealed class TimePlayedBonusUI : MonoBehaviour
{
    [SerializeField] private TimePlayedBonusModifierMB _modifier;
    [SerializeField] private TMP_Text _text;
    [SerializeField] private string _format = "Time Bonus: {0}";

    private void Awake()
    {
        if (_modifier == null)
            _modifier = FindAnyObjectByType<TimePlayedBonusModifierMB>();

        if (_modifier != null)
            _modifier.Changed += UpdateView;

        UpdateView();
    }

    private void OnDestroy()
    {
        if (_modifier != null)
            _modifier.Changed -= UpdateView;
    }

    private void UpdateView()
    {
        if (_text == null) return;

        if (_modifier == null || !_modifier.IsActive)
        {
            _text.text = string.Format(_format, "OFF");
            return;
        }

        _text.text = string.Format(_format, _modifier.DisplayValue);
    }
}
