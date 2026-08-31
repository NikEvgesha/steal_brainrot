using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class BigPetPoint : MonoBehaviour
{
    [Serializable]
    private struct XpRequirementAnchor
    {
        [Min(1)] public int Level;
        [Min(1)] public int XpRequired;
    }

    public static event Action<int> LocalLevelChanged;

    private static readonly HashSet<string> ExcludedBigPetIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "balerina",
        "BalerinaCapuchina",
        "frutodrillo",
        "Frutodillo",
        "sahur",
        "Sahur",
        "TungTungSahur",
        "GlorboFruttodrillo",
    };
    private static readonly ElementType[] ProgressionElements =
    {
        ElementType.NoElement,
        ElementType.Gold,
        ElementType.Diamond,
        ElementType.Electric,
        ElementType.Fire
    };

    [SerializeField] private bool _remoteMode;
    [SerializeField] private double _unlockPrice;
    [SerializeField] private List<Brainrot> _pets;
    [SerializeField] private GameObject _feedButton;
    [SerializeField] private Transform _foodPoint;
    [SerializeField] private Transform _petPoint;
    [SerializeField] private int _baseXPperLvl;
    [SerializeField] private int _xpAddintPerLvl;
    [SerializeField] private int _xpQuadraticPerLvl;
    [SerializeField] private int _xpCubicPerLvl;
    [SerializeField] private List<XpRequirementAnchor> _xpRequirementAnchors;
    [SerializeField] private Slider _xpProgressBar;
    [SerializeField] private Slider _foodTimeBar;
    [SerializeField] private TMP_Text _xpProgressText;
    [SerializeField] private TMP_Text _foodTimeBarText;
    [SerializeField] private float _foodScaler;
    [SerializeField] private float _petScaler;
    [SerializeField] private float _petGroundOffset;
    [SerializeField] private int _lvlsPerPet = 5;
    [SerializeField] private BrainrotInfoUI _petInfoUI;
    [SerializeField] private AudioSource _audio;
    [SerializeField] private InteractionPanel _buyPanel;
    [SerializeField] private GameObject _changePetArea;
    [SerializeField, Min(1f)] private float _changePetInteractionDistance = 5f;

    private bool _purchased;
    private bool _playerInArea;
    private int _currentXp;
    private int _currentLvl;
    private int _xpForNextLvl;
    private bool _feeding;
    private Food _currentFood;
    private GameObject _currentPet;
    private int _currentPetIdx;
    private int _maxAvailablePetIdx;
    private int _maxLvl;
    private double _currentIncome;
    private double _accumulatedIncome;
    private BigPetSetUI _setPetUI;
    private DateTime _lastIncomeCollectTimestamp;
    private bool _initializedLocal;
    private Coroutine _incomeRoutine;
    private Coroutine _groundAlignmentRoutine;
    private bool _quickAccessBound;
    private bool _hasOfflineIncomePending;
    private readonly List<Brainrot> _activePets = new List<Brainrot>();

    public double CurrentIncomePerSecond => _purchased ? _currentIncome : 0d;
    public double AccumulatedIncome => _remoteMode ? 0d : Math.Max(0d, _accumulatedIncome);
    public double MaxAccumulatedIncome =>
        Math.Max(0d, CurrentIncomePerSecond * OfflineRewardRules.MaxAccrualSeconds);
    public double UnlockPrice => Math.Max(0d, _unlockPrice);
    public bool IsPurchased => _purchased;
    public int CurrentLevel => Mathf.Max(1, _currentLvl);
    public bool IsPlayerInArea => _playerInArea;
    public Transform BuyActionTarget => _buyPanel != null ? _buyPanel.transform : transform;
    public Transform FeedActionTarget => _feedButton != null ? _feedButton.transform : transform;
    public bool IsRemoteMode => _remoteMode;
    public bool HasCollectibleIncome => !_remoteMode && _purchased && _accumulatedIncome > 0d;

    [HideInInspector] public UnityEvent PlayerEnter;
    [HideInInspector] public UnityEvent PlayerExit;

    private void Start()
    {
        if (_remoteMode) return;
        InitLocal();
    }

    private void OnEnable()
    {
        if (_initializedLocal && !_remoteMode && _purchased)
            EnsureIncomeRoutine();
    }

    private void OnDisable()
    {
        StopIncomeRoutine();
        StopGroundAlignmentRoutine();
    }

    private void OnDestroy()
    {
        if (_setPetUI != null)
            _setPetUI.PetSlotClicked.RemoveListener(ChangeActivePet);
    }

    private void InitLocal()
    {
        if (_initializedLocal) return;
        if (_remoteMode) return;
        _initializedLocal = true;

        _setPetUI = GetComponentInChildren<BigPetSetUI>(true);
        if (_setPetUI == null)
        {
            Debug.LogWarning("[BigPetPoint] BigPetSetUI not found.");
            PrepareLockedState();
            return;
        }

        _setPetUI.SetRemoteMode(false);
        if (!ResolvePetList())
        {
            Debug.LogWarning("[BigPetPoint] Pets list is empty.");
            PrepareLockedState();
            return;
        }

        BindPetSelection();

        CacheSceneRefs();

        ReloadLocalStateFromSave();
    }

    public void _OnPlayerEnter()
    {
        if (_remoteMode) return;

        _playerInArea = true;
        if (_purchased)
        {
            SetQuickAccessBinding(true);
            CheckPlayer(G.QuickAccess != null ? G.QuickAccess.CurrentActive : null);
            GetIncome();
            if (_petInfoUI != null)
                _petInfoUI.gameObject.SetActive(true);
        }
        else
        {
            ApplyLockedUiState(true);
        }

        PlayerEnter?.Invoke();
    }

    public void _OnPlayerExit()
    {
        if (_remoteMode) return;

        if (_purchased)
        {
            SetQuickAccessBinding(false);
            if (_feedButton != null)
                _feedButton.SetActive(false);
            if (_petInfoUI != null)
                _petInfoUI.gameObject.SetActive(true);
        }
        else
        {
            ApplyLockedUiState(false);
        }

        _playerInArea = false;
        PlayerExit?.Invoke();
    }

    private void HideRemoteUI()
    {
        if (_feedButton != null) _feedButton.SetActive(false);
        if (_foodTimeBar != null) _foodTimeBar.gameObject.SetActive(false);
        if (_xpProgressBar != null) _xpProgressBar.gameObject.SetActive(false);
        if (_foodTimeBarText != null) _foodTimeBarText.gameObject.SetActive(false);
        if (_xpProgressText != null) _xpProgressText.gameObject.SetActive(false);
        if (_setPetUI != null) _setPetUI.gameObject.SetActive(false);
        if (_petInfoUI != null) _petInfoUI.gameObject.SetActive(false);
        if (_buyPanel != null) _buyPanel.gameObject.SetActive(false);
        if (_changePetArea != null) _changePetArea.SetActive(false);
    }

    private void ShowLocalUI()
    {
        if (_feedButton != null) _feedButton.SetActive(false);
        if (_foodTimeBar != null) _foodTimeBar.gameObject.SetActive(false);
        if (_xpProgressBar != null) _xpProgressBar.gameObject.SetActive(true);
        if (_foodTimeBarText != null) _foodTimeBarText.gameObject.SetActive(false);
        if (_xpProgressText != null) _xpProgressText.gameObject.SetActive(true);
        if (_setPetUI != null) _setPetUI.gameObject.SetActive(_purchased);
        if (_petInfoUI != null) _petInfoUI.gameObject.SetActive(_purchased);
        if (_changePetArea != null) _changePetArea.SetActive(_purchased);
        if (_buyPanel != null) _buyPanel.gameObject.SetActive(false);
    }

    private void CheckPlayer(InventoryItem item = null)
    {
        if (_remoteMode) return;
        if (_feedButton == null) return;

        bool canFeed = _purchased
                       && !_feeding
                       && _playerInArea
                       && _currentLvl < _maxLvl
                       && item != null
                       && item.Type == Item.Food;
        if (canFeed)
        {
            InteractionPanel feedPanel = _feedButton.GetComponent<InteractionPanel>();
            if (feedPanel != null)
                feedPanel.SetInfoLocalized("Feed", "Feed");

            if (_buyPanel != null)
                _buyPanel.gameObject.SetActive(false);
        }

        _feedButton.SetActive(canFeed);
    }

    public void _Feed()
    {
        if (_remoteMode || !_purchased || !_playerInArea) return;
        if (_feeding) return;

        InventoryItem currentItem = G.QuickAccess.CurrentActive;
        if (currentItem == null) return;

        Food food = null;
        bool isFood = currentItem.Type == Item.Food && currentItem.TryGetComponent(out food);
        if (!isFood) return;

        G.QuickAccess.DropCurrent(_foodPoint);
        G.Sound?.PlayAt(GameAudioId.SFX_FEED_OFFER, _foodPoint != null ? _foodPoint.position : transform.position);
        _currentFood = food;
        _currentFood.transform.localScale = Vector3.one * _foodScaler;
        _feeding = true;
        if (_feedButton != null)
            _feedButton.SetActive(false);
        TutorialSignals.Raise(TutorialSignalType.BigPetFed, this, food.Name, Item.Food);
        StartCoroutine(FeedProcess());
    }

    private IEnumerator FeedProcess()
    {
        if (_remoteMode) yield break;
        string analyticsFoodId = _currentFood != null ? _currentFood.Name : string.Empty;
        int analyticsLevelBefore = _currentLvl;
        int analyticsXpBefore = _currentXp;
        if (_foodTimeBar != null)
            _foodTimeBar.gameObject.SetActive(true);

        int secondsRemains = _currentFood.Data.SecondsDuration;
        if (_foodTimeBarText != null)
        {
            _foodTimeBarText.text = string.Format(
                "{0}:{1}",
                (secondsRemains / 60).ToString("D2"),
                (secondsRemains % 60).ToString("D2"));
        }

        if (_foodTimeBar != null)
            _foodTimeBar.value = 1f;

        while (secondsRemains > 0)
        {
            yield return new WaitForSeconds(1f);
            secondsRemains--;
            AddExperience(_currentFood.Data.XPPerSecond);
            if (secondsRemains % 3 == 0)
            {
                G.Sound?.PlayAt(GameAudioId.SFX_CHEW_LOOP, transform.position);
                G.Sound?.Play(GameAudioId.SFX_XP_PULSE);
            }
            G.Save.SaveBigPetXP(_currentXp);

            float t = (float)secondsRemains / _currentFood.Data.SecondsDuration;
            _currentFood.transform.localScale = Vector3.Lerp(Vector3.one * _foodScaler, Vector3.one, 1f - t);
            if (_foodTimeBar != null)
                _foodTimeBar.value = t;
            if (_foodTimeBarText != null)
            {
                _foodTimeBarText.text = string.Format(
                    "{0}:{1}",
                    (secondsRemains / 60).ToString("D2"),
                    (secondsRemains % 60).ToString("D2"));
            }
        }

        _feeding = false;
        Destroy(_currentFood.gameObject);
        _currentFood = null;
        CheckPlayer(G.QuickAccess != null ? G.QuickAccess.CurrentActive : null);
        if (_foodTimeBar != null)
            _foodTimeBar.gameObject.SetActive(false);

        GameAnalytics.Track(AnalyticsEventNames.BigPetFed, GameAnalytics.Params(
            "big_pet_id", gameObject.name,
            "food_id", analyticsFoodId,
            "level_before", analyticsLevelBefore,
            "level_after", _currentLvl,
            "xp_before", analyticsXpBefore,
            "xp_after", _currentXp,
            "source", "big_pet_feed",
            "result", "success"));
    }

    private void CheckLvl()
    {
        CheckLvl(_currentXp);
    }

    private void AddExperience(int xpAmount)
    {
        if (_remoteMode || xpAmount <= 0)
            return;

        // Late-game food can add hundreds of millions of XP per tick. Keep the
        // transient sum as long so an almost-full int XP bar cannot overflow.
        CheckLvl((long)_currentXp + xpAmount);
    }

    private void CheckLvl(long availableXp)
    {
        if (_remoteMode) return;

        _maxLvl = Mathf.Max(1, GetBigPetVariantCount() * Mathf.Max(1, _lvlsPerPet));
        _currentLvl = Mathf.Clamp(_currentLvl, 1, _maxLvl);
        _xpForNextLvl = GetXpRequiredForLevel(_currentLvl);
        availableXp = Math.Max(0L, availableXp);

        if (_currentLvl >= _maxLvl)
        {
            _currentXp = 0;
            G.Save.SaveBigPetXP(_currentXp);
            UpdateXpUI();
            return;
        }

        int levelBefore = _currentLvl;
        int previousMaxAvailablePetIdx = _maxAvailablePetIdx;
        while (_currentLvl < _maxLvl && availableXp >= _xpForNextLvl)
        {
            availableXp -= _xpForNextLvl;
            _currentLvl++;
            _xpForNextLvl = GetXpRequiredForLevel(_currentLvl);
        }

        _currentXp = _currentLvl >= _maxLvl
            ? 0
            : (int)Math.Min(int.MaxValue, availableXp);

        if (_currentLvl != levelBefore)
        {
            G.Save.SaveBigPetLvl(_currentLvl);
            G.Save.SaveBigPetXP(_currentXp);
            LocalLevelChanged?.Invoke(_currentLvl);
            BaseDirtyTracker.MarkDirty();
            if (_feeding)
                G.Sound?.Play(GameAudioId.SFX_LEVEL_UP);

            var newMaxAvailablePetIdx = GetMaxAvailablePetIndexForLevel(_currentLvl);
            if (newMaxAvailablePetIdx != _maxAvailablePetIdx)
            {
                _maxAvailablePetIdx = newMaxAvailablePetIdx;
                _currentIncome = GetVariantIncome(_maxAvailablePetIdx);
                if (_setPetUI != null)
                    _setPetUI.SetMaxAvailablePet(_maxAvailablePetIdx);
                if (_petInfoUI != null)
                    _petInfoUI.SetInfo(_currentIncome);
                SetPet(_maxAvailablePetIdx);
                if (_feeding && _maxAvailablePetIdx > previousMaxAvailablePetIdx)
                    G.Sound?.Play(GameAudioId.SFX_UNLOCK_MAJOR);
            }

            CheckScale();
            GameAnalytics.Track(AnalyticsEventNames.BigPetLevelUp, GameAnalytics.Params(
                "big_pet_id", gameObject.name,
                "level_before", levelBefore,
                "level_after", _currentLvl,
                "variant_unlocked", _maxAvailablePetIdx > previousMaxAvailablePetIdx,
                "active_variant_index", _currentPetIdx,
                "source", "feeding",
                "result", "success"));
        }

        UpdateXpUI();
    }

    private int GetXpRequiredForLevel(int level)
    {
        if (TryGetAnchoredXpRequirement(level, out int anchoredRequirement))
            return anchoredRequirement;

        long baseXp = Mathf.Max(1, _baseXPperLvl);
        long linear = Mathf.Max(0, _xpAddintPerLvl);
        long quadratic = Mathf.Max(0, _xpQuadraticPerLvl);
        long cubic = Mathf.Max(0, _xpCubicPerLvl);
        long levelOffset = Mathf.Max(0, level - 1);
        long levelSquared = levelOffset * levelOffset;
        long result = baseXp
                      + linear * levelOffset
                      + quadratic * levelSquared
                      + cubic * levelSquared * levelOffset;
        return (int)Math.Min(int.MaxValue, result);
    }

    private bool TryGetAnchoredXpRequirement(int level, out int requirement)
    {
        requirement = 0;
        if (_xpRequirementAnchors == null || _xpRequirementAnchors.Count == 0)
            return false;

        int targetLevel = Mathf.Max(1, level);
        bool hasLower = false;
        bool hasUpper = false;
        int lowerLevel = 0;
        int upperLevel = int.MaxValue;
        int lowerXp = 0;
        int upperXp = 0;

        foreach (XpRequirementAnchor anchor in _xpRequirementAnchors)
        {
            int anchorLevel = Mathf.Max(1, anchor.Level);
            int anchorXp = Mathf.Max(1, anchor.XpRequired);
            if (anchorLevel == targetLevel)
            {
                requirement = anchorXp;
                return true;
            }

            if (anchorLevel < targetLevel && (!hasLower || anchorLevel > lowerLevel))
            {
                hasLower = true;
                lowerLevel = anchorLevel;
                lowerXp = anchorXp;
            }
            else if (anchorLevel > targetLevel && (!hasUpper || anchorLevel < upperLevel))
            {
                hasUpper = true;
                upperLevel = anchorLevel;
                upperXp = anchorXp;
            }
        }

        if (!hasLower && !hasUpper)
            return false;
        if (!hasLower)
        {
            requirement = upperXp;
            return true;
        }
        if (!hasUpper)
        {
            requirement = lowerXp;
            return true;
        }

        double interpolation = (targetLevel - lowerLevel) / (double)(upperLevel - lowerLevel);
        double interpolatedXp = lowerXp + (upperXp - (double)lowerXp) * interpolation;
        requirement = (int)Math.Max(1d, Math.Min(int.MaxValue, Math.Round(interpolatedXp)));
        return true;
    }

    private void UpdateXpUI()
    {
        bool isMaxLevel = _currentLvl >= _maxLvl;
        if (_xpProgressBar != null)
            _xpProgressBar.value = isMaxLevel ? 1f : (float)_currentXp / Mathf.Max(1, _xpForNextLvl);
        if (_xpProgressText != null)
        {
            _xpProgressText.text = isMaxLevel
                ? string.Format("LVL {0} : MAX", _currentLvl)
                : string.Format("LVL {0} : {1} / {2}", _currentLvl, _currentXp, _xpForNextLvl);
        }
    }

    private void CheckScale()
    {
        if (_currentPet == null)
            return;

        if (_currentPetIdx < _maxAvailablePetIdx || _currentLvl >= _maxLvl)
        {
            _currentPet.transform.localScale = _petScaler * Vector3.one;
        }
        else
        {
            var levelsPerPet = Mathf.Max(1, _lvlsPerPet);
            float t = (((_currentLvl - 1) % levelsPerPet) + 1) / (float)levelsPerPet;
            _currentPet.transform.localScale = Vector3.Lerp(Vector3.one, Vector3.one * _petScaler, t);
        }

        AlignCurrentPetToGround();
        ScheduleGroundAlignment();
    }

    private void SetPet(int idx)
    {
        if (_activePets == null || _activePets.Count == 0)
            return;

        idx = Mathf.Clamp(idx, 0, GetBigPetVariantCount() - 1);
        int basePetIndex = GetBasePetIndex(idx);
        ElementType element = GetVariantElement(idx);

        if (_currentPet != null)
            Destroy(_currentPet.gameObject);

        _currentPetIdx = idx;
        if (!_remoteMode)
            G.Save.SaveBigPetId(_currentPetIdx);
        if (!_remoteMode)
            BaseDirtyTracker.MarkDirty();

        if (_petPoint != null)
        {
            _currentPet = Instantiate(_activePets[basePetIndex].Model, _petPoint, false);
            _currentPet.transform.localPosition = Vector3.zero;
            ApplyBigPetElementVisual(_currentPet, element);
        }

        if (_setPetUI != null)
            _setPetUI.ChangeActivePet(idx);

        CheckScale();
        if (_currentPet != null)
            GroundBlobShadow.Ensure(_currentPet, GroundBlobShadowPreset.BigPet);
    }

    private void ApplyBigPetElementVisual(GameObject petModel, ElementType element)
    {
        if (petModel == null)
            return;

        Color tint = GetElementTint(element);
        if (element != ElementType.NoElement && element != ElementType.ElementType)
        {
            Renderer[] renderers = petModel.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                    continue;

                Material[] materials = renderer.materials;
                for (int j = 0; j < materials.Length; j++)
                {
                    Material material = materials[j];
                    if (material == null)
                        continue;

                    if (material.HasProperty("_BaseColor"))
                    {
                        Color baseColor = material.GetColor("_BaseColor");
                        material.SetColor("_BaseColor", Color.Lerp(baseColor, tint, 0.72f));
                    }
                    else if (material.HasProperty("_Color"))
                    {
                        material.color = Color.Lerp(material.color, tint, 0.72f);
                    }
                }
            }
        }

        ElementTypeVfx.Ensure(
            this,
            petModel.transform,
            element,
            sizeMultiplier: 1.45f,
            emissionMultiplier: 1.35f,
            rayAlphaMultiplier: 0.8f,
            particleRadiusMultiplier: 1.15f,
            raySpinSpeed: 25f,
            groundAlphaMultiplier: 0.75f);
    }

    private static Color GetElementTint(ElementType element)
    {
        return element switch
        {
            ElementType.Gold => new Color(1f, 0.82f, 0.08f),
            ElementType.Diamond => new Color(0.22f, 0.88f, 1f),
            ElementType.Electric => new Color(0.78f, 0.28f, 1f),
            ElementType.Fire => new Color(1f, 0.22f, 0.08f),
            _ => Color.white
        };
    }

    private void AlignCurrentPetToGround()
    {
        if (_currentPet == null)
            return;

        Renderer[] renderers = _currentPet.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        Bounds bounds = default;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (!IsPetGeometryRenderer(renderer))
                continue;

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        if (!hasBounds)
            return;

        float targetBottom = (_petPoint != null ? _petPoint.position.y : transform.position.y) + _petGroundOffset;
        float deltaY = targetBottom - bounds.min.y;
        if (Mathf.Abs(deltaY) > 0.001f)
            _currentPet.transform.position += Vector3.up * deltaY;
    }

    private bool IsPetGeometryRenderer(Renderer renderer)
    {
        if (renderer == null || !renderer.enabled || GroundBlobShadow.IsShadowRenderer(renderer))
            return false;

        // Elemental variants add particles, rays and a ground sprite below the model.
        // Those effects must not affect the feet-to-ground calculation.
        if (renderer is ParticleSystemRenderer ||
            renderer is SpriteRenderer ||
            renderer is TrailRenderer ||
            renderer is LineRenderer)
        {
            return false;
        }

        Transform petRoot = _currentPet != null ? _currentPet.transform : null;
        for (Transform current = renderer.transform;
             current != null && current != petRoot;
             current = current.parent)
        {
            if (current.name.StartsWith("Element", StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    private void ScheduleGroundAlignment()
    {
        StopGroundAlignmentRoutine();
        if (isActiveAndEnabled && _currentPet != null)
            _groundAlignmentRoutine = StartCoroutine(AlignCurrentPetAfterAnimationFrame());
    }

    private IEnumerator AlignCurrentPetAfterAnimationFrame()
    {
        // Let a newly instantiated Animator evaluate its initial pose before the
        // final grounding pass. The immediate pass in CheckScale still prevents
        // a visibly misplaced first frame.
        yield return null;
        yield return new WaitForEndOfFrame();
        AlignCurrentPetToGround();
        _groundAlignmentRoutine = null;
    }

    private void StopGroundAlignmentRoutine()
    {
        if (_groundAlignmentRoutine == null)
            return;

        StopCoroutine(_groundAlignmentRoutine);
        _groundAlignmentRoutine = null;
    }

    private void ChangeActivePet(int variantIndex)
    {
        if (_remoteMode) return;
        if (variantIndex < 0 || variantIndex > _maxAvailablePetIdx) return;
        int previousVariant = _currentPetIdx;
        if (previousVariant == variantIndex)
            return;
        SetPet(variantIndex);
        G.Sound?.Play(GameAudioId.SFX_PET_SELECT);
        GameAnalytics.Track(AnalyticsEventNames.BigPetVariantSelected, GameAnalytics.Params(
            "big_pet_id", gameObject.name,
            "variant_before", previousVariant,
            "variant_after", variantIndex,
            "big_pet_level", _currentLvl,
            "source", "big_pet_variant_ui",
            "result", "success"));
    }

    private void GetIncome()
    {
        CollectIncome();
    }

    public double CollectIncome(bool playAudio = true, bool grantCurrency = true)
    {
        if (_remoteMode) return 0d;
        if (!_purchased) return 0d;

        var collected = Math.Max(0d, _accumulatedIncome);
        if (collected <= 0d)
            return 0d;

        bool hadOfflineIncome = _hasOfflineIncomePending;
        bool playOfflineIncome = playAudio && hadOfflineIncome;
        if (grantCurrency && !TryAddCoins(collected, playAudio && !playOfflineIncome))
            return 0d;
        if (playOfflineIncome)
            G.Sound?.Play(GameAudioId.SFX_OFFLINE_INCOME);
        _hasOfflineIncomePending = false;

        _accumulatedIncome = 0;
        if (_petInfoUI != null)
            _petInfoUI.UpdateIncome(_accumulatedIncome);

        var nowTs = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        _lastIncomeCollectTimestamp = DateTimeOffset.FromUnixTimeSeconds(nowTs).UtcDateTime;
        G.Save.SaveBigPetIncomeTime(nowTs.ToString(CultureInfo.InvariantCulture));
        if (playAudio && G.Sound == null && _audio)
            _audio.Play();

        var incomeParameters = GameAnalytics.Params(
            "source_type", "big_pet",
            "source_id", gameObject.name,
            "amount", collected,
            "collection_mode", AnalyticsContext.IsIncomeBatch ? AnalyticsContext.IncomeBatchMode : "manual",
            "offline_income", hadOfflineIncome,
            "currency_type", "coins",
            "result", "success");
        GameAnalytics.TrackOnce("first_income_collected", AnalyticsEventNames.FirstIncomeCollected, incomeParameters);
        if (!AnalyticsContext.IsIncomeBatch)
            GameAnalytics.Track(AnalyticsEventNames.IncomeCollected, incomeParameters);

        return collected;
    }

    public void RefreshAccumulatedIncomeFromClock()
    {
        if (_remoteMode || !_purchased)
            return;

        long nowTs = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        long lastTs = _lastIncomeCollectTimestamp == default
            ? nowTs
            : new DateTimeOffset(_lastIncomeCollectTimestamp).ToUnixTimeSeconds();
        if (lastTs <= 0L || lastTs > nowTs)
            lastTs = nowTs;

        long elapsed = OfflineRewardRules.ClampAccrualSeconds(nowTs - lastTs);
        _accumulatedIncome = Math.Max(0d, Math.Round(ApplyOfflineShopMultiplier(
            elapsed * _currentIncome)));
        _accumulatedIncome = double.IsInfinity(_accumulatedIncome)
            ? float.MaxValue
            : Math.Min(_accumulatedIncome, MaxAccumulatedIncome);
        _hasOfflineIncomePending = elapsed >= 60L && _accumulatedIncome > 0d;

        if (_petInfoUI != null)
        {
            _petInfoUI.UpdateIncome(_accumulatedIncome);
            if (elapsed > 0L)
                _petInfoUI.UpdateOfflineIncome(_accumulatedIncome);
        }
    }

    private static bool TryAddCoins(double amount, bool playAudio)
    {
        if (amount <= 0d)
            return false;

        if (G.Income != null)
            return G.Income.TryAddCoins(amount, playAudio);

        if (G.Currency != null)
        {
            G.Currency.AddCurrency(CurrencyType.Coins, amount, playAudio);
            return true;
        }

        Debug.LogWarning("[BigPetPoint] Cannot collect income: income and currency managers are not initialized.");
        return false;
    }

    private static double ApplyOfflineShopMultiplier(double amount)
    {
        return G.ShopEffects != null ? G.ShopEffects.ApplyOfflineIncome(amount) : amount;
    }

    private IEnumerator ProduceIncome()
    {
        if (_remoteMode) yield break;

        while (true)
        {
            yield return new WaitForSecondsRealtime(1f);
            if (!_purchased)
                continue;

            _accumulatedIncome += _currentIncome;
            _accumulatedIncome = double.IsInfinity(_accumulatedIncome)
                ? float.MaxValue
                : Math.Min(_accumulatedIncome, MaxAccumulatedIncome);
            _accumulatedIncome = Math.Round(_accumulatedIncome);
            if (_petInfoUI != null)
                _petInfoUI.UpdateIncome(_accumulatedIncome);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_remoteMode || !_purchased) return;
        if (!other.CompareTag("Player")) return;
        if (_petInfoUI != null)
            _petInfoUI.gameObject.SetActive(true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (_remoteMode || !_purchased) return;
        if (!other.CompareTag("Player")) return;
        if (_petInfoUI != null)
            _petInfoUI.gameObject.SetActive(false);
    }

    public void ApplyRemoteState(int petId, int lvl, int xp, bool purchased = true)
    {
        _remoteMode = true;
        StopIncomeRoutine();
        SetQuickAccessBinding(false);
        HideRemoteUI();
        CacheSceneRefs();

        _purchased = purchased;
        if (!ResolvePetList())
        {
            _currentIncome = 0d;
            _accumulatedIncome = 0d;
            return;
        }

        if (!_purchased)
        {
            if (_currentPet != null)
            {
                Destroy(_currentPet.gameObject);
                _currentPet = null;
            }
            _currentIncome = 0d;
            _accumulatedIncome = 0d;
            return;
        }

        _maxLvl = GetBigPetVariantCount() * Mathf.Max(1, _lvlsPerPet);
        _currentLvl = Mathf.Clamp(lvl, 1, _maxLvl);
        _xpForNextLvl = GetXpRequiredForLevel(_currentLvl);
        _currentXp = _currentLvl >= _maxLvl
            ? 0
            : Mathf.Clamp(xp, 0, Mathf.Max(0, _xpForNextLvl - 1));
        _maxAvailablePetIdx = GetMaxAvailablePetIndexForLevel(_currentLvl);

        if (_setPetUI != null)
            _setPetUI.SetMaxAvailablePet(_maxAvailablePetIdx);

        petId = Mathf.Clamp(petId, 0, _maxAvailablePetIdx);
        SetPet(petId);

        _currentIncome = GetVariantIncome(_maxAvailablePetIdx);
        if (_petInfoUI != null)
        {
            _petInfoUI.SetInfo(_currentIncome);
            _petInfoUI.UpdateIncome(0);
        }

        if (_feedButton != null) _feedButton.SetActive(false);
        if (_foodTimeBar != null) _foodTimeBar.gameObject.SetActive(false);
    }

    public void ApplyRemoteDefaultState(int petId = 0, int lvl = 1, int xp = 0, bool purchased = false)
    {
        _remoteMode = true;
        HideRemoteUI();

        if (!ResolvePetList())
            return;

        ApplyRemoteState(petId, lvl, xp, purchased);
    }

    public void SetRemoteMode(bool remote)
    {
        _remoteMode = remote;
        if (_setPetUI == null)
            _setPetUI = GetComponentInChildren<BigPetSetUI>(true);
        if (_setPetUI != null)
            _setPetUI.SetRemoteMode(remote);

        if (_remoteMode)
        {
            StopIncomeRoutine();
            SetQuickAccessBinding(false);
            HideRemoteUI();
        }
        else
        {
            if (!_initializedLocal)
            {
                InitLocal();
            }
            else
            {
                ReloadLocalStateFromSave();
            }

            if (_purchased)
                ShowLocalUI();
        }
    }

    private void ReloadLocalStateFromSave()
    {
        if (_setPetUI == null)
            _setPetUI = GetComponentInChildren<BigPetSetUI>(true);
        if (_setPetUI != null)
        {
            _setPetUI.SetRemoteMode(false);
            BindPetSelection();
        }

        CacheSceneRefs();
        _purchased = ResolvePurchaseState();
        if (_purchased)
            InitPurchasedState();
        else
            PrepareLockedState();
    }

    private void BindPetSelection()
    {
        if (_setPetUI == null)
            return;

        _setPetUI.PetSlotClicked.RemoveListener(ChangeActivePet);
        _setPetUI.PetSlotClicked.AddListener(ChangeActivePet);
    }

    public void _TryBuy()
    {
        if (_remoteMode || _purchased)
            return;

        bool success = G.Currency.RemoveCurrency(CurrencyType.Coins, _unlockPrice);
        if (!success)
        {
            TrackPurchaseResult(false);
            return;
        }

        _purchased = true;
        G.Save.SaveBigPetStatus(true);
        InitPurchasedState();
        LocalLevelChanged?.Invoke(CurrentLevel);
        BaseDirtyTracker.MarkDirty();
        TutorialSignals.Raise(TutorialSignalType.BigPetPurchased, this, value: _unlockPrice);
        G.Sound?.Play(GameAudioId.SFX_UNLOCK_MAJOR);
        TrackPurchaseResult(true);

        if (_playerInArea)
        {
            SetQuickAccessBinding(true);
            CheckPlayer(G.QuickAccess != null ? G.QuickAccess.CurrentActive : null);
        }
    }

    private void TrackPurchaseResult(bool success)
    {
        GameAnalytics.Track(AnalyticsEventNames.BigPetPurchaseResult, GameAnalytics.Params(
            "big_pet_id", gameObject.name,
            "currency_type", "coins",
            "price", _unlockPrice,
            "source", "big_pet_buy_panel",
            "result", success ? "success" : "failed",
            "failure_reason", success ? string.Empty : "insufficient_currency"));
    }

    private void CacheSceneRefs()
    {
        if (_changePetArea == null)
        {
            var area = transform.Find("ChangePetArea");
            if (area != null)
                _changePetArea = area.gameObject;
        }

        if (_changePetArea == null)
            return;

        InteractionRaycastListener listener = _changePetArea.GetComponent<InteractionRaycastListener>();
        if (listener != null)
            listener.MaxDistance = Mathf.Max(0.5f, _changePetInteractionDistance);

        BoxCollider interactionCollider = _changePetArea.GetComponent<BoxCollider>();
        if (interactionCollider != null)
        {
            interactionCollider.isTrigger = true;
            FitChangePetColliderToPad(interactionCollider);
        }

        if (_setPetUI != null)
            _setPetUI.ConfigureWorldInteraction(interactionCollider, _changePetInteractionDistance);
    }

    private static void FitChangePetColliderToPad(BoxCollider interactionCollider)
    {
        if (interactionCollider == null)
            return;

        Transform area = interactionCollider.transform;
        Renderer[] renderers = area.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        Bounds localBounds = default;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled)
                continue;

            Bounds worldBounds = renderer.bounds;
            Vector3 min = worldBounds.min;
            Vector3 max = worldBounds.max;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 worldPoint = new Vector3(
                    (corner & 1) == 0 ? min.x : max.x,
                    (corner & 2) == 0 ? min.y : max.y,
                    (corner & 4) == 0 ? min.z : max.z);
                Vector3 localPoint = area.InverseTransformPoint(worldPoint);
                if (!hasBounds)
                {
                    localBounds = new Bounds(localPoint, Vector3.zero);
                    hasBounds = true;
                }
                else
                {
                    localBounds.Encapsulate(localPoint);
                }
            }
        }

        if (!hasBounds)
            return;

        const float horizontalPadding = 0.08f;
        const float verticalPadding = 0.12f;
        interactionCollider.center = localBounds.center;
        interactionCollider.size = new Vector3(
            Mathf.Max(0.5f, localBounds.size.x + horizontalPadding),
            Mathf.Max(0.25f, localBounds.size.y + verticalPadding),
            Mathf.Max(0.5f, localBounds.size.z + horizontalPadding));
    }

    private bool ResolvePurchaseState()
    {
        var purchased = G.Save.LoadBigPetStatus();
        if (purchased)
            return true;

        // Backward compatibility for saves created before purchase gating.
        var legacyLevel = G.Save.LoadBigPetLvl();
        var legacyXp = G.Save.LoadBigPetXP();
        var legacyPetId = G.Save.LoadBigPetId();
        if (legacyLevel > 1 || legacyXp > 0 || legacyPetId > 0)
        {
            purchased = true;
            G.Save.SaveBigPetStatus(true);
        }

        return purchased;
    }

    private void InitPurchasedState()
    {
        if (!ResolvePetList())
            return;

        _maxLvl = GetBigPetVariantCount() * Mathf.Max(1, _lvlsPerPet);
        _currentLvl = Mathf.Clamp(G.Save.LoadBigPetLvl(), 1, _maxLvl);
        _currentXp = Mathf.Max(0, G.Save.LoadBigPetXP());
        _xpForNextLvl = GetXpRequiredForLevel(_currentLvl);
        _maxAvailablePetIdx = GetMaxAvailablePetIndexForLevel(_currentLvl);
        _currentPetIdx = Mathf.Clamp(G.Save.LoadBigPetId(), 0, _maxAvailablePetIdx);

        if (_setPetUI != null)
        {
            _setPetUI.gameObject.SetActive(true);
            _setPetUI.InitUI(_activePets);
            _setPetUI.SetMaxAvailablePet(_maxAvailablePetIdx);
        }

        if (_changePetArea != null)
            _changePetArea.SetActive(true);

        if (_xpProgressBar != null)
            _xpProgressBar.gameObject.SetActive(true);
        if (_xpProgressText != null)
            _xpProgressText.gameObject.SetActive(true);

        SetPet(_currentPetIdx);
        CheckLvl();

        _currentIncome = GetVariantIncome(_maxAvailablePetIdx);
        if (_petInfoUI != null)
        {
            _petInfoUI.SetInfo(_currentIncome);
            _petInfoUI.gameObject.SetActive(true);
        }

        if (_foodTimeBar != null)
            _foodTimeBar.gameObject.SetActive(false);
        if (_foodTimeBarText != null)
            _foodTimeBarText.gameObject.SetActive(false);

        if (_buyPanel != null)
            _buyPanel.gameObject.SetActive(false);

        long incomeAccumulateTime;
        string timestamp = G.Save.LoadBigPetIncomeTime();
        var nowTs = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (!TryParseIncomeTimestamp(timestamp, out var lastCollectTs))
        {
            lastCollectTs = nowTs;
            G.Save.SaveBigPetIncomeTime(lastCollectTs.ToString(CultureInfo.InvariantCulture));
        }

        if (lastCollectTs > nowTs)
            lastCollectTs = nowTs;

        _lastIncomeCollectTimestamp = DateTimeOffset.FromUnixTimeSeconds(lastCollectTs).UtcDateTime;
        incomeAccumulateTime = OfflineRewardRules.ClampAccrualSeconds(nowTs - lastCollectTs);

        _accumulatedIncome = Math.Max(0d, ApplyOfflineShopMultiplier(
            incomeAccumulateTime * _currentIncome));
        _hasOfflineIncomePending = incomeAccumulateTime >= 60L && _accumulatedIncome > 0d;
        if (_petInfoUI != null)
        {
            _petInfoUI.UpdateIncome(_accumulatedIncome);
            if (incomeAccumulateTime > 0)
                _petInfoUI.UpdateOfflineIncome(_accumulatedIncome);
        }

        EnsureIncomeRoutine();
    }

    private bool ResolvePetList()
    {
        _activePets.Clear();

        var storagePets = G.Storage != null ? G.Storage.GetAllPetPrefabs() : null;
        AddValidBigPets(storagePets, _activePets);

        if (_activePets.Count == 0)
            AddValidBigPets(_pets, _activePets);

        _activePets.Sort(CompareBigPetAnimals);
        return _activePets.Count > 0;
    }

    private static void AddValidBigPets(IReadOnlyList<Brainrot> source, List<Brainrot> destination)
    {
        if (source == null || destination == null)
            return;

        for (int i = 0; i < source.Count; i++)
        {
            var pet = source[i];
            if (!IsValidBigPetAnimal(pet))
                continue;

            if (!destination.Contains(pet))
                destination.Add(pet);
        }
    }

    private static bool IsValidBigPetAnimal(Brainrot pet)
    {
        if (pet == null)
            return false;

        if (ExcludedBigPetIds.Contains(GetPetId(pet)))
            return false;

        return pet.Data.StartIncome > 0d;
    }

    private static int CompareBigPetAnimals(Brainrot left, Brainrot right)
    {
        if (ReferenceEquals(left, right))
            return 0;
        if (left == null)
            return 1;
        if (right == null)
            return -1;

        var incomeCompare = left.Data.StartIncome.CompareTo(right.Data.StartIncome);
        if (incomeCompare != 0)
            return incomeCompare;

        return string.Compare(GetPetId(left), GetPetId(right), StringComparison.OrdinalIgnoreCase);
    }

    private static string GetPetId(Brainrot pet)
    {
        if (pet == null)
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(pet.Name))
            return pet.Name.Trim();

        if (!string.IsNullOrWhiteSpace(pet.gameObject.name))
            return pet.gameObject.name.Replace("(Clone)", string.Empty).Trim();

        return pet.name != null ? pet.name.Replace("(Clone)", string.Empty).Trim() : string.Empty;
    }

    private int GetMaxAvailablePetIndexForLevel(int level)
    {
        if (_activePets == null || _activePets.Count == 0)
            return 0;

        var levelsPerPet = Mathf.Max(1, _lvlsPerPet);
        var safeLevel = Mathf.Max(1, level);
        return Mathf.Clamp((safeLevel - 1) / levelsPerPet, 0, GetBigPetVariantCount() - 1);
    }

    private int GetBigPetVariantCount()
    {
        return Mathf.Max(1, _activePets.Count * ProgressionElements.Length);
    }

    private int GetBasePetIndex(int variantIndex)
    {
        if (_activePets == null || _activePets.Count == 0)
            return 0;

        return Mathf.Clamp(variantIndex, 0, GetBigPetVariantCount() - 1) % _activePets.Count;
    }

    private ElementType GetVariantElement(int variantIndex)
    {
        if (_activePets == null || _activePets.Count == 0)
            return ElementType.NoElement;

        int elementIndex = Mathf.Clamp(
            variantIndex / _activePets.Count,
            0,
            ProgressionElements.Length - 1);
        return ProgressionElements[elementIndex];
    }

    private double GetVariantIncome(int variantIndex)
    {
        if (_activePets == null || _activePets.Count == 0)
            return 0d;

        Brainrot pet = _activePets[GetBasePetIndex(variantIndex)];
        if (pet == null)
            return 0d;

        ElementType element = GetVariantElement(variantIndex);
        float multiplier = G.Elements != null ? G.Elements.GetMultiplaer(element) : 1f;
        return Math.Max(0d, pet.Data.StartIncome * multiplier);
    }

    private static bool TryParseIncomeTimestamp(string raw, out long timestamp)
    {
        timestamp = 0;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        if (long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out timestamp))
            return true;

        if (DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dto))
        {
            timestamp = dto.ToUnixTimeSeconds();
            return true;
        }

        if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dtInvariant))
        {
            timestamp = new DateTimeOffset(dtInvariant.ToUniversalTime()).ToUnixTimeSeconds();
            return true;
        }

        if (DateTime.TryParse(raw, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out var dtCurrent))
        {
            timestamp = new DateTimeOffset(dtCurrent.ToUniversalTime()).ToUnixTimeSeconds();
            return true;
        }

        return false;
    }

    private void PrepareLockedState()
    {
        StopIncomeRoutine();
        SetQuickAccessBinding(false);
        _currentIncome = 0d;
        _accumulatedIncome = 0d;

        if (_currentPet != null)
        {
            Destroy(_currentPet.gameObject);
            _currentPet = null;
        }

        if (_setPetUI != null)
        {
            _setPetUI.OpenUI(false);
            _setPetUI.gameObject.SetActive(false);
        }

        if (_petInfoUI != null)
        {
            _petInfoUI.UpdateIncome(0d);
            _petInfoUI.gameObject.SetActive(false);
        }

        if (_changePetArea != null)
            _changePetArea.SetActive(false);

        ApplyLockedUiState(false);
    }

    private void ApplyLockedUiState(bool showBuyPanel)
    {
        SetQuickAccessBinding(false);

        if (_feedButton != null)
            _feedButton.SetActive(false);
        if (_foodTimeBar != null)
            _foodTimeBar.gameObject.SetActive(false);
        if (_foodTimeBarText != null)
            _foodTimeBarText.gameObject.SetActive(false);
        if (_xpProgressBar != null)
            _xpProgressBar.gameObject.SetActive(false);
        if (_xpProgressText != null)
            _xpProgressText.gameObject.SetActive(false);
        if (_setPetUI != null)
            _setPetUI.gameObject.SetActive(false);
        if (_petInfoUI != null)
            _petInfoUI.gameObject.SetActive(false);
        if (_changePetArea != null)
            _changePetArea.SetActive(false);

        if (_buyPanel != null)
        {
            _buyPanel.SetInfoLocalized("Buy", "Buy", Math.Round(_unlockPrice).ToString("0", CultureInfo.InvariantCulture));
            _buyPanel.gameObject.SetActive(showBuyPanel);
        }
    }

    private void EnsureIncomeRoutine()
    {
        if (_remoteMode || !_purchased || _incomeRoutine != null)
            return;
        if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
            return;

        _incomeRoutine = StartCoroutine(ProduceIncome());
    }

    private void StopIncomeRoutine()
    {
        if (_incomeRoutine == null)
            return;

        StopCoroutine(_incomeRoutine);
        _incomeRoutine = null;
    }

    private void SetQuickAccessBinding(bool enabled)
    {
        if (G.QuickAccess == null)
            return;

        if (enabled)
        {
            if (_quickAccessBound)
                return;

            G.QuickAccess.SwitchActiveItem.AddListener(CheckPlayer);
            _quickAccessBound = true;
            return;
        }

        if (!_quickAccessBound)
            return;

        G.QuickAccess.SwitchActiveItem.RemoveListener(CheckPlayer);
        _quickAccessBound = false;
    }
}
