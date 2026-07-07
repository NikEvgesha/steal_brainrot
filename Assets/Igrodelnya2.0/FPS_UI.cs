using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Text))]
public class FPS_UI : MonoBehaviour
{
    [SerializeField] private float sampleInterval = 0.5f;
    [SerializeField] private KeyCode toggleKey = KeyCode.F2;
    [SerializeField] private bool showFrameTime = true;
    [SerializeField] private bool showQuality = true;

    private Text _fpsText;
    private float _timeAccumulator;
    private float _fpsAccumulator;
    private int _framesCount;
    private float _minFps = float.MaxValue;
    private float _maxFps;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindAnyObjectByType<FPS_UI>() != null)
            return;

        GameObject canvasObject = new GameObject(
            "FPSOverlayCanvas",
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        DontDestroyOnLoad(canvasObject);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue - 8;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject textObject = new GameObject(
            "FPS",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Text),
            typeof(Outline),
            typeof(FPS_UI));
        textObject.transform.SetParent(canvasObject.transform, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-16f, -48f);
        rect.sizeDelta = new Vector2(300f, 96f);

        Text text = textObject.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 18;
        text.alignment = TextAnchor.UpperRight;
        text.color = Color.white;
        text.raycastTarget = false;
        text.supportRichText = true;

        Outline outline = textObject.GetComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.95f);
        outline.effectDistance = new Vector2(2f, -2f);
    }

    private void Awake()
    {
        _fpsText = GetComponent<Text>();
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
            _fpsText.enabled = !_fpsText.enabled;

        float deltaTime = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
        if (deltaTime > 0.25f)
        {
            ResetSample();
            return;
        }

        float currentFps = 1f / deltaTime;

        _timeAccumulator += deltaTime;
        _fpsAccumulator += currentFps;
        _framesCount++;
        _minFps = Mathf.Min(_minFps, currentFps);
        _maxFps = Mathf.Max(_maxFps, currentFps);

        if (_timeAccumulator < Mathf.Max(0.1f, sampleInterval))
            return;

        float averageFps = _framesCount > 0 ? _fpsAccumulator / _framesCount : 0f;
        float frameMs = averageFps > 0.01f ? 1000f / averageFps : 0f;

        _fpsText.text = BuildText(averageFps, frameMs);

        ResetSample();
    }

    private string BuildText(float averageFps, float frameMs)
    {
        float minFps = _minFps < float.MaxValue ? _minFps : averageFps;
        float maxFps = _maxFps > 0f ? _maxFps : averageFps;
        string text = $"FPS: {averageFps:0}\nmin/max: {minFps:0}/{maxFps:0}";

        if (showFrameTime)
            text += $"\n{frameMs:0.0} ms";

        if (showQuality)
        {
            int qualityLevel = QualitySettings.GetQualityLevel();
            string[] names = QualitySettings.names;
            string qualityName = names != null && qualityLevel >= 0 && qualityLevel < names.Length
                ? names[qualityLevel]
                : qualityLevel.ToString();
            text += $"\nQ: {qualityName}";
        }

        text += $"\n{toggleKey}: hide";
        return text;
    }

    private void ResetSample()
    {
        _timeAccumulator = 0f;
        _fpsAccumulator = 0f;
        _framesCount = 0;
        _minFps = float.MaxValue;
        _maxFps = 0f;
    }
}
