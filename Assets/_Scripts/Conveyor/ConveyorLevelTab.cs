using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class ConveyorLevelTab : MonoBehaviour
{
    [SerializeField] GameObject _activeIndicator;
    private LocalizedText _localization;
    private Text _name;
    private ConveyorLevel _level;

    [HideInInspector]
    public UnityEvent<ConveyorLevel> OnClick = new();

    public void Init(ConveyorLevel level)
    {
        _name = GetComponentInChildren<Text>();
        _level = level;
        string localizationKey = "Boost/RareType/" + _level.RareType.ToString();
        _localization = _name != null ? _name.GetComponent<LocalizedText>() : null;
        if (_localization != null)
        {
            var manager = LocalizationManager.Instance;
            _localization.Configure(
                _localization.LocalizationData != null ? _localization.LocalizationData : manager != null ? manager.LocalizationData : null,
                localizationKey,
                manager != null ? manager.CurrentLanguage : _localization.CurrentLanguage);
        }
        else if (_name != null)
        {
            _name.text = _level.RareType.ToString();
        }
        //_name.text = LocalizationManager.Instance.LocalizationData.GetTranslation(LocalizationKeyType. + _level.RareType, LocalizationManager.Instance.CurrentLanguage);
        //_name.text = _level.RareType.ToString(); // TODO: Localization
    }

    public void _OnClick()
    {
        OnClick?.Invoke(_level);
    }

    public void SetLvlActive(bool active)
    {
        _activeIndicator.SetActive(active);
    }
}
