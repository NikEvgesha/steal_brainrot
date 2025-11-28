public enum ItemType
{
    Any,
    Weapon,
    Heal,
    Fuel,
    Treasure,
    Trash,
    Ammo
}


public enum CurrencyType
{
    Coins,
    Gems,
    Real
}

public enum PlayerStat
{
    Health,
    Stamina,
    Exp,
}


public enum ItemTag
{
    Fuel,
    Trash,
    Valuable,
    Weapon,
    Medicine,
    Dead,
    Ammo,
    Reward
}
public enum Tag
{
    GravityPlatform,
    Sun,
    Decoration,
    Location,
    Item,
    Water,
    Inventory,
    Zomby
}
public enum Layer
{
    Default = 0,
    Ground = 6,
    Player = 7,
    Pickable = 8,
    SpawnLocations = 9,
    WorldCanvas = 10,
    Inventory = 11,
}


public enum LocalizationKeyType
{
    Settings,
    Item,
    Tag,
    Achievement,
    Level,
    Quest
}

public enum ItemSize
{
    Any,
    Small,
    Medium,
    Large
}


public enum ItemStatus
{
    Free,
    Grabbed,
    Attached,
    InInventory
}


public enum StoreType
{
    Items,
    Enemies
}

public enum WeaponType
{
    Pistol,
    Rifle,
    Shotgun
}

public enum Location
{
    None,
    Lobby,
    Game
}

public enum AchievementType
{
    Distance,
    EnemiesKilled,
    WinCount,
    DeathCount,
    ItemsCollected,
    TotalDistance,
    KilledBossSquid,
    KilledBossPurpleDragon,
    KilledBossIceDragon,
}
/// <summary>
/// Здесь перечисляются все идентификаторы квестов.
/// Чтобы завести новый квест, нужно:
///   1. Добавить новый элемент в этот enum, например, NewAwesomeQuest = 5.
///   2. Создать ScriptableObject QuestDefinition, где в инспекторе выбрать именно этот елемент.
/// </summary>
public enum QuestID
{
    Undefined = 0,

    SellGold,
    RewardPirate,
    BuyCoal,
    BuyWeapon,
    CheckLocation,
    GoToCastle,
    Kill5Dragon,
    KillBoss,

}
/// <summary>
/// Отдельный enum для всех «ключей квестов». 
/// В него входят пары: <QuestID>_Title и <QuestID>_Description.
/// Эти ключи используются только внутри QuestDefinition, 
/// чтобы не мешать основным ключам локализации.
/// </summary>
public enum QuestKeyTypeTitle
{
    None = 0,

    Quest_SellGold_Title,
    Quest_RewardPirate_Title,
    Quest_BuyCoal_Title,
    Quest_BuyWeapon_Title,
    Quest_CheckLocation_Title,
    Quest_GoToCastle_Title,
    Quest_Kill5Dragon_Title,
    Quest_KillBoss_Title,
}
/// <summary>
/// Отдельный enum для всех «ключей квестов». 
/// В него вход <QuestID>_Description.
/// Эти ключи используются только внутри QuestDefinition, 
/// чтобы не мешать основным ключам локализации.
/// </summary>
public enum QuestKeyTypeDescription
{
    None = 0,

    Quest_SellGold_Description,
    Quest_RewardPirate_Description,
    Quest_BuyCoal_Description,
    Quest_BuyWeapon_Description,
    Quest_CheckLocation_Description,
    Quest_GoToCastle_Description,
    Quest_Kill5Dragon_Description,
    Quest_KillBoss_Description,
}
public enum EnemyType
{
    None = 0,

    Any,
    Zomby,
    Drowned,
    Fish,
    Dragon,
    Pirate,
    Boss
}
public enum InteractType
{
    Location,
    City
}

public enum RouletteRewardType
{
    Gems,
    Item
}

public enum InteractionArea
{
    Fuel,
    Sell,
    Reward
}
public enum SaveKey
{
    MusicVolume,
    SoundVolume,
    Sensivity,
    Score_,
    LevelUnlock_,
    LevelWin_,
    Gems,
    Save,
    LobbyItems,
    Distance,
    InventoryList,
    Boardlist,
    Fuel,
    AttachedItems,
    Ammo_,
    Wins,
    LevelId,
    RouletteLastDate,
    Health,
    Exp,
    Level,
    Coins,
    EndTutorial,
    QuestProgress,
    PlayerFixPos,
    BoardFixPos,
    BoostType,

}
public enum BoostType
{
    Boost,
    HP,
    MultExp,
    MoveSpeedMult,
    MoneyMultSale,

    MaxFuel,
    ConsumptionFuel,
    AddMultFuel,
    MaxSpeedBoard,

    MeleDamage,
    MeleAttackSpeed,

    RangeDamage,
    RangeAttackSpeed,
    RangeReloadSpeed
}
public enum RareType
{
    RareType,
    Common,
    Uncommon,
    Rare,
    Epic,
    Legendary,
    Mythic,
}
public enum ElementType
{
    ElementType,
    NoElement,
    Gold,
    Diamond,
    Electric,
    Fire
}
public enum BoostUI
{
    BoostUI,
    Title,
    Useble,
    Discription,
    Stats,
}
public enum Item
{
    Free,
    Hamer,
    Egg,
    Brainrot,
}

public enum ShopSlotType
{
    Small, // 2-3 в строке
    Big // занимает всю строку
}

public enum ShopRewardType
{
    Item,
    Currency
}
