using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class LoadingProgressBarUI : MonoBehaviour
{
    public static LoadingProgressBarUI Instance { get; private set; }
    [SerializeField] private Image _image;
    [SerializeField] private List<AnimationCurve> _visuals;
    [SerializeField] private Text _text;
    [SerializeField] private float _progress;
    [SerializeField] private float _firstPartProgress = 0.8f;
    [SerializeField] private float _fakeProgressLimit = 0.92f;
    [SerializeField] private float _fakeProgressDuration = 4f;

    private Coroutine _fakeProgressRoutine;
    private Coroutine _finishProgressRoutine;
    private float _realProgressTarget;

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

    private void OnEnable()
    {
        _realProgressTarget = 0f;
        SetVisualProgress(0f);
        RestartFakeProgress();
    }

    private void OnDisable()
    {
        StopProgressRoutine(ref _fakeProgressRoutine);
        StopProgressRoutine(ref _finishProgressRoutine);
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
}
