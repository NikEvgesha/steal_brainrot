using UnityEngine;
using System.Collections;

public class Fade : MonoBehaviour
{
    public static Fade Instance { get; private set; }

    [SerializeField] private CanvasGroup _fadeCanvas;
    [SerializeField] private float _fadeDuration = 1f;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public IEnumerator FadeIn()
    {
        float elapsedTime = 0f;
        while (elapsedTime < _fadeDuration)
        {
            _fadeCanvas.alpha = Mathf.Lerp(0, 1, elapsedTime / _fadeDuration);
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        _fadeCanvas.alpha = 1;
    }

    public IEnumerator FadeOut()
    {
        float elapsedTime = 0f;
        while (elapsedTime < _fadeDuration)
        {
            _fadeCanvas.alpha = Mathf.Lerp(1, 0, elapsedTime / _fadeDuration);
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        _fadeCanvas.alpha = 0;
    }

    public void StartFadeIn(System.Action onComplete = null)
    {
        StartCoroutine(FadeRoutine(true, onComplete));
    }

    public void StartFadeOut(System.Action onComplete = null)
    {
        StartCoroutine(FadeRoutine(false, onComplete));
    }

    private IEnumerator FadeRoutine(bool fadeIn, System.Action onComplete)
    {
        yield return fadeIn ? StartCoroutine(FadeIn()) : StartCoroutine(FadeOut());
        onComplete?.Invoke();
    }
}
