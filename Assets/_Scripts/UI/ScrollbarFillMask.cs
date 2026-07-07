using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public class ScrollbarFillMask : MonoBehaviour
{
    [SerializeField] private Scrollbar scrollbar;
    [SerializeField] private Image maskImage;
    [SerializeField] private bool invert;

    private float _lastValue = float.NaN;

    private void Reset()
    {
        ResolveReferences();
        UpdateFill();
    }

    private void OnEnable()
    {
        ResolveReferences();

        if (scrollbar != null)
            scrollbar.onValueChanged.AddListener(OnValueChanged);

        UpdateFill();
    }

    private void OnDisable()
    {
        if (scrollbar != null)
            scrollbar.onValueChanged.RemoveListener(OnValueChanged);
    }

    private void OnValidate()
    {
        ResolveReferences();
        UpdateFill();
    }

    private void LateUpdate()
    {
        if (scrollbar == null)
            return;

        if (!Mathf.Approximately(_lastValue, scrollbar.value))
            UpdateFill();
    }

    private void OnValueChanged(float value)
    {
        UpdateFill();
    }

    private void ResolveReferences()
    {
        if (scrollbar == null)
            scrollbar = GetComponent<Scrollbar>();

        if (maskImage != null)
            return;

        var mask = transform.Find("Mask");
        if (mask != null)
            maskImage = mask.GetComponent<Image>();
    }

    private void UpdateFill()
    {
        if (scrollbar == null || maskImage == null)
            return;

        float value = Mathf.Clamp01(scrollbar.value);
        if (invert)
            value = 1f - value;

        maskImage.type = Image.Type.Filled;
        maskImage.fillMethod = Image.FillMethod.Horizontal;
        maskImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        maskImage.fillAmount = value;
        _lastValue = scrollbar.value;
    }
}
