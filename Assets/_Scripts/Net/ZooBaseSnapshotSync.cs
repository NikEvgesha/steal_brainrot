using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable] public class BigPetDto { public int id; public int lvl; public int xp; public float income; }
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
}

public class ZooBaseSnapshotSync : MonoBehaviour
{
    [SerializeField] private ZooBackendClient backend;   // С‚РІРѕР№ СЃСѓС‰РµСЃС‚РІСѓСЋС‰РёР№ РєР»РёРµРЅС‚
    [SerializeField] private SaveManager save;           // С‚РІРѕР№ SaveManager

    [Header("How often to publish base snapshot")]
    [SerializeField] private float publishIntervalSec = 5f;
    [SerializeField] private bool autoPublish = true;

    private Coroutine _publishLoop;

    private void Awake()
    {
        if (backend == null) backend = G.Backend;
        if (save == null) save = G.Save;
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
        return new BaseSnapshotDto
        {
            updatedAt = DateTime.UtcNow.ToString("o"),
            bigPet = new BigPetDto
            {
                id = save.LoadBigPetId(),
                lvl = save.LoadBigPetLvl(),
                xp = save.LoadBigPetXP(),
                //income = save.LoadBigPetIncome()
            },
            conveyor = new ConveyorDto
            {
                lvl = LoadConveyorLevel_SOMEHOW()
            },
            land = new LandDto
            {
                boughtCells = LoadBoughtCells_SOMEHOW()
            },
            cells = LoadCells_SOMEHOW(),
            animalsOnCells = LoadAnimalsOnCells_SOMEHOW()
        };
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
        var fields = FindObjectsByType<Field>(FindObjectsSortMode.None);
        foreach (var field in fields)
        {
            if (save.LoadFieldUnblockStatus(field.ID))
                list.Add(field.ID);
        }
        return list;
    }

    private List<CellSnapshotDto> LoadCells_SOMEHOW()
    {
        var list = new List<CellSnapshotDto>();
        var cells = FindObjectsByType<FieldCell>(FindObjectsSortMode.None);
        foreach (var cell in cells)
        {
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
        return list;
    }

    private List<AnimalOnCellDto> LoadAnimalsOnCells_SOMEHOW()
    {
        var list = new List<AnimalOnCellDto>();
        var cells = FindObjectsByType<FieldCell>(FindObjectsSortMode.None);
        foreach (var cell in cells)
        {
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
        return list;
    }
}
