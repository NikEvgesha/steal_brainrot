using TMPro;
using UnityEngine;

public static class TmpUiTextFactory
{
    public static TextMeshProUGUI Add(GameObject gameObject)
    {
        var text = gameObject.AddComponent<TextMeshProUGUI>();
        ApplyDefaults(text);
        return text;
    }

    public static void ApplyDefaults(TMP_Text text)
    {
        if (text == null)
            return;

        if (text.font == null && TMP_Settings.defaultFontAsset != null)
            text.font = TMP_Settings.defaultFontAsset;

        if (text.fontSharedMaterial == null && text.font != null && text.font.material != null)
            text.fontSharedMaterial = text.font.material;

        text.richText = true;
        text.extraPadding = true;
    }
}
