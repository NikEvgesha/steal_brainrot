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
    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(Instance.gameObject);
    }
    public void Progress(float progress)
    {
        float lerpT = Mathf.Lerp(0, _firstPartProgress, progress);
        _image.fillAmount = lerpT;
        _text.text = (int)(lerpT * 100) + "%";
    }
    public void EndProgress(float time)
    {
        StartCoroutine(TimerProgress(time));
    }
    private IEnumerator TimerProgress(float finishTime)
    {
        float t;
        float time = 0;
        float lerpT;
        int r = UnityEngine.Random.Range(0, _visuals.Count);
        float CurveT;
        while (time <= finishTime)
        {
            yield return null;
            time += Time.deltaTime;
            t = time/finishTime;
            CurveT = _visuals[r].Evaluate(t);
            lerpT = Mathf.Lerp(_firstPartProgress, 1, CurveT);
            _image.fillAmount = lerpT;
            _text.text = (int)(lerpT*100) + "%";
        }
    }
}
