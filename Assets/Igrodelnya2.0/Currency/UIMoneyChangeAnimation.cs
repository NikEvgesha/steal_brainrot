using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIMoneyChangeAnimation : MonoBehaviour
{
    private Animator _animator;
    private TMP_Text _text;
    private RectTransform _rectTransform;
    private Coroutine _flyRoutine;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _text = GetComponent<TMP_Text>();
        _rectTransform = transform as RectTransform;
        ApplyTextStyle();
    }

    public void Config(string text, bool isPositive)
    {
        ApplyTextStyle();
        text = isPositive ? "+" + text : text;
        if (_animator != null)
            _animator.SetTrigger(isPositive ? "Add" : "Remove");
        _text.text = text;
    }

    public void ConfigFly(
        string text,
        bool isPositive,
        Vector2 startPosition,
        Vector2 endPosition,
        Color color,
        float duration = 0.95f,
        float startScale = 0.85f,
        float endScale = 0.48f,
        float delay = 0f)
    {
        if (_flyRoutine != null)
            StopCoroutine(_flyRoutine);

        if (_animator != null)
            _animator.enabled = false;

        if (_text == null)
            _text = GetComponent<TMP_Text>();
        if (_rectTransform == null)
            _rectTransform = transform as RectTransform;

        ApplyTextStyle();
        _text.text = isPositive ? "+" + text : text;
        _text.color = color;
        _text.raycastTarget = false;
        _rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        _rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        _rectTransform.pivot = new Vector2(0.5f, 0.5f);
        _rectTransform.sizeDelta = new Vector2(300f, 80f);
        _rectTransform.anchoredPosition = startPosition;
        _rectTransform.localScale = Vector3.one * startScale;

        _flyRoutine = StartCoroutine(FlyRoutine(startPosition, endPosition, color, Mathf.Max(0.1f, duration), startScale, endScale, Mathf.Max(0f, delay)));
    }

    private IEnumerator FlyRoutine(Vector2 startPosition, Vector2 endPosition, Color color, float duration, float startScale, float endScale, float delay)
    {
        if (delay > 0f)
        {
            _text.color = new Color(color.r, color.g, color.b, 0f);
            yield return new WaitForSecondsRealtime(delay);
        }

        float elapsed = 0f;
        Vector2 control = Vector2.Lerp(startPosition, endPosition, 0.35f) + new Vector2(0f, 95f);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float ease = 1f - Mathf.Pow(1f - t, 3f);

            Vector2 a = Vector2.Lerp(startPosition, control, ease);
            Vector2 b = Vector2.Lerp(control, endPosition, ease);
            _rectTransform.anchoredPosition = Vector2.Lerp(a, b, ease);
            _rectTransform.localScale = Vector3.one * Mathf.Lerp(startScale, endScale, t);

            float alpha = t < 0.72f ? 1f : Mathf.Lerp(1f, 0f, (t - 0.72f) / 0.28f);
            _text.color = new Color(color.r, color.g, color.b, alpha);

            yield return null;
        }

        Destroy(gameObject);
    }

    private void ApplyTextStyle()
    {
        if (_text == null)
            return;

        TmpUiTextFactory.ApplyDefaults(_text);
        _text.enableAutoSizing = true;
        _text.fontSizeMin = 9;
        _text.fontSizeMax = 34;
        _text.alignment = TextAlignmentOptions.Center;
        _text.outlineColor = new Color(0f, 0f, 0f, 0.78f);
        _text.outlineWidth = Mathf.Max(_text.outlineWidth, 0.14f);
    }

    private void OnDisable()
    {
        if (_flyRoutine == null)
            Destroy(gameObject);
    }
}
