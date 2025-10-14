using System.Collections;
using UnityEngine;
using UnityEngine.UI;
public class QuestUIItem : MonoBehaviour
{
    [Header("Ссылки на UI-элементы (заполните через Inspector)")]
    [SerializeField] private Text _titleText;
    [SerializeField] private Text _descriptionText;
    [SerializeField] private Slider _progressBar;
    [SerializeField] private Button _claimButton;
    [SerializeField] private Text _claimButtonText;
    [SerializeField] private GameObject _hint;
    [SerializeField] private GameObject _body;


    [Header("Параметры для анимации")]
    [SerializeField] private float _maxAnimScale;
    [SerializeField] private float _minAnimScale;
    [SerializeField] private float _speedScale;
    [SerializeField] private float _speedPos;
    [SerializeField] private float _startAnimPos = 100;
    [SerializeField] private float _finishAnimPos = 0;



    private float _animScale = 1;
    private float _animPos = 1;
    private bool _start = false;

    private QuestInstance _boundQuest;

    private void OnEnable()
    {
        StartCoroutine(StartAnimation());
    }

    private void Start()
    {
        LocalizationManager.Instance.OnLanguageChanged += UpdateLanguage;
    }

    private void OnDisable()
    {
        LocalizationManager.Instance.OnLanguageChanged -= UpdateLanguage;
    }
    private void UpdateLanguage(string lang)
    {
        _titleText.text = _boundQuest.questDefinition.Title;
        if (_descriptionText != null)
            _descriptionText.text = _boundQuest.questDefinition.Description;
    }

    /// <summary>
    /// Вызывается сразу после Instantiate(prefab) — привязываем QuestInstance
    /// и настраиваем UI.
    /// </summary>
    /// 
    public void Bind(QuestInstance quest)
    {
        _boundQuest = quest;

        // Установим локализованный текст (из QuestDefinition)
        _titleText.text = quest.questDefinition.Title;
        if (_descriptionText != null)
            _descriptionText.text = quest.questDefinition.Description;

        // Прячем кнопку «Забрать» до готовности квеста
        _claimButton.gameObject.SetActive(false);

        // Устанавливаем первоначальный прогресс
        float p = _boundQuest.GetCurrentProgress();
        if (_progressBar != null)
            _progressBar.value = p;

        // Подписываемся на события QuestInstance
        _boundQuest.OnProgressChanged += OnProgressChanged;
        _boundQuest.OnReadyToClaim += OnReadyToClaim;
        _boundQuest.OnQuestClaimed += OnBoundQuestDestroyed;
        _boundQuest.OnDestroyed += OnBoundQuestDestroyed;

        // Навешиваем клик на кнопку «Забрать»
        _claimButton.onClick.AddListener(() =>
        {
            //boundQuest.ClaimReward();
        });

        // Если квест уже завершён (IsCompleted) к этому моменту — сразу показать «Забрать»
        if (_boundQuest.IsCompleted)
        {
            OnReadyToClaim();
        }
    }

    private void OnProgressChanged(float normalized)
    {
        if (!_hint.activeSelf && normalized > 0)
            _hint.SetActive(true);

        // Обновляем шкалу, если кнопка «Забрать» ещё скрыта
        if (_progressBar != null)
            _progressBar.value = normalized;
    }

    private void OnReadyToClaim()
    {
        // Скрываем полоску прогресса и показываем кнопку «Забрать награду»
        //if (progressBar != null)
        //progressBar.gameObject.SetActive(false);
        OnProgressChanged(1);
        StartCoroutine(EndAnimation());
        //  claimButton.gameObject.SetActive(true);

        //_boundQuest.ClaimReward();
        //claimButtonText.text = "Забрать награду";
    }

    private void OnBoundQuestDestroyed(QuestInstance _)
    {
        // Когда QuestInstance удаляется (Destroy) или ClaimReward  уничтожаем UI
        Destroy(gameObject);
    }
    private void OnBoundQuestDestroyed()
    {
        // Когда QuestInstance удаляется (Destroy) или ClaimReward  уничтожаем UI
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (_boundQuest != null)
        {
            _boundQuest.OnProgressChanged -= OnProgressChanged;
            _boundQuest.OnReadyToClaim -= OnReadyToClaim;
            _boundQuest.OnQuestClaimed -= OnBoundQuestDestroyed;
            _boundQuest.OnDestroyed -= OnBoundQuestDestroyed;
        }
        _claimButton.onClick.RemoveAllListeners();
    }
    private IEnumerator EndAnimation()
    {
        while (!_start)
        {
            yield return null;
        }
        _animScale = transform.localScale.x;
        while (_animScale < _maxAnimScale)
        {
            _animScale += Time.deltaTime * _speedScale;
            transform.localScale = Vector3.one * _animScale;
            yield return null;
        }
        while (_animScale > _minAnimScale)
        {
            _animScale -= Time.deltaTime * _speedScale;
            transform.localScale = Vector3.one * _animScale;
            yield return null;
        }

        _boundQuest.ClaimReward();
    }
    private IEnumerator StartAnimation()
    {
        _animPos = _body.transform.localPosition.y + _startAnimPos;
        while (_animPos > _finishAnimPos)
        {

            _animPos -= Time.deltaTime * _speedPos;
            _body.transform.localPosition = Vector3.up * _animPos;
            yield return null;
        }
        _body.transform.localPosition = Vector3.zero;
        _start = true;
    }
}
