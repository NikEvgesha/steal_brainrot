using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class PlayerPublicStatsDto
{
    public int totalHatched;
    public double petsIncomePerSec;
    public double bestPetIncomePerSec;
    public double bigPetIncomePerSec;
}

public static class LocalPlayerStatsStore
{
    private const string HatchedKeyPrefix = "net.stats.total_hatched";

    public static int GetTotalHatched()
    {
        return Mathf.Max(0, PlayerPrefs.GetInt(BuildScopedKey(HatchedKeyPrefix), 0));
    }

    public static void IncrementHatched(int amount = 1)
    {
        if (amount <= 0)
            return;

        var key = BuildScopedKey(HatchedKeyPrefix);
        var next = Mathf.Max(0, PlayerPrefs.GetInt(key, 0)) + amount;
        PlayerPrefs.SetInt(key, next);
        PlayerPrefs.Save();
    }

    private static string BuildScopedKey(string baseKey)
    {
        var playerId = string.Empty;
        try
        {
            if (G.Save != null)
                playerId = G.Save.LoadBackendProfile().playerId;
        }
        catch
        {
            // Save bootstrap race: fallback to global key for this session.
        }

        return string.IsNullOrWhiteSpace(playerId)
            ? baseKey
            : $"{baseKey}.{playerId}";
    }
}

[Serializable] public class BigPetDto { public int id; public int lvl; public int xp; public float income; public bool purchased; }
[Serializable] public class ConveyorDto { public int lvl; }
[Serializable] public class LandDto { public List<int> boughtCells = new(); }
[Serializable] public class AnimalOnCellDto { public string cell; public string animalId; public int lvl; }
[Serializable] public class CellSnapshotDto
{
    public string cell;
    public string kind; // "egg" or "brainrot"
    public string id;
    public BrainrotDinamicData dinamic;
    public long hatchingTimestamp;
    public long incomeLastTime;
}

[Serializable]
public class BaseSnapshotDto
{
    public int schema = 1;
    public string updatedAt;
    public BigPetDto bigPet;
    public ConveyorDto conveyor;
    public LandDto land;
    public List<CellSnapshotDto> cells = new();
    public List<AnimalOnCellDto> animalsOnCells = new();
    public PlayerPublicStatsDto playerStats;
}

public class ZooBaseSnapshotSync : MonoBehaviour
{
    [SerializeField] private ZooBackendClient backend;   // С‚РІРѕР№ СЃСѓС‰РµСЃС‚РІСѓСЋС‰РёР№ РєР»РёРµРЅС‚
    [SerializeField] private SaveManager save;           // С‚РІРѕР№ SaveManager
    [SerializeField] private RemoteBasesApplier remoteBases;

    [Header("How often to publish base snapshot")]
    [SerializeField] private float publishIntervalSec = 5f;
    [SerializeField] private bool autoPublish = true;

    private Coroutine _publishLoop;
    private Transform _cachedSnapshotRoot;
    private Field[] _cachedSnapshotFields = Array.Empty<Field>();
    private FieldCell[] _cachedSnapshotCells = Array.Empty<FieldCell>();
    private BigPetPoint _cachedSnapshotBigPet;

    private void Awake()
    {
        if (backend == null) backend = G.Backend;
        if (save == null) save = G.Save;
        if (remoteBases == null) remoteBases = FindAnyObjectByType<RemoteBasesApplier>();
    }

    private void Start()
    {
        if (autoPublish)
            _publishLoop = StartCoroutine(PublishLoop());
    }

    IEnumerator PublishLoop()
    {
        // ZooBackendClient СЃР°Рј РґРµР»Р°РµС‚ EnsureAuthThenLoad() РІ Start :contentReference[oaicite:7]{index=7}
        // РЅРѕ РµСЃР»Рё РїРѕСЂСЏРґРѕРє РёРЅРёС†РёР°Р»РёР·Р°С†РёРё РЅР° СЃС†РµРЅРµ РїР»Р°РІР°РµС‚ вЂ” РїРµСЂРµСЃС‚СЂР°С…СѓРµРјСЃСЏ:
       // if (string.IsNullOrEmpty(backend.Token))
           // yield return backend.AuthGuest();

        yield return new WaitForSeconds(2f);

        while (true)
        {
            if (BaseDirtyTracker.Consume())
            {
                var json = BuildSnapshotJson();
                if (!string.IsNullOrEmpty(json) && backend != null)
                    yield return backend.SaveZoo(json); // PUT /zoo/me
                else
                    BaseDirtyTracker.MarkDirty();
            }
            yield return new WaitForSeconds(publishIntervalSec);
        }
    }

