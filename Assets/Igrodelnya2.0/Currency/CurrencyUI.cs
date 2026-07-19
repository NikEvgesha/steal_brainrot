using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CurrencyUI : MonoBehaviour
{
    [SerializeField] private TMP_Text _currencyAmount;
    [SerializeField] private CurrencyType _type;
    [SerializeField] private UIMoneyChangeAnimation _diffObj;
    [SerializeField] private float _flyDuration = 0.95f;
    [SerializeField] private int _minFlyParts = 3;
    [SerializeField] private int _maxFlyParts = 4;

    private double _currentAmount = 0;
    private bool _hasAmount;
    private bool _subscribed;

    private void OnEnable()
    {
        BlockyUITheme.StyleCurrencyBadge(gameObject, _type);

        if (G.Currency != null && !_subscribed)
        {
            G.Currency.CurrencyChanged.AddListener(OnCurrencyChanged);
            _subscribed = true;
        }

        if (G.Currency != null)
            SetAmount(G.Currency.GetBalance(_type), false);
    }

    private void OnDisable()
    {
        if (G.Currency != null && _subscribed)
            G.Currency.CurrencyChanged.RemoveListener(OnCurrencyChanged);
        _subscribed = false;
    }


    private void OnCurrencyChanged(CurrencyType type, double newAmount)
    {
        if (type == _type)
            SetAmount(newAmount, _hasAmount);
    }

    private void SetAmount(double newAmount, bool animateDifference)
    {
        double difference = newAmount - _currentAmount;
        if (animateDifference && difference != 0)
            ShowDifference(difference);

        _hasAmount = true;
        _currentAmount = newAmount;

        if (_currencyAmount != null)
            _currencyAmount.text = G.Currency.ToString(newAmount);
    }

    private void ShowDifference(double diff)
    {
        if (!gameObject.activeInHierarchy || _diffObj == null) return;

        if (diff > 0 && TryShowFlyDifference(diff))
            return;

        UIMoneyChangeAnimation animation = Instantiate(_diffObj, transform);
        animation.Config(G.Currency.ToString(diff), diff > 0);
    }

    private bool TryShowFlyDifference(double diff)
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        RectTransform canvasRect = canvas != null ? canvas.transform as RectTransform : null;
        RectTransform targetRect = _currencyAmount != null ? _currencyAmount.transform as RectTransform : transform as RectTransform;
        if (canvas == null || canvasRect == null || targetRect == null)
            return false;

        Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        Vector2 targetScreen = RectTransformUtility.WorldToScreenPoint(camera, targetRect.position);
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, targetScreen, camera, out var endLocal))
            return false;

        double[] parts = SplitAmount(diff, ResolveFlyPartCount(diff));
        Color color = GetFlyColor();
        Vector2 centerScreen = new Vector2(Screen.width * 0.5f, Screen.height * 0.54f);
        float screenUnit = Mathf.Min(Screen.width, Screen.height);

        for (int i = 0; i < parts.Length; i++)
        {
            Vector2 startScreen = centerScreen + GetBurstOffset(screenUnit, i, parts.Length);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, startScreen, camera, out var startLocal))
                continue;

            UIMoneyChangeAnimation animation = Instantiate(_diffObj, canvasRect);
            animation.transform.SetAsLastSibling();

            float startScale = UnityEngine.Random.Range(0.62f, 0.98f);
            float endScale = startScale * UnityEngine.Random.Range(0.48f, 0.62f);
            float delay = i * 0.035f + UnityEngine.Random.Range(0f, 0.045f);
            float duration = _flyDuration + UnityEngine.Random.Range(-0.08f, 0.14f);
            Vector2 targetOffset = UnityEngine.Random.insideUnitCircle * 14f;
            animation.ConfigFly(G.Currency.ToString(parts[i]), true, startLocal, endLocal + targetOffset, color, duration, startScale, endScale, delay);
        }

        return true;
    }

    private int ResolveFlyPartCount(double diff)
    {
        int minParts = Mathf.Max(1, _minFlyParts);
        int maxParts = Mathf.Max(minParts, _maxFlyParts);
        int amountCap = diff >= maxParts ? maxParts : Mathf.Max(1, Mathf.FloorToInt((float)diff));

        maxParts = Mathf.Min(maxParts, amountCap);
        minParts = Mathf.Min(minParts, maxParts);

        if (diff < minParts)
            return Mathf.Max(1, Mathf.RoundToInt((float)diff));

        return UnityEngine.Random.Range(minParts, maxParts + 1);
    }

    private double[] SplitAmount(double amount, int count)
    {
        count = Mathf.Max(1, count);
        double[] parts = new double[count];

        if (count == 1)
        {
            parts[0] = amount;
            return parts;
        }

        double remaining = amount;
        for (int i = 0; i < count; i++)
        {
            int slotsLeft = count - i;
            if (slotsLeft == 1)
            {
                parts[i] = Math.Max(0d, remaining);
                break;
            }

            double average = remaining / slotsLeft;
            double part = Math.Round(average * UnityEngine.Random.Range(0.72f, 1.28f));
            double maxPart = remaining - (slotsLeft - 1);
            if (maxPart < 1d)
                maxPart = average;

            part = Math.Max(1d, Math.Min(part, maxPart));
            parts[i] = part;
            remaining -= part;
        }

        return parts;
    }

    private static Vector2 GetBurstOffset(float screenUnit, int index, int count)
    {
        float angleStep = Mathf.PI * 2f / Mathf.Max(1, count);
        float angle = angleStep * index + UnityEngine.Random.Range(-0.45f, 0.45f);
        float radius = screenUnit * UnityEngine.Random.Range(0.035f, 0.105f);
        Vector2 radial = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        radial.y += UnityEngine.Random.Range(-screenUnit * 0.025f, screenUnit * 0.055f);
        return radial;
    }

    private Color GetFlyColor()
    {
        switch (_type)
        {
            case CurrencyType.Gems:
                return new Color(0.45f, 0.95f, 1f, 1f);
            case CurrencyType.Real:
                return new Color(1f, 0.72f, 0.28f, 1f);
            default:
                return new Color(1f, 0.9f, 0.18f, 1f);
        }
    }
}
