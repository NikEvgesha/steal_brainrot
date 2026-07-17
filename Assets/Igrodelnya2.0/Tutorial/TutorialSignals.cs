using System;
using UnityEngine;

public enum TutorialSignalType
{
    ItemAcquired,
    EggPlaced,
    AnimalHatched,
    AnimalPlaced,
    IncomeReady,
    IncomeCollected,
    FieldUnlocked,
    ConveyorUpgraded,
    AlbumOpened,
    AlbumRewardClaimed,
    EggSpeedupUsed,
    BigPetPurchased,
    FoodPurchased,
    BigPetFed
}

public readonly struct TutorialSignal
{
    public readonly TutorialSignalType Type;
    public readonly UnityEngine.Object Context;
    public readonly string ItemId;
    public readonly Item ItemType;
    public readonly double Value;

    public TutorialSignal(
        TutorialSignalType type,
        UnityEngine.Object context = null,
        string itemId = null,
        Item itemType = Item.Free,
        double value = 0d)
    {
        Type = type;
        Context = context;
        ItemId = itemId ?? string.Empty;
        ItemType = itemType;
        Value = value;
    }
}

public static class TutorialSignals
{
    public static event Action<TutorialSignal> Raised;

    public static void Raise(
        TutorialSignalType type,
        UnityEngine.Object context = null,
        string itemId = null,
        Item itemType = Item.Free,
        double value = 0d)
    {
        Raised?.Invoke(new TutorialSignal(type, context, itemId, itemType, value));
    }
}