    public void SetAutoPublish(bool enabled)
    {
        autoPublish = enabled;
        if (autoPublish && _publishLoop == null)
            _publishLoop = StartCoroutine(PublishLoop());
        if (!autoPublish && _publishLoop != null)
        {
            StopCoroutine(_publishLoop);
            _publishLoop = null;
        }
    }

    public string ConsumeDirtySnapshot()
    {
        if (!BaseDirtyTracker.Consume()) return null;
        var json = BuildSnapshotJson();
        if (string.IsNullOrEmpty(json))
        {
            BaseDirtyTracker.MarkDirty();
            return null;
        }

        return json;
    }

    public string BuildSnapshotJson()
    {
        var dto = BuildSnapshotDto();
        return dto != null ? JsonUtility.ToJson(dto) : null;
    }

    public BaseSnapshotDto BuildSnapshotDto()
    {
        if (!CanBuildSnapshot())
            return null;

        var cells = LoadCells_SOMEHOW();
        var bigPetPurchased = save.LoadBigPetStatus();
        var bigPetIncomePerSec = LoadBigPetIncomePerSecond();

        return new BaseSnapshotDto
        {
            updatedAt = DateTime.UtcNow.ToString("o"),
            bigPet = new BigPetDto
            {
                id = save.LoadBigPetId(),
                lvl = save.LoadBigPetLvl(),
                xp = save.LoadBigPetXP(),
                income = (float)bigPetIncomePerSec,
                purchased = bigPetPurchased
            },
            conveyor = new ConveyorDto
            {
                lvl = LoadConveyorLevel_SOMEHOW()
            },
            land = new LandDto
            {
                boughtCells = LoadBoughtCells_SOMEHOW()
            },
            cells = cells,
            animalsOnCells = BuildAnimalsOnCells(cells),
            playerStats = BuildPublicStats(cells, bigPetIncomePerSec)
        };
    }

    public PlayerPublicStatsDto BuildPublicStatsDto()
    {
        if (!CanBuildSnapshot())
            return null;

        var cells = LoadCells_SOMEHOW();
        return BuildPublicStats(cells, LoadBigPetIncomePerSecond());
    }

    private PlayerPublicStatsDto BuildPublicStats(List<CellSnapshotDto> cells, double bigPetIncomePerSec)
    {
        var totalPetsIncome = 0d;
        var bestPetIncome = 0d;
        if (cells != null)
        {
            for (int i = 0; i < cells.Count; i++)
            {
                var cell = cells[i];
                if (cell == null || cell.kind != "brainrot")
                    continue;

                var income = Math.Max(0d, cell.dinamic.ResultIncome);
                totalPetsIncome += income;
                if (income > bestPetIncome)
                    bestPetIncome = income;
            }
        }

        return new PlayerPublicStatsDto
        {
            totalHatched = LocalPlayerStatsStore.GetTotalHatched(),
            petsIncomePerSec = totalPetsIncome,
            bestPetIncomePerSec = bestPetIncome,
            bigPetIncomePerSec = Math.Max(0d, bigPetIncomePerSec)
        };
    }

    private double LoadBigPetIncomePerSecond()
    {
        if (save != null && !save.LoadBigPetStatus())
            return 0d;

        var root = GetSnapshotRoot();
        EnsureSnapshotHierarchyCache(root);
        var bigPet = _cachedSnapshotBigPet;
        if (bigPet == null)
            bigPet = FindAnyObjectByType<BigPetPoint>();
        if (bigPet == null)
            return 0d;
        return Math.Max(0d, bigPet.CurrentIncomePerSecond);
    }

    // ====== РўРЈРў РўР« вЂњР’РЎРўР РђРР’РђР•РЁР¬РЎРЇвЂќ Р’ РЎР’РћР® РР“Р РЈ ======
    // РЇ РѕСЃС‚Р°РІР»СЏСЋ Р·Р°РіР»СѓС€РєРё, С‡С‚РѕР±С‹ С‚С‹ РїСЂРѕСЃС‚Рѕ РїРѕРґРєР»СЋС‡РёР» СЃРІРѕРё РёСЃС‚РѕС‡РЅРёРєРё РґР°РЅРЅС‹С….

