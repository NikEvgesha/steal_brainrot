using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class UniversalDecisionPopup : MonoBehaviour
{
    [Serializable]
    public struct LocalizedTextPayload
    {
        public string key;
        [TextArea]
        public string fallback;

        public LocalizedTextPayload(string key, string fallback)
        {
            this.key = key;
            this.fallback = fallback;
        }
    }

    public sealed class Request
    {
        public LocalizedTextPayload title;
        public LocalizedTextPayload description;
        public LocalizedTextPayload confirm;
        public LocalizedTextPayload cancel;
        public Action onConfirm;
        public Action onCancel;
        public bool closeOnConfirm = true;
        public bool closeOnCancel = true;
        public bool closeButtonActsAsCancel = true;
    }

    [Header("Root")]
    [SerializeField] private GameObject root;
    [SerializeField] private bool hideOnStart = true;

    [Header("Texts")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text confirmButtonText;
    [SerializeField] private TMP_Text cancelButtonText;

    [Header("Buttons")]
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private Button closeButton;

    [Header("Defaults")]
    [SerializeField] private string defaultTitleKey = "UI/Popup/ConfirmTitle";
    [SerializeField] private string defaultTitleFallback = "Подтверждение";
    [SerializeField] private string defaultDescriptionKey = "UI/Popup/ConfirmDescription";
    [SerializeField] private string defaultDescriptionFallback = "Вы уверены?";
    [SerializeField] private string defaultConfirmKey = "UI/Popup/Yes";
    [SerializeField] private string defaultConfirmFallback = "Да";
    [SerializeField] private string defaultCancelKey = "UI/Popup/No";
    [SerializeField] private string defaultCancelFallback = "Нет";

    [Header("Events")]
    [SerializeField] private UnityEvent onConfirm;
    [SerializeField] private UnityEvent onCancel;

    private LocalizedText _titleLocalized;
    private LocalizedText _descriptionLocalized;
    private LocalizedText _confirmLocalized;
    private LocalizedText _cancelLocalized;
    private Request _activeRequest;

    public bool IsOpen
    {
        get
        {
            var target = root != null ? root : gameObject;
            return target.activeSelf;
        }
    }

    private void Awake()
    {
        AutoBindReferences();
        CacheLocalizedTextRefs();

        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirmPressed);
        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancelPressed);
        if (closeButton != null)
            closeButton.onClick.AddListener(OnClosePressed);

        if (hideOnStart)
            SetVisible(false);
    }

    private void OnEnable()
    {
        SubscribeLocalization();
    }

    private void OnDisable()
    {
        UnsubscribeLocalization();
    }

    private void OnDestroy()
    {
        if (confirmButton != null)
            confirmButton.onClick.RemoveListener(OnConfirmPressed);
        if (cancelButton != null)
            cancelButton.onClick.RemoveListener(OnCancelPressed);
        if (closeButton != null)
            closeButton.onClick.RemoveListener(OnClosePressed);

        UnsubscribeLocalization();
    }

    public void Show(Request request)
    {
        _activeRequest = request ?? BuildDefaultRequest();
        EnsureDefaults(_activeRequest);
        ApplyRequest(_activeRequest);
        SetVisible(true);
    }

    public void Show(
        string titleKey,
        string titleFallback,
        string descriptionKey,
        string descriptionFallback,
        string confirmKey,
        string confirmFallback,
        string cancelKey,
        string cancelFallback,
        Action onConfirmAction = null,
        Action onCancelAction = null,
        bool closeOnConfirm = true,
        bool closeOnCancel = true,
        bool closeButtonActsAsCancel = true)
    {
        var request = new Request
        {
            title = new LocalizedTextPayload(titleKey, titleFallback),
            description = new LocalizedTextPayload(descriptionKey, descriptionFallback),
            confirm = new LocalizedTextPayload(confirmKey, confirmFallback),
            cancel = new LocalizedTextPayload(cancelKey, cancelFallback),
            onConfirm = onConfirmAction,
            onCancel = onCancelAction,
            closeOnConfirm = closeOnConfirm,
            closeOnCancel = closeOnCancel,
            closeButtonActsAsCancel = closeButtonActsAsCancel
        };

        Show(request);
    }

    public void Hide()
    {
        _activeRequest = null;
        SetVisible(false);
    }

    private void OnConfirmPressed()
    {
        var request = _activeRequest;
        if (request != null && request.closeOnConfirm)
            SetVisible(false);

        onConfirm?.Invoke();
        request?.onConfirm?.Invoke();

        if (request != null && request.closeOnConfirm)
            _activeRequest = null;
    }

    private void OnCancelPressed()
    {
        var request = _activeRequest;
        if (request != null && request.closeOnCancel)
            SetVisible(false);

        onCancel?.Invoke();
        request?.onCancel?.Invoke();

        if (request != null && request.closeOnCancel)
            _activeRequest = null;
    }

    private void OnClosePressed()
    {
        if (_activeRequest != null && _activeRequest.closeButtonActsAsCancel)
            OnCancelPressed();
        else
            Hide();
    }

    private void OnLanguageChanged(string _)
    {
        if (!IsOpen || _activeRequest == null)
            return;

        ApplyRequest(_activeRequest);
    }

    private void ApplyRequest(Request request)
    {
        ApplyLabel(titleText, _titleLocalized, request.title);
        ApplyLabel(descriptionText, _descriptionLocalized, request.description);
        ApplyLabel(confirmButtonText, _confirmLocalized, request.confirm);
        ApplyLabel(cancelButtonText, _cancelLocalized, request.cancel);
        AdButtonIconDecorator.SetAdIcon(confirmButton, ShouldShowAdIcon(request.confirm));
    }

    private void EnsureDefaults(Request request)
    {
        if (IsEmpty(request.title))
            request.title = new LocalizedTextPayload(defaultTitleKey, defaultTitleFallback);

        if (IsEmpty(request.description))
            request.description = new LocalizedTextPayload(defaultDescriptionKey, defaultDescriptionFallback);

        if (IsEmpty(request.confirm))
            request.confirm = new LocalizedTextPayload(defaultConfirmKey, defaultConfirmFallback);

        if (IsEmpty(request.cancel))
            request.cancel = new LocalizedTextPayload(defaultCancelKey, defaultCancelFallback);
    }

    private static bool IsEmpty(LocalizedTextPayload payload)
    {
        return string.IsNullOrWhiteSpace(payload.key) && string.IsNullOrWhiteSpace(payload.fallback);
    }

    private static bool ShouldShowAdIcon(LocalizedTextPayload payload)
    {
        return ContainsAdToken(payload.key) || ContainsAdToken(payload.fallback);
    }

    private static bool ContainsAdToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        string lower = value.ToLowerInvariant();
        return lower.Contains("реклам")
            || lower.Contains("(ad)")
            || lower.Contains("watchad")
            || lower.Contains("rewardedad")
            || lower.Contains("adreward")
            || lower.Contains("x2ad")
            || lower.EndsWith("ad")
            || lower.Contains(" ad")
            || lower.Contains("ad ");
    }

    private static Request BuildDefaultRequest()
    {
        return new Request();
    }

    private void ApplyLabel(TMP_Text text, LocalizedText localized, LocalizedTextPayload payload)
    {
        if (text == null)
            return;

        if (!string.IsNullOrWhiteSpace(payload.key))
        {
            var manager = LocalizationManager.Instance;
            if (localized != null && manager != null && manager.LocalizationData != null)
            {
                localized.enabled = true;
                localized.Configure(manager.LocalizationData, payload.key, manager.CurrentLanguage);
                return;
            }

            if (localized != null && localized.enabled)
                localized.enabled = false;

            text.text = ResolveLocalization(payload.key, payload.fallback);
            return;
        }

        if (localized != null && localized.enabled)
            localized.enabled = false;

        text.text = payload.fallback ?? string.Empty;
    }

    private static string ResolveLocalization(string key, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(key)
            && LocalizationManager.Instance != null
            && LocalizationManager.Instance.LocalizationData != null)
        {
            var translated = LocalizationManager.Instance.LocalizationData.GetTranslation(key);
            if (!string.IsNullOrWhiteSpace(translated)
                && !string.Equals(translated, key, StringComparison.Ordinal))
            {
                return translated;
            }
        }

        if (!string.IsNullOrWhiteSpace(fallback))
            return fallback;

        return key ?? string.Empty;
    }

    private void SubscribeLocalization()
    {
        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.OnLanguageChanged += OnLanguageChanged;
    }

    private void UnsubscribeLocalization()
    {
        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.OnLanguageChanged -= OnLanguageChanged;
    }

    private void AutoBindReferences()
    {
        if (root == null)
            root = gameObject;

        if (titleText == null)
            titleText = FindByPath<TMP_Text>("container/panel/Elements/Title/Text (TMP)");
        if (descriptionText == null)
            descriptionText = FindByPath<TMP_Text>("container/panel/Elements/Discription/Text (TMP) (1)");
        if (confirmButtonText == null)
            confirmButtonText = FindByPath<TMP_Text>("container/panel/Elements/Buttons/Ok/Text (TMP)");
        if (cancelButtonText == null)
            cancelButtonText = FindByPath<TMP_Text>("container/panel/Elements/Buttons/Cancel/Text (TMP)");

        if (confirmButton == null)
            confirmButton = FindByPath<Button>("container/panel/Elements/Buttons/Ok");
        if (cancelButton == null)
            cancelButton = FindByPath<Button>("container/panel/Elements/Buttons/Cancel");
        if (closeButton == null)
            closeButton = FindByPath<Button>("container/panel/Button (Legacy)");

        // Fallback lookup by names in case hierarchy labels change slightly.
        if (titleText == null)
            titleText = FindFirstTextUnder("Title");
        if (descriptionText == null)
            descriptionText = FindFirstTextUnder("Discription");
        if (confirmButtonText == null)
            confirmButtonText = FindFirstTextUnder("Ok");
        if (cancelButtonText == null)
            cancelButtonText = FindFirstTextUnder("Cancel");

        if (confirmButton == null)
            confirmButton = FindFirstComponentUnder<Button>("Ok");
        if (cancelButton == null)
            cancelButton = FindFirstComponentUnder<Button>("Cancel");
        if (closeButton == null)
            closeButton = FindFirstComponentUnder<Button>("Button (Legacy)");
    }

    private void CacheLocalizedTextRefs()
    {
        _titleLocalized = titleText != null ? titleText.GetComponent<LocalizedText>() : null;
        _descriptionLocalized = descriptionText != null ? descriptionText.GetComponent<LocalizedText>() : null;
        _confirmLocalized = confirmButtonText != null ? confirmButtonText.GetComponent<LocalizedText>() : null;
        _cancelLocalized = cancelButtonText != null ? cancelButtonText.GetComponent<LocalizedText>() : null;
    }

    private T FindByPath<T>(string path) where T : Component
    {
        var node = transform.Find(path);
        return node != null ? node.GetComponent<T>() : null;
    }

    private TMP_Text FindFirstTextUnder(string nodeName)
    {
        var node = FindNodeRecursive(transform, nodeName);
        return node != null ? node.GetComponentInChildren<TMP_Text>(true) : null;
    }

    private T FindFirstComponentUnder<T>(string nodeName) where T : Component
    {
        var node = FindNodeRecursive(transform, nodeName);
        return node != null ? node.GetComponent<T>() : null;
    }

    private static Transform FindNodeRecursive(Transform parent, string targetName)
    {
        if (parent == null || string.IsNullOrWhiteSpace(targetName))
            return null;

        if (string.Equals(parent.name, targetName, StringComparison.Ordinal))
            return parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            var found = FindNodeRecursive(parent.GetChild(i), targetName);
            if (found != null)
                return found;
        }

        return null;
    }

    private void SetVisible(bool visible)
    {
        var target = root != null ? root : gameObject;
        if (target != null && target.activeSelf != visible)
            target.SetActive(visible);
    }
}
