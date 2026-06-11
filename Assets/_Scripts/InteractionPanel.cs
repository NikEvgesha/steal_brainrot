using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InteractionPanel : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    private const string RewardedAdBadgeName = "AdIconBadge";

    [SerializeField] private GameObject _hintDesctop;
    [SerializeField] private GameObject _hintTouch;
    [SerializeField] private Image _fillImg;
    [SerializeField] private Text _priceText;
    [SerializeField] private Text _actionText;
    [SerializeField] private float _speed = 1f;
    [SerializeField] private float _inputDropGraceSec = 0.08f;
    [SerializeField] private float _nearCompleteThreshold = 0.99f;
    [SerializeField] private float _resumeAfterDisableWindowSec = 0.35f;
    [Header("Rewarded Ad Badge")]
    [SerializeField] private bool _showRewardedAdBadge;
    [SerializeField] private Vector2 _rewardedAdBadgeSize = new Vector2(36f, 36f);
    [SerializeField] private Vector2 _rewardedAdBadgeOffset = new Vector2(-7f, -7f);
    [SerializeField] public UnityEvent InteractionStarted;
    [SerializeField] public UnityEvent InteractionComplete;

    private float _progress;
    private bool _interactionInProgress;
    private bool _interactionHold;
    private bool _pointerHold;
    private LocalizedText _actionLocalizedText;
    private LocalizedText _priceLocalizedText;
    private bool _hasResumeProgress;
    private float _resumeProgress;
    private float _resumeUntilUnscaledTime;
    private bool _awaitReleaseAfterComplete;
    private Image _rewardedAdBadgeImage;

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
                _actionLocalizedText.enabled = false;
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
            StartCoroutine(InteractionProcess());
            return;
        }

        _hasResumeProgress = false;
        ResetProgress();
    }

    private void OnDisable()
    {
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

        ResetProgress();
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
        InteractionComplete?.Invoke();
    }

    public void SetInfo(string actionText, string price = null)
    {
        if (_actionLocalizedText != null && _actionLocalizedText.enabled)
            _actionLocalizedText.enabled = false;
        if (_priceLocalizedText != null && _priceLocalizedText.enabled)
            _priceLocalizedText.enabled = false;

        if (_actionText != null)
            _actionText.text = actionText;

        if (_priceText != null)
        {
            _priceText.gameObject.SetActive(price != null);
            if (price != null)
                _priceText.text = "$" + price;
        }

        ApplyRewardedAdBadgeState();
    }

    public void SetRewardedAdBadgeVisible(bool visible)
    {
        _showRewardedAdBadge = visible;
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
            return;
        }

        Image badge = EnsureRewardedAdBadge();
        if (badge != null)
            badge.gameObject.SetActive(true);
    }

    private Image EnsureRewardedAdBadge()
    {
        if (_rewardedAdBadgeImage != null)
            return _rewardedAdBadgeImage;

        Transform badgeTransform = transform.Find(RewardedAdBadgeName);
        if (badgeTransform == null)
        {
            var badgeObject = new GameObject(RewardedAdBadgeName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
            badgeObject.transform.SetParent(transform, false);
            badgeTransform = badgeObject.transform;
        }

        _rewardedAdBadgeImage = badgeTransform.GetComponent<Image>();
        if (_rewardedAdBadgeImage == null)
            _rewardedAdBadgeImage = badgeTransform.gameObject.AddComponent<Image>();

        _rewardedAdBadgeImage.sprite = AdButtonIconDecorator.GetIconSprite();
        _rewardedAdBadgeImage.type = Image.Type.Simple;
        _rewardedAdBadgeImage.preserveAspect = true;
        _rewardedAdBadgeImage.color = Color.white;
        _rewardedAdBadgeImage.raycastTarget = false;

        var rect = badgeTransform as RectTransform;
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

        var layoutElement = badgeTransform.GetComponent<LayoutElement>();
        if (layoutElement == null)
            layoutElement = badgeTransform.gameObject.AddComponent<LayoutElement>();
        layoutElement.ignoreLayout = true;

        return _rewardedAdBadgeImage;
    }
}
