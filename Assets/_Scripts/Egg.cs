using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;


[System.Serializable]
public struct EggBrainrotDrop
{
    public Brainrot Brainrot;
    public float Weight;
}

[System.Serializable]
public struct EggData
{
    public int Luck;
    public float Price;
    public int SecondsToHatching;
    public List<Brainrot> Brainrots;
    public List<EggBrainrotDrop> BrainrotDrops;
    public BrainrotDinamicData DinamicData;
    //public float Weight;
}
public class Egg : InventoryItem
{
    private const int ElementVfxDefaultsVersion = 2;
    private const float DefaultElementParticleSizeMultiplier = 6f;
    private const float DefaultElementParticleEmissionMultiplier = 3f;
    private const float DefaultElementParticleRadiusMultiplier = 0.7f;
    private const float DefaultElementRayAlphaMultiplier = 2f;
    private const float DefaultElementRayLength = 5f;
    private const float DefaultElementRayThickness = 5f;
    private const float DefaultElementRaySpinSpeed = 35f;
    private const float DefaultElementGroundAlphaMultiplier = 2f;
    private const float DefaultElementGroundRadius = 2f;
    private const float DefaultElementGroundYOffset = 0.02f;

    [SerializeField] private EggData _data;
    [SerializeField] private float _animationHatchingTime;
    [SerializeField] private float _animationHatchingSpeed;
    [SerializeField] private AnimationCurve _animationCurve = new AnimationCurve();
    [SerializeField] private GameObject _mesh;
    [SerializeField] private Transform _rouleteModelsParent;
    [SerializeField] private Material _rouleteMaterial;
    [Header("Element VFX")]
    [SerializeField] private float _elementParticleSizeMultiplier = DefaultElementParticleSizeMultiplier;
    [SerializeField] private float _elementParticleEmissionMultiplier = DefaultElementParticleEmissionMultiplier;
    [SerializeField] private float _elementParticleRadiusMultiplier = DefaultElementParticleRadiusMultiplier;
    [SerializeField] private float _elementRayAlphaMultiplier = DefaultElementRayAlphaMultiplier;
    [SerializeField] private float _elementRayLength = DefaultElementRayLength;
    [SerializeField] private float _elementRayThickness = DefaultElementRayThickness;
    [SerializeField] private float _elementRaySpinSpeed = DefaultElementRaySpinSpeed;
    [SerializeField] private float _elementGroundAlphaMultiplier = DefaultElementGroundAlphaMultiplier;
    [SerializeField] private float _elementGroundRadius = DefaultElementGroundRadius;
    [SerializeField] private float _elementGroundYOffset = DefaultElementGroundYOffset;
    [SerializeField, HideInInspector] private int _elementVfxDefaultsVersion;

    private List<GameObject> _rouleteObjects;
    private EggInfoUI _infoUI;
    private InteractionPanel _buyPanel;
    private EggStatus _status;
    private FieldCell _currentCell;
    //private int _currentHatchingTime;
    private long _hatchingTimectamp;
    private bool _initialized;
    private bool _remoteConveyorPurchase;
    private bool _purchaseInProgress;
    private bool _speedBoostAdInProgress;

    private int _totalDurationSec;      // РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р… РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р… РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…
    private DateTimeOffset _endUtc;           // РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р… РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р… (UTC)
    private Coroutine _ticker;
    private string SaveKey => $"egg_endUtc_{_currentCell.Id}";//РїС—Р…РїС—Р…РїС—Р…РїС—Р… РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р… РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р… РїС—Р… РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р… id РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…/РїС—Р…РїС—Р…РїС—Р…РїС—Р…)

    public long HatchingTime => _hatchingTimectamp;
    public EggStatus Status => _status;
    public EggData Data { get { return _data; } }
    public double EffectivePrice => GetPriceForElement(_data.DinamicData.ElementType);
    [HideInInspector] public UnityEvent<Egg> EggPurchased;

    public double GetPriceForElement(ElementType elementType)
    {
        return Math.Max(0d, _data.Price) * GetElementMultiplier(elementType);
    }

    private void Awake()
    {
        ApplyElementVfxDefaultsIfNeeded();

        if (!_initialized)
        {
            Init();
        }
    }

    private void OnEnable()
    {
        if (_initialized && _status == EggStatus.Maturing && _currentCell != null && _ticker == null)
            StartTicker();
    }

    private void OnDisable()
    {
        StopTicker();
    }

