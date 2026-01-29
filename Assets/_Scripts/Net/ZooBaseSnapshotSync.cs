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
    [SerializeField] private ZooBackendClient backend;   // твой существующий клиент
    [SerializeField] private SaveManager save;           // твой SaveManager

    [Header("How often to publish base snapshot")]
    [SerializeField] private float publishIntervalSec = 5f;

    private void Awake()
    {
        if (backend == null) backend = G.Backend;
        if (save == null) save = G.Save;
    }

    private void Start()
    {
        StartCoroutine(PublishLoop());
    }

    IEnumerator PublishLoop()
    {
        // ZooBackendClient сам делает EnsureAuthThenLoad() в Start :contentReference[oaicite:7]{index=7}
        // но если порядок инициализации на сцене плавает — перестрахуемся:
       // if (string.IsNullOrEmpty(backend.Token))
           // yield return backend.AuthGuest();

        yield return new WaitForSeconds(2f);

        while (true)
        {
            var json = BuildSnapshotJson();
            yield return backend.SaveZoo(json); // PUT /zoo/me :contentReference[oaicite:8]{index=8}
            yield return new WaitForSeconds(publishIntervalSec);
        }
    }

    private string BuildSnapshotJson()
    {
        var dto = new BaseSnapshotDto
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

        return JsonUtility.ToJson(dto);
    }

    // ====== ТУТ ТЫ “ВСТРАИВАЕШЬСЯ” В СВОЮ ИГРУ ======
    // Я оставляю заглушки, чтобы ты просто подключил свои источники данных.

    private int LoadConveyorLevel_SOMEHOW()
    {
        // Варианты:
        // - из SaveManager (если есть сохранение/ключ)
        // - из ConveyorManager.CurrentLevel
        return save.LoadConveyorCurrentLevel();
    }

    private List<int> LoadBoughtCells_SOMEHOW()
    {
        var list = new List<int>();
        var fields = FindObjectsOfType<Field>();
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
        var cells = FindObjectsOfType<FieldCell>();
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
        var cells = FindObjectsOfType<FieldCell>();
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
