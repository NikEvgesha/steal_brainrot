using System;
using System.Globalization;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class SpecialShopEternalTrackView : MonoBehaviour
{
    private const int VisibleSteps = 5;

    [Header("Visual template")]
    [SerializeField, Tooltip("Edit this prefab to change the visual layout of every Eternal Track reward.")]
    private SpecialShopEternalRewardCellView _cellTemplate;

    private SpecialShop _shop;
    private ShopPackData _pack;
    private Sprite _cellSprite;
    private TMP_FontAsset _font;
    private HorizontalLayoutGroup _layout;
    private bool _claiming;
    private float _nextStateRefreshAt;
    private int _renderedStep = -1;
    private string _renderedUtcDay;

    public void Initialize(
        SpecialShop shop,
        ShopPackData pack,
        Sprite cellSprite,
        TMP_FontAsset font)
    {
        _shop = shop;
        _pack = pack;
        _cellSprite = cellSprite;
        _font = font;
        EnsureLayout();
        Refresh();
    }

    public void Refresh()
    {
        if (_claiming || _pack == null)
            return;

        Clear();
        int current = _shop != null ? _shop.GetEternalPackStep(_pack) : 0;
        _renderedStep = current;
        _renderedUtcDay = DateTimeOffset.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        if (_pack.TrackSteps == null || current >= _pack.TrackSteps.Count)
        {
            CreateCompletedLabel();
            return;
        }

        int end = Mathf.Min(_pack.TrackSteps.Count, current + VisibleSteps);
        for (int i = current; i < end; i++)
            CreateStep(i, _pack.TrackSteps[i], i == current);
    }

    private void Update()
    {
        if (_claiming || _pack == null || _shop == null || Time.unscaledTime < _nextStateRefreshAt)
            return;

        _nextStateRefreshAt = Time.unscaledTime + 1f;
        string currentDay = DateTimeOffset.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        int currentStep = _shop.GetEternalPackStep(_pack);
        if (currentStep != _renderedStep ||
            !string.Equals(currentDay, _renderedUtcDay, StringComparison.Ordinal))
        {
            Refresh();
        }
    }

    private void EnsureLayout()
    {
        _layout = GetComponent<HorizontalLayoutGroup>();
        if (_layout == null)
        {
            _layout = gameObject.AddComponent<HorizontalLayoutGroup>();
            _layout.padding = new RectOffset(6, 6, 4, 4);
            _layout.spacing = 10f;
            _layout.childAlignment = TextAnchor.MiddleCenter;
            _layout.childControlWidth = true;
            _layout.childControlHeight = true;
            _layout.childForceExpandWidth = true;
            _layout.childForceExpandHeight = true;
        }
    }

    private void CreateStep(int stepIndex, ShopTrackStep step, bool current)
    {
        if (_cellTemplate != null)
        {
            SpecialShopEternalRewardCellView cellView = Instantiate(_cellTemplate, transform);
            GameObject templateCell = cellView.gameObject;
            templateCell.name = "Reward_" + stepIndex;
            templateCell.SetActive(true);
            Sprite currencySprite = !step.Free && G.Currency != null
                ? G.Currency.GetCurrencyIcon(step.PriceCurrencyType)
                : null;
            cellView.Configure(
                step,
                current,
                ResolveRewardIcon(step.Reward),
                currencySprite,
                current ? () => StartCoroutine(ClaimRoutine(templateCell)) : null);
            return;
        }

        var cell = new GameObject(
            "Reward_" + stepIndex,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button),
            typeof(LayoutElement),
            typeof(CanvasGroup));
        cell.transform.SetParent(transform, false);

        var image = cell.GetComponent<Image>();
        image.sprite = _cellSprite;
        image.type = _cellSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        image.color = current
            ? step.Free
                ? new Color(0.08f, 0.72f, 0.95f, 1f)
                : new Color(0.54f, 0.16f, 0.86f, 1f)
            : new Color(0.14f, 0.25f, 0.38f, 0.92f);

        var layout = cell.GetComponent<LayoutElement>();
        layout.minWidth = 115f;
        layout.preferredWidth = 150f;
        layout.flexibleWidth = 0f;

        var button = cell.GetComponent<Button>();
        button.targetGraphic = image;
        button.interactable = current;
        if (current)
            button.onClick.AddListener(() => StartCoroutine(ClaimRoutine(cell)));

        CreateIcon(cell.transform, step.Reward);
        CreateAmount(cell.transform, step.Reward);
        CreatePrice(cell.transform, step, current);

        if (!current)
            CreateLock(cell.transform);
    }

    private void CreateIcon(Transform parent, ShopReward reward)
    {
        var iconObject = new GameObject(
            "Icon",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        iconObject.transform.SetParent(parent, false);
        var rect = iconObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.18f, 0.29f);
        rect.anchorMax = new Vector2(0.82f, 0.91f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var image = iconObject.GetComponent<Image>();
        image.sprite = ResolveRewardIcon(reward);
        image.enabled = image.sprite != null;
        image.preserveAspect = true;
        image.raycastTarget = false;
    }

    private void CreateAmount(Transform parent, ShopReward reward)
    {
        int amount = Mathf.Max(1, reward.Amount);
        CreateText(
            parent,
            "Amount",
            "x" + amount,
            new Vector2(0.52f, 0.68f),
            new Vector2(0.98f, 0.98f),
            24f,
            TextAlignmentOptions.TopRight,
            Color.white);
    }

    private void CreatePrice(Transform parent, ShopTrackStep step, bool current)
    {
        string label = step.Free
            ? LocalizationUtils.T("UI/Shop/Free", "FREE")
            : step.Price.ToString();
        Color color = current
            ? step.Free ? new Color(0.92f, 1f, 0.28f, 1f) : Color.white
            : new Color(0.72f, 0.78f, 0.86f, 1f);
        bool hasCurrencyIcon = !step.Free && CreatePriceCurrencyIcon(parent, step.PriceCurrencyType);
        CreateText(
            parent,
            "Price",
            label,
            hasCurrencyIcon ? new Vector2(0.37f, 0.02f) : new Vector2(0.03f, 0.02f),
            new Vector2(0.97f, 0.28f),
            step.Free ? 20f : 27f,
            TextAlignmentOptions.Center,
            color);
    }

    private static bool CreatePriceCurrencyIcon(Transform parent, CurrencyType currencyType)
    {
        Sprite sprite = G.Currency != null ? G.Currency.GetCurrencyIcon(currencyType) : null;
        if (sprite == null)
            return false;

        var iconObject = new GameObject(
            "PriceCurrencyIcon",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        iconObject.transform.SetParent(parent, false);

        var rect = iconObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.13f, 0.045f);
        rect.anchorMax = new Vector2(0.38f, 0.265f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var image = iconObject.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return true;
    }

    private void CreateLock(Transform parent)
    {
        CreateText(
            parent,
            "Lock",
            LocalizationUtils.T("UI/Shop/Next", "NEXT"),
            new Vector2(0.02f, 0.67f),
            new Vector2(0.32f, 0.98f),
            22f,
            TextAlignmentOptions.TopLeft,
            new Color(1f, 1f, 1f, 0.84f));
    }

    private void CreateCompletedLabel()
    {
        CreateText(
            transform,
            "Completed",
            LocalizationUtils.T(
                "UI/Shop/EternalCompleted",
                "All rewards claimed — a new track arrives tomorrow"),
            Vector2.zero,
            Vector2.one,
            29f,
            TextAlignmentOptions.Center,
            Color.white);
    }

    private TextMeshProUGUI CreateText(
        Transform parent,
        string objectName,
        string value,
        Vector2 anchorMin,
        Vector2 anchorMax,
        float size,
        TextAlignmentOptions alignment,
        Color color)
    {
        var textObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI),
            typeof(Outline));
        textObject.transform.SetParent(parent, false);
        var rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = new Vector2(3f, 2f);
        rect.offsetMax = new Vector2(-3f, -2f);

        var text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.font = _font;
        text.fontStyle = FontStyles.Bold;
        text.fontSize = size;
        text.enableAutoSizing = true;
        text.fontSizeMin = Mathf.Max(12f, size * 0.62f);
        text.fontSizeMax = size;
        text.alignment = alignment;
        text.color = color;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;

        var outline = textObject.GetComponent<Outline>();
        outline.effectColor = new Color(0.03f, 0.015f, 0.01f, 0.95f);
        outline.effectDistance = new Vector2(2f, -2f);
        return text;
    }

    private IEnumerator ClaimRoutine(GameObject cell)
    {
        if (_claiming || _shop == null)
            yield break;

        _claiming = true;
        if (!_shop.TryAdvanceEternalPack(_pack))
        {
            _claiming = false;
            yield break;
        }

        var canvasGroup = cell.GetComponent<CanvasGroup>();
        var layout = cell.GetComponent<LayoutElement>();
        float initialMinWidth = layout != null ? layout.minWidth : 0f;
        float initialPreferredWidth = layout != null ? layout.preferredWidth : 0f;
        float elapsed = 0f;
        const float duration = 0.24f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            cell.transform.localScale = Vector3.Lerp(Vector3.one, new Vector3(0.05f, 0.05f, 1f), t);
            canvasGroup.alpha = 1f - t;
            if (layout != null)
            {
                layout.minWidth = Mathf.Lerp(initialMinWidth, 0f, t);
                layout.preferredWidth = Mathf.Lerp(initialPreferredWidth, 0f, t);
            }
            if (transform is RectTransform trackRect)
                LayoutRebuilder.ForceRebuildLayoutImmediate(trackRect);
            yield return null;
        }

        _claiming = false;
        Refresh();
    }

    private void Clear()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject child = transform.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }
    }

    private static Sprite ResolveRewardIcon(ShopReward reward)
    {
        if (reward.Icon != null)
            return reward.Icon;
        if (reward.Type == ShopRewardType.Item && reward.Item != null)
            return reward.Item.Icon;
        return null;
    }
}
