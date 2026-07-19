using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class ConveyorLevelTab : MonoBehaviour
{
    [SerializeField] GameObject _activeIndicator;
    [SerializeField] private Color _inactiveTextColor = Color.white;
    [SerializeField] private Color _activeTextColor = new Color(0.16f, 0.04f, 0.08f, 1f);

    private LocalizedText _localization;
    private TMP_Text _name;
    private ConveyorLevel _level;
    private string _fallbackName;
    private bool _active;

    [HideInInspector]
    public UnityEvent<ConveyorLevel> OnClick = new();

    public void Init(ConveyorLevel level)
    {
        _name = GetComponentInChildren<TMP_Text>(true);
        _level = level;
        _fallbackName = GetFallbackName(_level);

        _localization = _name != null ? _name.GetComponent<LocalizedText>() : null;
        ApplyName();
        ApplyVisualState();
    }

    private void ApplyName()
    {
        if (_name == null)
            return;

        _name.text = _fallbackName;
        _name.transform.SetAsLastSibling();

        if (_localization == null)
            return;

        string localizationKey = GetLocalizationKey(_level);
        if (string.IsNullOrEmpty(localizationKey))
        {
            _localization.enabled = false;
            _name.text = _fallbackName;
            return;
        }

        var manager = LocalizationManager.Instance;
        LocalizationData data = _localization.LocalizationData != null
            ? _localization.LocalizationData
            : manager != null ? manager.LocalizationData : null;

        if (data == null)
        {
            _localization.enabled = false;
            _name.text = _fallbackName;
            return;
        }

        if (!_localization.enabled)
            _localization.enabled = true;

        string language = manager != null && !string.IsNullOrEmpty(manager.CurrentLanguage)
            ? manager.CurrentLanguage
            : _localization.CurrentLanguage;

        _localization.Configure(data, localizationKey, language);
        if (string.IsNullOrWhiteSpace(_name.text) || string.Equals(_name.text, localizationKey, StringComparison.Ordinal))
            _name.text = _fallbackName;
    }

    private void ApplyVisualState()
    {
        if (_activeIndicator != null)
            _activeIndicator.SetActive(_active);

        if (_name != null)
        {
            _name.color = _active ? _activeTextColor : _inactiveTextColor;
            _name.transform.SetAsLastSibling();
        }
    }

    public void _OnClick()
    {
        OnClick?.Invoke(_level);
    }

    public void SetLvlActive(bool active)
    {
        _active = active;
        ApplyVisualState();
    }

    private static string GetFallbackName(ConveyorLevel level)
    {
        if (level == null)
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(level.Name))
            return level.Name.Trim();

        return level.RareType != RareType.RareType ? level.RareType.ToString() : string.Empty;
    }

    public static string GetLocalizedName(ConveyorLevel level)
    {
        var fallbackName = GetFallbackName(level);
        var key = GetLocalizationKey(level);
        return string.IsNullOrEmpty(key)
            ? fallbackName
            : LocalizationUtils.T(key, fallbackName);
    }

    private static string GetLocalizationKey(ConveyorLevel level)
    {
        if (level == null || level.RareType == RareType.RareType)
            return null;

        return "Boost/RareType/" + level.RareType;
    }
}