    private void OnValidate()
    {
        ApplyElementVfxDefaultsIfNeeded();
        ClampElementVfxSettings();

        if (Application.isPlaying && isActiveAndEnabled && _initialized)
            RefreshElementVfx();
    }

    [ContextMenu("Refresh Element VFX")]
    private void RefreshElementVfx()
    {
        ClampElementVfxSettings();
        if (!Application.isPlaying || !_initialized)
            return;

        SetTypeVisual();
    }

    private void ClampElementVfxSettings()
    {
        _elementParticleSizeMultiplier = Mathf.Max(0.1f, _elementParticleSizeMultiplier);
        _elementParticleEmissionMultiplier = Mathf.Max(0.1f, _elementParticleEmissionMultiplier);
        _elementParticleRadiusMultiplier = Mathf.Max(0.1f, _elementParticleRadiusMultiplier);
        _elementRayAlphaMultiplier = Mathf.Max(0f, _elementRayAlphaMultiplier);
        _elementRayLength = Mathf.Max(0f, _elementRayLength);
        _elementRayThickness = Mathf.Max(0f, _elementRayThickness);
        _elementGroundAlphaMultiplier = Mathf.Max(0f, _elementGroundAlphaMultiplier);
        _elementGroundRadius = Mathf.Max(0f, _elementGroundRadius);
    }

    private void ApplyElementVfxDefaultsIfNeeded()
    {
        if (_elementVfxDefaultsVersion >= ElementVfxDefaultsVersion)
            return;

        _elementParticleSizeMultiplier = DefaultElementParticleSizeMultiplier;
        _elementParticleEmissionMultiplier = DefaultElementParticleEmissionMultiplier;
        _elementParticleRadiusMultiplier = DefaultElementParticleRadiusMultiplier;
        _elementRayAlphaMultiplier = DefaultElementRayAlphaMultiplier;
        _elementRayLength = DefaultElementRayLength;
        _elementRayThickness = DefaultElementRayThickness;
        _elementRaySpinSpeed = DefaultElementRaySpinSpeed;
        _elementGroundAlphaMultiplier = DefaultElementGroundAlphaMultiplier;
        _elementGroundRadius = DefaultElementGroundRadius;
        _elementGroundYOffset = DefaultElementGroundYOffset;
        _elementVfxDefaultsVersion = ElementVfxDefaultsVersion;
    }

    private void Init()
    {
        ApplyElementVfxDefaultsIfNeeded();

        _infoUI = GetComponentInChildren<EggInfoUI>();
        _buyPanel = GetComponentInChildren<InteractionPanel>();
        ApplyBuyPanelAdBadge();
        _buyPanel.gameObject.SetActive(false);
        _status = EggStatus.Conveyer;
        _infoUI.SetStatus(_status);
        _rouleteObjects = new List<GameObject>();
        SetRouletteModels();
        _initialized = true;
    }

    public void SetRandomData()
    {
        if (!_initialized)
        {
            Init();
        }
        _data.DinamicData.ElementType = G.Elements != null ? G.Elements.GetRandomWeighted() : ElementType.NoElement;
        SetTypeVisual();
        _infoUI?.SetInfo(this);
    }

    public void SetData(BrainrotDinamicData data)
    {
        if (!_initialized)
        {
            Init();
        }
        _data.DinamicData = data;
        SetTypeVisual();
        _infoUI?.SetInfo(this);
    }

