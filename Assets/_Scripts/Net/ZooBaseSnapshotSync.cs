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
                yield return backend.SaveZoo(json); // PUT /zoo/me
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
        return BuildSnapshotJson();
    }

    public string BuildSnapshotJson()
    {
        return JsonUtility.ToJson(BuildSnapshotDto());
    }

    public BaseSnapshotDto BuildSnapshotDto()
    {
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
            animalsOnCells = LoadAnimalsOnCells_SOMEHOW(),
            playerStats = BuildPublicStats(cells, bigPetIncomePerSec)
        };
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
        BigPetPoint bigPet = null;
        if (root != null)
            bigPet = root.GetComponentInChildren<BigPetPoint>(true);
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
        return save.LoadConveyorCurrentLevel();
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
            if (save.LoadFieldUnblockStatus(field.ID))
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

    private List<AnimalOnCellDto> LoadAnimalsOnCells_SOMEHOW()
    {
        var list = new List<AnimalOnCellDto>();
        var seen = new HashSet<string>();
        var cells = GetSnapshotCells();
        foreach (var cell in cells)
        {
            if (cell == null || string.IsNullOrEmpty(cell.Id))
                continue;
            if (!seen.Add(cell.Id))
                continue;

            var data = save.LoadCellData(cell.Id);
            if (data == null || data.Status != Item.Brainrot)
                continue;

            list.Add(new AnimalOnCellDto
            {
                cell = cell.Id,
                animalId = data.ID,
                lvl = 1
            });
        }
        list.Sort((a, b) => string.CompareOrdinal(a.cell, b.cell));
        return list;
    }

    private Field[] GetSnapshotFields()
    {
        var root = GetSnapshotRoot();
        return root != null
            ? root.GetComponentsInChildren<Field>(true)
            : FindObjectsByType<Field>(FindObjectsSortMode.None);
    }

    private FieldCell[] GetSnapshotCells()
    {
        var root = GetSnapshotRoot();
        return root != null
            ? root.GetComponentsInChildren<FieldCell>(true)
            : FindObjectsByType<FieldCell>(FindObjectsSortMode.None);
    }

    private Transform GetSnapshotRoot()
    {
        if (remoteBases == null)
            remoteBases = FindAnyObjectByType<RemoteBasesApplier>();
        return remoteBases != null ? remoteBases.GetLocalSlotRoot() : null;
    }
}
