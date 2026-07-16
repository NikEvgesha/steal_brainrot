using TMPro;
using UnityEngine;

public sealed class SpecialShopSectionHeader : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _title;

    public RectTransform RectTransform => transform as RectTransform;

    public void Init(string localizationKey, string fallback)
    {
        if (_title != null)
            _title.text = LocalizationUtils.T(localizationKey, fallback);
    }
}