    public void SetConveyorPurchaseMode(bool remoteRewardPurchase)
    {
        _remoteConveyorPurchase = remoteRewardPurchase;
        ApplyBuyPanelAdBadge();
    }
    private void SetTypeVisual()
    {
        ElementTypeVfx.Ensure(
            this,
            transform,
            _data.DinamicData.ElementType,
            sizeMultiplier: _elementParticleSizeMultiplier,
            emissionMultiplier: _elementParticleEmissionMultiplier,
            rayAlphaMultiplier: _elementRayAlphaMultiplier,
            particleRadiusMultiplier: _elementParticleRadiusMultiplier,
            rayLengthWorldOverride: _elementRayLength,
            rayThicknessWorldOverride: _elementRayThickness,
            raySpinSpeed: _elementRaySpinSpeed,
            groundAlphaMultiplier: _elementGroundAlphaMultiplier,
            groundRadiusWorldOverride: _elementGroundRadius,
            groundYOffsetWorld: _elementGroundYOffset);
    }
    private void OnTriggerEnter(Collider other)
    {
        if (_status != EggStatus.Conveyer) return;
        if (other.CompareTag("Player"))
        {
            ApplyBuyPanelAdBadge();
            _buyPanel.gameObject.SetActive(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (_status != EggStatus.Conveyer) return;
        if (other.CompareTag("Player"))
        {
            _buyPanel.gameObject.SetActive(false);
        }
    }

    public void TryBuy()
    {
        if (_purchaseInProgress) return;

        if (_remoteConveyorPurchase)
        {
            if (G.Ad == null) return;

            _purchaseInProgress = true;
            G.Ad.ShowRewardedAd("ConveyorRemoteEgg", success =>
            {
                _purchaseInProgress = false;
                if (!success) return;
                G.Inventory.Add(this);
                EggPurchased.Invoke(this);
            });
            return;
        }

        if (G.Currency.RemoveCurrency(CurrencyType.Coins, EffectivePrice))
        {
            G.Inventory.Add(this);
            EggPurchased.Invoke(this);
        }
    }
    public void InitTimer(FieldCell field)
    {
        _status = EggStatus.Maturing;
        _infoUI?.SetInfo(this);
        _infoUI?.SetStatus(_status);
        _currentCell = field;
        _totalDurationSec = Mathf.RoundToInt(
            _data.SecondsToHatching * GetElementMultiplier()
        );

        // РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р… РїС—Р…РїС—Р… РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р… РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…

        _currentCell.SpeedBoost.RemoveListener(SpeedBoostAd);
        _currentCell.SpeedBoost.AddListener(SpeedBoostAd);
        //_currentCell.SpeedBoost.AddListener(SpeedBoostInstant);     // debug instant hatch
        //_currentCell.InstantHatch.AddListener(SpeedBoostInstant); // РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…: РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…

        /* РїС—Р…РїС—Р…РїС—Р… РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р… РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р… РїС—Р… РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р… РїС—Р…РїС—Р… РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р… РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р… РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…
        if (PlayerPrefs.HasKey(SaveKey))
        {
            long ticks = long.Parse(PlayerPrefs.GetString(SaveKey));
            _endUtc = new DateTime(ticks, DateTimeKind.Utc);
        }
        else
        {
            _endUtc = DateTime.UtcNow.AddSeconds(_totalDurationSec);
            PlayerPrefs.SetString(SaveKey, _endUtc.Ticks.ToString());
            PlayerPrefs.Save();
        }
       */
        _endUtc = DateTimeOffset.UtcNow.AddSeconds(_totalDurationSec); // РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р… РїС—Р…РїС—Р…РїС—Р… РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…
        _hatchingTimectamp = _endUtc.ToUnixTimeSeconds();


        StartTicker();

    }
    /// <summary>
    /// РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р… РїС—Р…РїС—Р…РїС—Р…РїС—Р…: РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р… 30 РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р… РїС—Р…РїС—Р… РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р… РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р….
    /// </summary>
    public void SpeedBoostAd()
    {
        if (_status != EggStatus.Maturing || _speedBoostAdInProgress)
            return;

        if (G.Ad == null)
        {
            Debug.LogWarning("[Egg] Ads manager is missing, speed boost ad skipped.");
            return;
        }

        _speedBoostAdInProgress = true;
        G.Ad.ShowRewardedAd("EggSpeedBoost", success =>
        {
            _speedBoostAdInProgress = false;
            if (!success || _status != EggStatus.Maturing)
                return;

            ApplySpeedBoostSeconds(30 * 60);
        });
    }

    private void ApplySpeedBoostSeconds(int seconds)
    {
        _endUtc = _endUtc.AddSeconds(-Mathf.Max(1, seconds));

        DateTimeOffset now = DateTimeOffset.UtcNow;
        if (_endUtc < now)
            _endUtc = now;

        _hatchingTimectamp = _endUtc.ToUnixTimeSeconds();
        _currentCell?.SaveData();
    }

    public bool TryReduceHatchingTime(int seconds)
    {
        if (seconds <= 0 || _status != EggStatus.Maturing || _currentCell == null || _currentCell.IsRemoteMode)
            return false;

        ApplySpeedBoostSeconds(seconds);
        return true;
    }
    /// <summary>
    /// РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р… РїС—Р…РїС—Р…РїС—Р…РїС—Р…: РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р… РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р….
    /// </summary>
    public void SpeedBoostInstant()
    {
        _endUtc = DateTimeOffset.UtcNow;
        _hatchingTimectamp = _endUtc.ToUnixTimeSeconds();
        _currentCell?.SaveData();
        //SaveDeadline();
    }
    public void SpeedBoost()
    {
        //_currentHatchingTime = 0; //РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р… РїС—Р…РїС—Р… РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р… РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…, РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р… РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р… - РїС—Р…РїС—Р… РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р… - 30 РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…, РїС—Р…РїС—Р… РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р… РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…
    }

    public void InitTimer(FieldCell cell, DateTimeOffset endTime)
    {
        _status = EggStatus.Maturing;
        _infoUI?.SetInfo(this);
        _infoUI?.SetStatus(_status);
        _currentCell = cell;
        _currentCell.SpeedBoost.RemoveListener(SpeedBoostAd);
        _currentCell.SpeedBoost.AddListener(SpeedBoostAd);

        _totalDurationSec = Mathf.RoundToInt(
            _data.SecondsToHatching * GetElementMultiplier()
        );
        DateTimeOffset now = DateTimeOffset.UtcNow;
        _endUtc = endTime.ToUnixTimeSeconds() > 0 ? endTime : now.AddSeconds(_totalDurationSec);
        _hatchingTimectamp = _endUtc.ToUnixTimeSeconds();
        StartTicker();
    }

    private float GetElementMultiplier()
    {
        return GetElementMultiplier(_data.DinamicData.ElementType);
    }

    private static float GetElementMultiplier(ElementType elementType)
    {
        return G.Elements != null ? G.Elements.GetMultiplaer(elementType) : 1f;
    }

    private void StartTicker()
    {
        StopTicker();
        if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
            return;

        _ticker = StartCoroutine(Ticker());
    }

    private void StopTicker()
    {
        if (_ticker == null)
            return;

        StopCoroutine(_ticker);
        _ticker = null;
    }

    private IEnumerator Ticker()
    {
        // РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р… РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р… РїС—Р…РїС—Р…РїС—Р… РїС—Р… 0.2РїС—Р…0.5РїС—Р… РїС—Р…РїС—Р…РїС—Р… РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р… РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…
        var wait = new WaitForSeconds(0.25f);

        while (true)
        {
            double remainingSec = (_endUtc - DateTime.UtcNow).TotalSeconds;

            if (remainingSec <= 0)
            {
                _infoUI?.ShowTimeUI(0, 1f); // 100% РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…
                _currentCell.HatchEgg.RemoveListener(Hatching);
                _currentCell.HatchEgg.AddListener(Hatching);
                _status = EggStatus.ReadyToHatch;
                _ticker = null;
                _currentCell.CheckPlayer();
                //Hatching();
                yield break;
            }
            float progress01 = _totalDurationSec <= 0 ? 1f : 1f - Mathf.Clamp01((float)(remainingSec / _totalDurationSec));
            _infoUI?.ShowTimeUI((int)Math.Ceiling(remainingSec), progress01);

            yield return wait;
        }
    }
    private void SaveDeadline()
    {
        if (_currentCell == null) return;
        PlayerPrefs.SetString(SaveKey, _endUtc.Ticks.ToString());
        PlayerPrefs.Save();
    }

    // РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р… РїС—Р…РїС—Р…РїС—Р… РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…/РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р… (РїС—Р…РїС—Р… РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…, РїС—Р…РїС—Р…РїС—Р…РїС—Р… endUtc РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…)
    private void OnApplicationPause(bool pause)
    {
        if (pause) SaveDeadline();
    }
    private void OnApplicationQuit()
    {
        SaveDeadline();
    }

    private void Hatching()
    {
        _status = EggStatus.Hatching;
        _infoUI.SetStatus(_status);
        _currentCell.CheckPlayer();
        /*
        PlayerPrefs.DeleteKey(SaveKey);
        PlayerPrefs.Save();
        */

        _currentCell.SpeedBoost.RemoveListener(SpeedBoost);
        _currentCell.SpeedBoost.RemoveListener(SpeedBoostAd);
        _currentCell.SpeedBoost.RemoveListener(SpeedBoostInstant);


        _currentCell.LockCell(true);
        StartCoroutine(ShowAnimation());
    }
    private IEnumerator ShowAnimation()
    {
        _mesh.SetActive(false);
        float startTime = _animationHatchingTime;
        //float frameTime = _animationHatchingTime / _animationHatchingSpeed;
        int idx = -1;
        while (_animationHatchingTime > 0) 
        {
            //РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р… РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р… РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р… РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р… РїС—Р…РїС—Р… РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…РїС—Р…
            if (idx>=0)
                _rouleteObjects[idx].SetActive(false);
            idx = (idx + 1) % _rouleteObjects.Count;
            _rouleteObjects[idx].SetActive(true);
            _animationHatchingTime -= _animationHatchingSpeed;
            _animationCurve.Evaluate(_animationHatchingTime/ startTime);
            yield return new WaitForSecondsRealtime(_animationHatchingSpeed);
        }
        SpawnBrainrot();
    }
    private void SpawnBrainrot()
    {
        Brainrot brainrotPrefab = GetRandomBrainrot();
        if (brainrotPrefab == null)
        {
            Debug.LogWarning("[Egg] Brainrot prefab is missing in roll, hatch aborted.");
            _currentCell.LockCell(false);
            Destroy(gameObject);
            return;
        }

        _data.DinamicData.WeightMultiplier = UnityEngine.Random.Range(1, brainrotPrefab.Data.MaxWeightMult);
        Brainrot brainrot = Instantiate(brainrotPrefab, _currentCell.transform, false);
        brainrot.transform.localPosition = Vector3.zero;
        brainrot.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        brainrot.Init(_data.DinamicData, _currentCell);
        brainrot.transform.SetParent(_currentCell.transform, false);
        brainrot.transform.localPosition = Vector3.zero;
        brainrot.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

        // Album progress should track hatched pets even before they are picked up.
        if (G.Album != null)
            G.Album.TryDiscover(AlbumEntityType.Animal, brainrot.Name, brainrot.DinamicData.ElementType);

        LocalPlayerStatsStore.IncrementHatched();
        _currentCell.UpdateFieldItem(Item.Brainrot);
        Destroy(gameObject);
        _currentCell.LockCell(false);
    }
    private Brainrot GetRandomBrainrot()
    {
        var picked = ConveyorDropChanceCalculator.PickRandomBrainrot(this, applyLuckBonus: true);
        if (picked != null)
            return picked;

        if (_data.Brainrots == null || _data.Brainrots.Count == 0)
            return null;

        var validAnimals = new List<Brainrot>();
        for (int i = 0; i < _data.Brainrots.Count; i++)
        {
            Brainrot candidate = _data.Brainrots[i];
            if (IsAnimalDrop(candidate))
                validAnimals.Add(candidate);
        }

        if (validAnimals.Count == 0)
            return null;

        int rand = UnityEngine.Random.Range(0, validAnimals.Count);
        return validAnimals[rand];
    }

    public static bool IsAnimalDrop(Brainrot candidate)
    {
        if (candidate == null)
            return false;

        if (G.Storage == null)
            return true;

        return G.Storage.ContainsPetPrefab(candidate);
    }

    public override void OnInventoryAdd() {
        _status = EggStatus.Purchased;
        _infoUI?.SetStatus(_status);
        if (_buyPanel != null)
            Destroy(_buyPanel.gameObject);
    }

    private void ApplyBuyPanelAdBadge()
    {
        if (_buyPanel != null)
            _buyPanel.SetRewardedAdBadgeVisible(_remoteConveyorPurchase);
    }


    private void SetRouletteModels()
    {
        foreach (var brainrot in GetRouletteBrainrots())
        {
            GameObject obj = Instantiate(brainrot.Model, _rouleteModelsParent).gameObject;
            Renderer[] renderer = obj.GetComponentsInChildren<Renderer>();
            foreach (Renderer item in renderer)
            {
                item.material = _rouleteMaterial;          
            }
            obj.transform.localRotation = Quaternion.identity;
            _rouleteObjects.Add(obj);
            obj.SetActive(false);
        }
    }

    private IEnumerable<Brainrot> GetRouletteBrainrots()
    {
        var hasWeightedDrops = false;
        if (_data.BrainrotDrops != null)
        {
            for (int i = 0; i < _data.BrainrotDrops.Count; i++)
            {
                var brainrot = _data.BrainrotDrops[i].Brainrot;
                if (IsAnimalDrop(brainrot) && _data.BrainrotDrops[i].Weight > 0f)
                {
                    hasWeightedDrops = true;
                    yield return brainrot;
                }
            }
        }

        if (hasWeightedDrops)
            yield break;

        if (_data.Brainrots == null)
            yield break;

        for (int i = 0; i < _data.Brainrots.Count; i++)
        {
            if (IsAnimalDrop(_data.Brainrots[i]))
                yield return _data.Brainrots[i];
        }
    }

}
