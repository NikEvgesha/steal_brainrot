using UnityEngine.Events;

public static class G
{
    public static SoundManager SoundManager;
    public static SaveManager SaveManager;
    public static GameLoader GameLoader;
    public static LocalizationManager LocalizationManager;
    public static Settings Settings;
    public static Inventory Inventory;
    public static PlayerInput Input;
    public static GameManager Game;
    public static ControlManager Control;
    public static CurrencyManager Currency;
    public static QuickAccessManager QuickAccess;
    public static ElementTypeMultiplaer Elements;
    public static PlayerManager Player;

    public static bool IsPaused;
    public static UnityEvent Initialized = new();
}

