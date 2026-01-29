using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable] public class BigPetDto { public int id; public int lvl; public int xp; public float income; }
[Serializable] public class ConveyorDto { public int lvl; }
[Serializable] public class LandDto { public List<int> boughtCells = new(); }
[Serializable] public class AnimalOnCellDto { public int cell; public int animalId; public int lvl; }

[Serializable]
public class BaseSnapshotDto
{
    public int schema = 1;
    public string updatedAt;
    public BigPetDto bigPet;
    public ConveyorDto conveyor;
    public LandDto land;
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
        return 1;
    }

    private List<int> LoadBoughtCells_SOMEHOW()
    {
        // Например: LandManager.BoughtCellIds
        return new List<int>();
    }

    private List<AnimalOnCellDto> LoadAnimalsOnCells_SOMEHOW()
    {
        // Например: GridManager.GetAnimals() -> (cellId, animalId, level)
        return new List<AnimalOnCellDto>();
    }
}