    private int LoadConveyorLevel_SOMEHOW()
    {
        // Р’Р°СЂРёР°РЅС‚С‹:
        // - РёР· SaveManager (РµСЃР»Рё РµСЃС‚СЊ СЃРѕС…СЂР°РЅРµРЅРёРµ/РєР»СЋС‡)
        // - РёР· ConveyorManager.CurrentLevel
        return save != null ? save.LoadConveyorCurrentLevel() : 0;
    }

    private List<int> LoadBoughtCells_SOMEHOW()
    {
        var list = new List<int>();
        var seen = new HashSet<int>();
        var fields = GetSnapshotFields();
        foreach (var field in fields)
        {
            if (field == null) continue;
            if (!seen.Add(field.ID)) continue;
            if (field.DefaultUnblocked || (save != null && save.LoadFieldUnblockStatus(field.ID)))
                list.Add(field.ID);
        }
        list.Sort();
        return list;
    }

    private List<CellSnapshotDto> LoadCells_SOMEHOW()
    {
        var list = new List<CellSnapshotDto>();
        var seen = new HashSet<string>();
        var cells = GetSnapshotCells();
        foreach (var cell in cells)
        {
            if (cell == null || string.IsNullOrEmpty(cell.Id))
                continue;
            if (!seen.Add(cell.Id))
                continue;

            var data = save.LoadCellData(cell.Id);
            if (data == null)
                continue;

            if (data.Status == Item.Brainrot)
            {
                list.Add(new CellSnapshotDto
                {
                    cell = cell.Id,
                    kind = "brainrot",
                    id = data.ID,
                    dinamic = data.DinamicData,
                    incomeLastTime = data.IncomeLastTime
                });
            }
            else if (data.Status == Item.Egg)
            {
                list.Add(new CellSnapshotDto
                {
                    cell = cell.Id,
                    kind = "egg",
                    id = data.ID,
                    dinamic = data.DinamicData,
                    hatchingTimestamp = data.HatchingTimestamp
                });
            }
        }
        list.Sort((a, b) => string.CompareOrdinal(a.cell, b.cell));
        return list;
    }

    private static List<AnimalOnCellDto> BuildAnimalsOnCells(List<CellSnapshotDto> cells)
    {
        var list = new List<AnimalOnCellDto>();
        if (cells == null)
            return list;

        foreach (var cell in cells)
        {
            if (cell == null || cell.kind != "brainrot" || string.IsNullOrEmpty(cell.cell))
                continue;

            list.Add(new AnimalOnCellDto
            {
                cell = cell.cell,
                animalId = cell.id,
                lvl = 1
            });
        }

        return list;
    }

    private Field[] GetSnapshotFields()
    {
        var root = GetSnapshotRoot();
        EnsureSnapshotHierarchyCache(root);
        return _cachedSnapshotFields;
    }

    private FieldCell[] GetSnapshotCells()
    {
        var root = GetSnapshotRoot();
        EnsureSnapshotHierarchyCache(root);
        return _cachedSnapshotCells;
    }

    private void EnsureSnapshotHierarchyCache(Transform root)
    {
        if (root != null && _cachedSnapshotRoot == root)
            return;

        _cachedSnapshotRoot = root;
        if (root == null)
        {
            _cachedSnapshotFields = Array.Empty<Field>();
            _cachedSnapshotCells = Array.Empty<FieldCell>();
            _cachedSnapshotBigPet = null;
            return;
        }

        _cachedSnapshotFields = root.GetComponentsInChildren<Field>(true);
        _cachedSnapshotCells = root.GetComponentsInChildren<FieldCell>(true);
        _cachedSnapshotBigPet = root.GetComponentInChildren<BigPetPoint>(true);
    }

    private Transform GetSnapshotRoot()
    {
        if (remoteBases == null)
            remoteBases = FindAnyObjectByType<RemoteBasesApplier>();

        if (remoteBases != null && remoteBases.TryGetResolvedLocalSlotRoot(out var root))
            return root;

        return null;
    }

    private bool CanBuildSnapshot()
    {
        if (save == null) save = G.Save;
        if (save == null || !save.IsReady)
            return false;

        try
        {
            var profile = save.LoadBackendProfile();
            if (string.IsNullOrWhiteSpace(profile.playerId))
                return false;
        }
        catch
        {
            return false;
        }

        return GetSnapshotRoot() != null;
    }
}
