using UnityEngine.Events;

public static class G
{
    public static SoundManager Sound;
    public static SaveManager Save;
    public static GameLoader GameLoader;
    public static LocalizationManager Localization;
    public static Settings Settings;
    public static Inventory Inventory;
    public static PlayerInput Input;
    public static GameManager Game;
    public static ControlManager Control;
    public static CurrencyManager Currency;
    public static QuickAccessManager QuickAccess;
    public static ElementTypeMultiplaer Elements;
    public static PlayerManager Player;
    public static AdsManager Ad;
    public static SpecialShop SpecialShop;
    public static PurchasesManager Purchases;
    public static IncomeModifiersHub Income;
    public static PlayerLuckHub Luck;
    public static ShopEffectsService ShopEffects;
    public static ItemPrefabStorage Storage;
    public static ZooBackendClient Backend;
    public static AlbumProgressService Album;

    public static bool IsPaused;
    public static UnityEvent Initialized = new();
}
