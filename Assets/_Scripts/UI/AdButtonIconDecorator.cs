using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public static class AdButtonIconDecorator
{
    private const string BadgeName = "AdIconBadge";
    private static Sprite _iconSprite;

    public static void DecorateScene()
    {
        var buttons = Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < buttons.Length; i++)
        {
            if (IsRewardedAdButton(buttons[i]) && buttons[i].transform.Find("AdIcon") == null)
                SetAdIcon(buttons[i], true);
        }

        var interactionPanels = Object.FindObjectsByType<InteractionPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < interactionPanels.Length; i++)
        {
            if (IsRewardedAdInteraction(interactionPanels[i]))
                interactionPanels[i].SetRewardedAdBadgeVisible(true);
        }
    }

    public static void SetAdIcon(Button button, bool visible)
    {
        if (button == null)
            return;

        SetAdIcon(button.transform, visible);
    }

    public static void SetAdIcon(Transform target, bool visible)
    {
        if (target == null)
            return;

        Transform existingAdIcon = target.Find("AdIcon");
        if (existingAdIcon != null)
        {
            existingAdIcon.gameObject.SetActive(visible);
            return;
        }

        Transform badge = target.Find(BadgeName);
        if (badge == null)
        {
            var badgeObject = new GameObject(BadgeName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
            badgeObject.transform.SetParent(target, false);
            badge = badgeObject.transform;
        }

        ConfigureIcon(badge);
        badge.gameObject.SetActive(visible);
    }

    private static bool IsRewardedAdButton(Button button)
    {
        if (button == null)
            return false;

        string path = GetPath(button.transform).ToLowerInvariant();
        if (path.Contains("adbutton") || path.Contains("playbuttonad") || path.Contains("adreward"))
            return true;

        return HasPersistentMethod(button.onClick, "_ShowAd")
            || HasPersistentMethod(button.onClick, "OnRewardButtonCLick")
            || (HasPersistentMethod(button.onClick, "OnPlayButtonClick") && path.Contains("ad"));
    }

    private static bool IsRewardedAdInteraction(InteractionPanel panel)
    {
        if (panel == null)
            return false;

        string path = GetPath(panel.transform).ToLowerInvariant();
        return path.Contains("addspeedinteractioncanvas")
            || HasPersistentMethod(panel.InteractionComplete, "_AddSpeed");
    }

    private static bool HasPersistentMethod(UnityEvent unityEvent, string methodName)
    {
        if (unityEvent == null)
            return false;

        for (int i = 0; i < unityEvent.GetPersistentEventCount(); i++)
        {
            if (unityEvent.GetPersistentMethodName(i) == methodName)
                return true;
        }

        return false;
    }

    private static void ConfigureIcon(Transform iconTransform)
    {
        var iconImage = iconTransform.GetComponent<Image>();
        if (iconImage == null)
            iconImage = iconTransform.gameObject.AddComponent<Image>();

        iconImage.sprite = GetIconSprite();
        iconImage.type = Image.Type.Simple;
        iconImage.preserveAspect = true;
        iconImage.color = Color.white;
        iconImage.raycastTarget = false;

        var rect = iconTransform as RectTransform;
        if (rect != null)
        {
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-7f, -7f);
            rect.sizeDelta = new Vector2(34f, 34f);
            rect.localScale = Vector3.one;
            rect.SetAsLastSibling();
        }

        var layoutElement = iconTransform.GetComponent<LayoutElement>();
        if (layoutElement == null)
            layoutElement = iconTransform.gameObject.AddComponent<LayoutElement>();
        layoutElement.ignoreLayout = true;
    }

    public static Sprite GetIconSprite()
    {
        if (_iconSprite != null)
            return _iconSprite;

        var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Color clear = new Color(1f, 1f, 1f, 0f);
        Color bg = new Color(1f, 0.82f, 0.18f, 1f);
        Color bg2 = new Color(1f, 0.55f, 0.1f, 1f);
        Color dark = new Color(0.16f, 0.12f, 0.08f, 1f);
        Color shine = new Color(1f, 1f, 1f, 0.82f);

        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
            {
                float dx = x - 31.5f;
                float dy = y - 31.5f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);

                if (d > 31f)
                {
                    texture.SetPixel(x, y, clear);
                    continue;
                }

                Color c = Color.Lerp(bg, bg2, Mathf.Clamp01((31.5f - y) / 45f));
                if (d > 27f)
                    c = dark;
                texture.SetPixel(x, y, c);
            }
        }

        DrawTriangle(texture, new Vector2(25, 18), new Vector2(25, 46), new Vector2(48, 32), Color.white);
        DrawRect(texture, 16, 14, 20, 19, shine);
        DrawRect(texture, 12, 21, 17, 25, shine);
        DrawRect(texture, 46, 46, 51, 50, shine);

        texture.Apply();
        texture.hideFlags = HideFlags.DontSave;

        _iconSprite = Sprite.Create(texture, new Rect(0f, 0f, 64f, 64f), new Vector2(0.5f, 0.5f), 64f);
        _iconSprite.hideFlags = HideFlags.DontSave;
        return _iconSprite;
    }

    private static void DrawRect(Texture2D texture, int minX, int minY, int maxX, int maxY, Color color)
    {
        for (int y = Mathf.Max(0, minY); y < Mathf.Min(texture.height, maxY); y++)
        {
            for (int x = Mathf.Max(0, minX); x < Mathf.Min(texture.width, maxX); x++)
                texture.SetPixel(x, y, color);
        }
    }

    private static void DrawTriangle(Texture2D texture, Vector2 a, Vector2 b, Vector2 c, Color color)
    {
        int minX = Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x)));
        int maxX = Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x)));
        int minY = Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y)));
        int maxY = Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y)));

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                if (PointInTriangle(p, a, b, c))
                    texture.SetPixel(x, y, color);
            }
        }
    }

    private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Sign(p, a, b);
        float d2 = Sign(p, b, c);
        float d3 = Sign(p, c, a);

        bool hasNeg = d1 < 0f || d2 < 0f || d3 < 0f;
        bool hasPos = d1 > 0f || d2 > 0f || d3 > 0f;
        return !(hasNeg && hasPos);
    }

    private static float Sign(Vector2 p1, Vector2 p2, Vector2 p3)
    {
        return (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
    }

    private static string GetPath(Transform transform)
    {
        if (transform == null)
            return string.Empty;

        string path = transform.name;
        Transform parent = transform.parent;
        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }

        return path;
    }
}
