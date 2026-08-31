using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LoadingProgressBarUI : MonoBehaviour
{
    private static readonly Vector2 LoadingWindowSize = new Vector2(800f, 650f);
    private const float SafeMargin = 32f;

    public static LoadingProgressBarUI Instance { get; private set; }
    [SerializeField] private Image _image;
    [SerializeField] private List<AnimationCurve> _visuals;
    [SerializeField] private TMP_Text _text;
    [SerializeField] private float _progress;
    [SerializeField] private float _firstPartProgress = 0.8f;
    [SerializeField] private float _fakeProgressLimit = 0.92f;
    [SerializeField] private float _fakeProgressDuration = 4f;

    private Coroutine _fakeProgressRoutine;
    private Coroutine _finishProgressRoutine;
    private float _realProgressTarget;
    private RectTransform _windowRect;
    private Vector2 _lastViewportSize = new Vector2(float.NaN, float.NaN);

    private void Awake()
    {
        ApplyResponsiveLayout(true);

        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnEnable()
    {
        ApplyResponsiveLayout(true);
        _realProgressTarget = 0f;
        SetVisualProgress(0f);
        RestartFakeProgress();
    }

    private void OnDisable()
    {
        StopProgressRoutine(ref _fakeProgressRoutine);
        StopProgressRoutine(ref _finishProgressRoutine);
    }

    private void LateUpdate()
    {
        ApplyResponsiveLayout(false);
    }

    public void Progress(float progress)
    {
        _realProgressTarget = Mathf.Lerp(0f, _firstPartProgress, Mathf.Clamp01(progress));

        if (_realProgressTarget > _progress)
            SetVisualProgress(_realProgressTarget);
    }

    public void EndProgress(float time)
    {
        StopProgressRoutine(ref _fakeProgressRoutine);
        StopProgressRoutine(ref _finishProgressRoutine);

        if (time <= 0f)
        {
            SetVisualProgress(1f);
            return;
        }

        _finishProgressRoutine = StartCoroutine(TimerProgress(time));
    }

    private void RestartFakeProgress()
    {
        StopProgressRoutine(ref _fakeProgressRoutine);
        _fakeProgressRoutine = StartCoroutine(FakeProgress());
    }

    private IEnumerator FakeProgress()
    {
        float time = 0f;
        float duration = Mathf.Max(0.1f, _fakeProgressDuration);
        float limit = Mathf.Clamp01(_fakeProgressLimit);

        while (_progress < limit)
        {
            yield return null;

            time += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(time / duration);
            float easedT = 1f - Mathf.Pow(1f - t, 2f);
            float fakeTarget = Mathf.Lerp(0f, limit, easedT);

            SetVisualProgress(Mathf.Max(_progress, fakeTarget, _realProgressTarget));

            if (t >= 1f)
                break;
        }
    }

    private IEnumerator TimerProgress(float finishTime)
    {
        float time = 0f;
        float startProgress = _progress;
        int curveIndex = -1;

        if (_visuals != null && _visuals.Count > 0)
            curveIndex = UnityEngine.Random.Range(0, _visuals.Count);

        while (time <= finishTime)
        {
            yield return null;

            time += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(time / finishTime);
            float curveT = curveIndex >= 0 ? _visuals[curveIndex].Evaluate(t) : Mathf.SmoothStep(0f, 1f, t);

            SetVisualProgress(Mathf.Lerp(startProgress, 1f, curveT));
        }

        SetVisualProgress(1f);
    }

    private void SetVisualProgress(float progress)
    {
        _progress = Mathf.Clamp01(progress);

        if (_image != null)
            _image.fillAmount = _progress;

        if (_text != null)
            _text.text = Mathf.RoundToInt(_progress * 100f) + "%";
    }

    private void StopProgressRoutine(ref Coroutine routine)
    {
        if (routine == null)
            return;

        StopCoroutine(routine);
        routine = null;
    }

    private void ApplyResponsiveLayout(bool force)
    {
        if (_windowRect == null)
        {
            Transform current = transform;
            while (current != null && current.name != "LoadingWindow")
                current = current.parent;
            _windowRect = current as RectTransform;
        }

        if (_windowRect == null)
            return;

        Canvas canvas = _windowRect.GetComponentInParent<Canvas>();
        RectTransform canvasRect = canvas != null ? canvas.transform as RectTransform : null;
        Vector2 viewportSize = canvasRect != null ? canvasRect.rect.size : Vector2.zero;
        if (viewportSize.x <= 1f || viewportSize.y <= 1f)
            return;
        if (!force && (viewportSize - _lastViewportSize).sqrMagnitude < 0.25f)
            return;

        float availableWidth = Mathf.Max(1f, viewportSize.x - SafeMargin * 2f);
        float availableHeight = Mathf.Max(1f, viewportSize.y - SafeMargin * 2f);
        float scale = Mathf.Clamp(
            Mathf.Min(availableWidth / LoadingWindowSize.x, availableHeight / LoadingWindowSize.y),
            0.1f,
            1f);

        _windowRect.anchorMin = _windowRect.anchorMax = new Vector2(0.5f, 0.5f);
        _windowRect.pivot = new Vector2(0.5f, 0.5f);
        _windowRect.anchoredPosition = Vector2.zero;
        _windowRect.sizeDelta = LoadingWindowSize;
        _windowRect.localScale = new Vector3(scale, scale, 1f);

        RectTransform content = _windowRect.Find("Content") as RectTransform;
        RectTransform header = _windowRect.Find("Header") as RectTransform;
        RectTransform tip = _windowRect.Find("Tip") as RectTransform;

        ConfigureRect(header, new Vector2(0f, 0.79f), Vector2.one, Vector2.zero, Vector2.zero);
        ConfigureRect(content, Vector2.zero, new Vector2(1f, 0.79f), Vector2.zero, Vector2.zero);
        ConfigureRect(tip, Vector2.zero, new Vector2(1f, 0.07f), new Vector2(18f, 2f), new Vector2(-18f, -2f));
        ConfigureRect(transform as RectTransform, new Vector2(0.10f, 0.10f), new Vector2(0.90f, 0.24f), Vector2.zero, Vector2.zero);

        if (content != null)
        {
            RectTransform subtitle = content.Find("Subtitle") as RectTransform;
            ConfigureRect(subtitle, new Vector2(0.06f, 0.29f), new Vector2(0.94f, 0.46f), new Vector2(8f, 2f), new Vector2(-8f, -2f));
            ConfigureLoadingText(subtitle != null ? subtitle.GetComponent<TMP_Text>() : null, 18f, 34f);
        }

        RectTransform title = header != null ? header.Find("Title") as RectTransform : null;
        ConfigureLoadingText(title != null ? title.GetComponent<TMP_Text>() : null, 28f, 58f);
        ConfigureLoadingText(tip != null ? tip.GetComponent<TMP_Text>() : null, 16f, 30f);
        ConfigureLoadingText(_text, 18f, 34f);

        _lastViewportSize = viewportSize;
    }

    private static void ConfigureRect(
        RectTransform rect,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        if (rect == null)
            return;

        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        rect.localScale = Vector3.one;
    }

    private static void ConfigureLoadingText(TMP_Text text, float minSize, float maxSize)
    {
        if (text == null)
            return;

        text.enableAutoSizing = true;
        text.fontSizeMin = minSize;
        text.fontSizeMax = maxSize;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.alignment = TextAlignmentOptions.Center;
    }
}
