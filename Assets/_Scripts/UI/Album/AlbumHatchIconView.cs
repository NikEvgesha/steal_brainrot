using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class AlbumHatchIconView : MonoBehaviour
{
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image iconImage;

    public void SetIcon(Sprite sprite, Color color)
    {
        var target = ResolveIconImage();
        if (target == null)
            return;

        target.sprite = sprite;
        target.color = color;
        target.enabled = sprite != null;
    }

    public void AutoWire()
    {
        if (backgroundImage == null)
            backgroundImage = GetComponent<Image>();

        if (iconImage == null)
        {
            var icon = transform.Find("Icon");
            if (icon != null)
                iconImage = icon.GetComponent<Image>();
        }

        if (iconImage == null)
        {
            var childImages = GetComponentsInChildren<Image>(true);
            for (var i = 0; i < childImages.Length; i++)
            {
                if (childImages[i] != backgroundImage)
                {
                    iconImage = childImages[i];
                    break;
                }
            }
        }

        if (iconImage == null)
            iconImage = backgroundImage;

        if (backgroundImage != null)
            backgroundImage.raycastTarget = false;
        if (iconImage != null)
        {
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;
        }
    }

    private Image ResolveIconImage()
    {
        if (iconImage == null)
            AutoWire();
        return iconImage;
    }

#if UNITY_EDITOR
    private void Reset()
    {
        AutoWire();
    }

    private void OnValidate()
    {
        AutoWire();
    }
#endif
}
