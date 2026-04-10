using UnityEditor.Localization.Editor;
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
        _localization = _name.GetComponent<LocalizedText>();
        _level = level;
        _localization.SelectedKey = "Boost/RareType/" + _level.RareType.ToString();
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