using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InteractionPanel : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    private const string RewardedAdBadgeName = "AdIconBadge";
    private const string RewardedAdBadgeLabelName = "AdIconBadgeLabel";

    [SerializeField] private GameObject _hintDesctop;
    [SerializeField] private GameObject _hintTouch;
    [SerializeField] private Image _fillImg;
    [SerializeField] private TMP_Text _priceText;
    [SerializeField] private TMP_Text _actionText;
    [SerializeField] private float _speed = 1f;
    [SerializeField] private float _inputDropGraceSec = 0.08f;
    [SerializeField] private float _nearCompleteThreshold = 0.99f;
    [SerializeField] private float _resumeAfterDisableWindowSec = 0.35f;
    [Header("Rewarded Ad Badge")]
    [SerializeField] private bool _showRewardedAdBadge;
    [SerializeField] private Sprite _rewardedAdBadgeSprite;
    [SerializeField] private string _rewardedAdBadgeLabel;
    [SerializeField] private Vector2 _rewardedAdBadgeSize = new Vector2(36f, 36f);
    [SerializeField] private Vector2 _rewardedAdBadgeOffset = new Vector2(-7f, -7f);
    [SerializeField] private Vector2 _rewardedAdBadgeLabelSize = new Vector2(72f, 24f);
    [SerializeField] private Vector2 _rewardedAdBadgeLabelOffset = new Vector2(0f, -31f);
    [SerializeField] private int _rewardedAdBadgeLabelFontSize = 18;
    [SerializeField] private Color _rewardedAdBadgeLabelColor = Color.white;
    [SerializeField] private Color _rewardedAdBadgeLabelOutlineColor = new Color(0f, 0f, 0f, 0.82f);
    [SerializeField] public UnityEvent InteractionStarted;
    [SerializeField] public UnityEvent InteractionComplete;

    private float _progress;
    private bool _interactionInProgress;
    private bool _interactionHold;
    private bool _pointerHold;
    private LocalizedText _actionLocalizedText;
    private LocalizedText _priceLocalizedText;
    private LocalizationManager _localizationManager;
    private string _actionLocalizationKey;
    private string _actionLocalizationFallback;
    private bool _hasResumeProgress;
    private float _resumeProgress;
    private float _resumeUntilUnscaledTime;
    private bool _awaitReleaseAfterComplete;
    private Image _rewardedAdBadgeImage;
    private TMP_Text _rewardedAdBadgeLabelText;
    private Transform _rewardedAdBadgeParent;
    private AudioSource _interactionLoopSource;

    public bool IsInteracting => _interactionInProgress;

    
    private void Awake()
    {
        InteractionStarted ??= new UnityEvent();
        InteractionComplete ??= new UnityEvent();
        _inputDropGraceSec = Mathf.Max(0f, _inputDropGraceSec);
        _nearCompleteThreshold = Mathf.Clamp(_nearCompleteThreshold, 0.9f, 1f);

        if (_actionText != null)
        {
            _actionLocalizedText = _actionText.GetComponent<LocalizedText>();
            if (_actionLocalizedText != null)
            {
                _actionLocalizationKey = _actionLocalizedText.SelectedKey;
                _actionLocalizationFallback = _actionText.text;
                _actionLocalizedText.enabled = false;
            }
        }

        if (_priceText != null)
        {
            _priceLocalizedText = _priceText.GetComponent<LocalizedText>();
            if (_priceLocalizedText != null)
                _priceLocalizedText.enabled = false;
        }

        ApplyRewardedAdBadgeState();
    }

    private void Start()
    {
        if (_hintTouch != null)
            _hintTouch.SetActive(G.Control.UseTouchControl);
        if (_hintDesctop != null)
            _hintDesctop.SetActive(!G.Control.UseTouchControl);
        ApplyRewardedAdBadgeState();
    }

    private void OnEnable()
    {
        LocalizationManager.OnInstanceReady += HandleLocalizationManagerReady;
        LocalizationUtils.OnFallbackLanguageChanged += HandleLanguageChanged;
        SubscribeToLocalizationManager(LocalizationManager.Instance);
        RefreshLocalizedAction();
        ApplyRewardedAdBadgeState();

        if (_hasResumeProgress &&
            Time.unscaledTime <= _resumeUntilUnscaledTime &&
            (IsHoldPressed() || _pointerHold))
        {
            _progress = Mathf.Clamp01(_resumeProgress);
            if (_fillImg != null)
                _fillImg.fillAmount = _progress;

            _interactionHold = true;
            _interactionInProgress = true;
            _hasResumeProgress = false;
            _interactionLoopSource = G.Sound?.PlayLoop(GameAudioId.SFX_INTERACT_LOOP);
            StartCoroutine(InteractionProcess());
            return;
        }

        _hasResumeProgress = false;
        ResetProgress();
    }

    private void OnDisable()
    {
        LocalizationManager.OnInstanceReady -= HandleLocalizationManagerReady;
        LocalizationUtils.OnFallbackLanguageChanged -= HandleLanguageChanged;
        UnsubscribeFromLocalizationManager();

        bool wasInterrupted = _interactionInProgress && _progress > 0f && _progress < _nearCompleteThreshold;
        var canResumeAfterDisable =
            _interactionInProgress &&
            _progress > 0f &&
            _progress < _nearCompleteThreshold;

        if (canResumeAfterDisable)
        {
            _hasResumeProgress = true;
            _resumeProgress = _progress;
            _resumeUntilUnscaledTime = Time.unscaledTime + Mathf.Max(0f, _resumeAfterDisableWindowSec);
        }
        else
        {
            _hasResumeProgress = false;
            ResetProgress();
        }

        _interactionInProgress = false;
        _interactionHold = false;
        _pointerHold = false;
        StopAllCoroutines();
        StopInteractionAudio(wasInterrupted && !canResumeAfterDisable);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        _pointerHold = true;
        _interactionHold = true;
        StartInteraction();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        _pointerHold = false;
    }


    private void Update()
    {
        var holdPressed = _pointerHold || IsHoldPressed();
        var interactPressed = IsInteractTriggered();

        if (_awaitReleaseAfterComplete)
        {
            if (!holdPressed && !interactPressed)
                _awaitReleaseAfterComplete = false;
            else
                return;
        }

        _interactionHold = holdPressed;
        if (_interactionInProgress) return;

        if (interactPressed)
        {
            StartInteraction();
        }
    }
    private void StartInteraction()
    {
        if (_interactionInProgress || _awaitReleaseAfterComplete)
            return;

        _awaitReleaseAfterComplete = false;
        _interactionInProgress = true;
        _progress = 0;
        G.Sound?.Play(GameAudioId.SFX_INTERACT_START);
        _interactionLoopSource = G.Sound?.PlayLoop(GameAudioId.SFX_INTERACT_LOOP);
        InteractionStarted?.Invoke();
        StartCoroutine(InteractionProcess());
    }

    private static bool IsInteractTriggered()
    {
        return G.Input != null && G.Input.Interaction;
    }

    private static bool IsHoldPressed()
    {
        return G.Input != null && G.Input.InteractionHold;
    }

    private IEnumerator InteractionProcess()
    {
        var completed = false;
        var lostHoldSec = 0f;
        while (_progress < 1f)
        {
            if (!_interactionHold)
            {
                if (_progress >= _nearCompleteThreshold)
                {
                    _progress = 1f;
                    if (_fillImg != null)
                        _fillImg.fillAmount = 1f;
                    NotifyInteractionCompleted();
                    completed = true;
                    break;
                }

                lostHoldSec += Time.deltaTime;
                if (lostHoldSec > _inputDropGraceSec)
                    break;

                yield return null;
                continue;
            }

            lostHoldSec = 0f;
            _progress += Time.deltaTime * _speed;
            if (_progress > 1f)
                _progress = 1f;

            if (_fillImg != null)
                _fillImg.fillAmount = _progress;

            if (_progress >= 1f)
            {
                NotifyInteractionCompleted();
                completed = true;
                break;
            }

            yield return null;
        }

        if (!completed && _progress >= 1f)
        {
            NotifyInteractionCompleted();
        }

        bool cancelled = !completed && _progress > 0f && _progress < 1f;
        ResetProgress();
        StopInteractionAudio(cancelled);
    }
    private void ResetProgress()
    {
        _progress = 0;
        if (_fillImg != null)
            _fillImg.fillAmount = 0;
        _interactionInProgress = false;
    }

    private void NotifyInteractionCompleted()
    {
        _awaitReleaseAfterComplete = true;
        StopInteractionAudio(false);
        G.Sound?.Play(GameAudioId.SFX_INTERACT_COMPLETE);
        InteractionComplete?.Invoke();
    }

    private void StopInteractionAudio(bool playCancel)
    {
        if (_interactionLoopSource != null)
        {
            G.Sound?.StopLoop(_interactionLoopSource);
            _interactionLoopSource = null;
        }

        if (playCancel)
            G.Sound?.Play(GameAudioId.SFX_INTERACT_CANCEL);
    }

    public void SetInfo(string actionText, string price = null)
    {
        if (_actionLocalizedText != null && _actionLocalizedText.enabled)
            _actionLocalizedText.enabled = false;
        if (_priceLocalizedText != null && _priceLocalizedText.enabled)
            _priceLocalizedText.enabled = false;

        _actionLocalizationKey = ResolveLocalizationKey(actionText);
        _actionLocalizationFallback = actionText;
        RefreshLocalizedAction();

        ApplyPrice(price);
        ApplyRewardedAdBadgeState();
    }

    public void SetInfoLocalized(string localizationKey, string fallback, string price = null)
    {
        if (_actionLocalizedText != null && _actionLocalizedText.enabled)
            _actionLocalizedText.enabled = false;
        if (_priceLocalizedText != null && _priceLocalizedText.enabled)
            _priceLocalizedText.enabled = false;

        _actionLocalizationKey = localizationKey;
        _actionLocalizationFallback = fallback;
        RefreshLocalizedAction();
        ApplyPrice(price);
        ApplyRewardedAdBadgeState();
    }

    private void ApplyPrice(string price)
    {
        if (_priceText != null)
        {
            _priceText.gameObject.SetActive(price != null);
            if (price != null)
                _priceText.text = CurrencyText.Coins(price);
        }
    }

    private void HandleLocalizationManagerReady(LocalizationManager manager)
    {
        SubscribeToLocalizationManager(manager);
        RefreshLocalizedAction();
    }

    private void HandleLanguageChanged(string _)
    {
        RefreshLocalizedAction();
    }

    private void SubscribeToLocalizationManager(LocalizationManager manager)
    {
        if (manager == null || _localizationManager == manager)
            return;

        UnsubscribeFromLocalizationManager();
        _localizationManager = manager;
        _localizationManager.OnLanguageChanged += HandleLanguageChanged;
    }

    private void UnsubscribeFromLocalizationManager()
    {
        if (_localizationManager == null)
            return;

        _localizationManager.OnLanguageChanged -= HandleLanguageChanged;
        _localizationManager = null;
    }

    private void RefreshLocalizedAction()
    {
        if (_actionText == null)
            return;

        _actionText.text = string.IsNullOrWhiteSpace(_actionLocalizationKey)
            ? _actionLocalizationFallback ?? string.Empty
            : LocalizationUtils.T(_actionLocalizationKey, _actionLocalizationFallback);
    }

    private static string ResolveLocalizationKey(string actionText)
    {
        var manager = LocalizationManager.Instance;
        var data = manager != null ? manager.LocalizationData : null;
        return data != null && data.TryFindKeyByTranslation(actionText, out string key)
            ? key
            : string.Empty;
    }

    public void SetRewardedAdBadgeVisible(bool visible)
    {
        _showRewardedAdBadge = visible;
        ApplyRewardedAdBadgeState();
    }

    public void ConfigureRewardedAdBadge(
        bool visible,
        Sprite sprite,
        string label,
        Transform badgeParent = null,
        Vector2? size = null,
        Vector2? offset = null)
    {
        _showRewardedAdBadge = visible;
        _rewardedAdBadgeSprite = sprite;
        _rewardedAdBadgeLabel = label;
        _rewardedAdBadgeParent = badgeParent;

        if (size.HasValue)
            _rewardedAdBadgeSize = size.Value;
        if (offset.HasValue)
            _rewardedAdBadgeOffset = offset.Value;

        ApplyRewardedAdBadgeState();
    }

    public void MarkAsRewardedAdInteraction(bool rewarded = true)
    {
        SetRewardedAdBadgeVisible(rewarded);
    }

    private void ApplyRewardedAdBadgeState()
    {
        if (!_showRewardedAdBadge)
        {
            if (_rewardedAdBadgeImage != null)
                _rewardedAdBadgeImage.gameObject.SetActive(false);
            if (_rewardedAdBadgeLabelText != null)
                _rewardedAdBadgeLabelText.gameObject.SetActive(false);
            return;
        }

        Image badge = EnsureRewardedAdBadge();
        if (badge != null)
        {
            badge.gameObject.SetActive(true);
            ApplyRewardedAdBadgeLabel(badge.transform);
        }
    }

    private Image EnsureRewardedAdBadge()
    {
        Transform badgeParent = ResolveRewardedAdBadgeParent();
        if (_rewardedAdBadgeImage != null)
        {
            if (_rewardedAdBadgeImage.transform.parent != badgeParent)
                _rewardedAdBadgeImage.transform.SetParent(badgeParent, false);
            ConfigureRewardedAdBadgeImage(_rewardedAdBadgeImage);
            return _rewardedAdBadgeImage;
        }

        Transform badgeTransform = badgeParent.Find(RewardedAdBadgeName);
        if (badgeTransform == null)
        {
            var badgeObject = new GameObject(RewardedAdBadgeName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
            badgeObject.transform.SetParent(badgeParent, false);
            badgeTransform = badgeObject.transform;
        }

        _rewardedAdBadgeImage = badgeTransform.GetComponent<Image>();
        if (_rewardedAdBadgeImage == null)
            _rewardedAdBadgeImage = badgeTransform.gameObject.AddComponent<Image>();

        ConfigureRewardedAdBadgeImage(_rewardedAdBadgeImage);

        var layoutElement = badgeTransform.GetComponent<LayoutElement>();
        if (layoutElement == null)
            layoutElement = badgeTransform.gameObject.AddComponent<LayoutElement>();
        layoutElement.ignoreLayout = true;

        return _rewardedAdBadgeImage;
    }

    private Transform ResolveRewardedAdBadgeParent()
    {
        if (_rewardedAdBadgeParent != null && _rewardedAdBadgeParent != transform)
            return _rewardedAdBadgeParent;

        // InteractionCanvas faces the camera through a 180-degree UI container.
        // Keep dynamically-created decorations beside the action text so they
        // inherit the same correction instead of appearing mirrored in world space.
        if (_actionText != null && _actionText.transform.parent != null)
            return _actionText.transform.parent;

        return transform;
    }

    private void ConfigureRewardedAdBadgeImage(Image badgeImage)
    {
        if (badgeImage == null)
            return;

        badgeImage.sprite = _rewardedAdBadgeSprite != null ? _rewardedAdBadgeSprite : AdButtonIconDecorator.GetIconSprite();
        badgeImage.type = Image.Type.Simple;
        badgeImage.preserveAspect = true;
        badgeImage.color = Color.white;
        badgeImage.raycastTarget = false;

        var rect = badgeImage.transform as RectTransform;
        if (rect != null)
        {
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = _rewardedAdBadgeOffset;
            rect.sizeDelta = _rewardedAdBadgeSize;
            rect.localScale = Vector3.one;
            rect.SetAsLastSibling();
        }
    }

    private void ApplyRewardedAdBadgeLabel(Transform badgeTransform)
    {
        if (badgeTransform == null)
            return;

        if (string.IsNullOrWhiteSpace(_rewardedAdBadgeLabel))
        {
            if (_rewardedAdBadgeLabelText != null)
                _rewardedAdBadgeLabelText.gameObject.SetActive(false);
            return;
        }

        TMP_Text label = EnsureRewardedAdBadgeLabel(badgeTransform);
        if (label == null)
            return;

        label.text = _rewardedAdBadgeLabel;
        label.gameObject.SetActive(true);
    }

    private TMP_Text EnsureRewardedAdBadgeLabel(Transform badgeTransform)
    {
        if (_rewardedAdBadgeLabelText != null)
            return _rewardedAdBadgeLabelText;

        Transform labelTransform = badgeTransform.Find(RewardedAdBadgeLabelName);
        if (labelTransform == null)
        {
            var labelObject = new GameObject(RewardedAdBadgeLabelName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(badgeTransform, false);
            labelTransform = labelObject.transform;
        }

        _rewardedAdBadgeLabelText = labelTransform.GetComponent<TMP_Text>();
        if (_rewardedAdBadgeLabelText == null)
            _rewardedAdBadgeLabelText = TmpUiTextFactory.Add(labelTransform.gameObject);

        TmpUiTextFactory.ApplyDefaults(_rewardedAdBadgeLabelText);
        if (_actionText != null && _actionText.font != null)
            _rewardedAdBadgeLabelText.font = _actionText.font;
        _rewardedAdBadgeLabelText.alignment = TextAlignmentOptions.Center;
        _rewardedAdBadgeLabelText.color = _rewardedAdBadgeLabelColor;
        _rewardedAdBadgeLabelText.fontSize = _rewardedAdBadgeLabelFontSize;
        _rewardedAdBadgeLabelText.fontStyle = FontStyles.Bold;
        _rewardedAdBadgeLabelText.raycastTarget = false;
        _rewardedAdBadgeLabelText.textWrappingMode = TextWrappingModes.NoWrap;
        _rewardedAdBadgeLabelText.overflowMode = TextOverflowModes.Overflow;
        _rewardedAdBadgeLabelText.outlineColor = _rewardedAdBadgeLabelOutlineColor;
        _rewardedAdBadgeLabelText.outlineWidth = 0.14f;

        var rect = labelTransform as RectTransform;
        if (rect != null)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = _rewardedAdBadgeLabelOffset;
            rect.sizeDelta = _rewardedAdBadgeLabelSize;
            rect.localScale = Vector3.one;
        }

        return _rewardedAdBadgeLabelText;
    }
}
