# Album Setup Guide (Unity)

Updated: 2026-02-27

This document describes how to wire the album UI in Unity for:
- eggs tab,
- animals tab,
- locked silhouettes,
- mentions,
- first-discovery rewards.

## 1) Scene objects

1. Create `AlbumScreen` object under your game canvas.
2. Add `AlbumScreenController` to `AlbumScreen`.
3. Assign `panelRoot` to the popup root object that should open/close.
4. Keep `hideOnStart = true` if album should be closed at scene start.

## 2) Card list (left grid)

1. Create a card prefab and add `AlbumEntryView`.
2. Assign inside prefab:
- `button` -> card button,
- `iconImage` -> icon image,
- `titleText` -> title text,
- `mentionBadge` -> red dot/new badge object,
- `lockOverlay` -> silhouette/black overlay,
- `selectedFrame` -> selected border.
3. In `AlbumScreenController`:
- `cardsRoot` -> grid root transform,
- `cardPrefab` -> the card prefab from step 1.

## 3) Rare tabs (right-top icons)

1. Create rare tab prefab and add `AlbumRareTabView`.
2. Assign inside prefab:
- `button`,
- `titleText` (or icon label),
- `lockOverlay`,
- `mentionBadge`,
- `selectedFrame`.
3. In `AlbumScreenController`:
- `rareTabsRoot` -> container for rare buttons,
- `rareTabPrefab` -> rare tab prefab.

## 4) Main info panel (right)

Assign fields in `AlbumScreenController`:
- `infoIcon`,
- `infoTitle`,
- `infoDescription`,
- `infoIncome`,
- `infoSources`,
- `infoLockedOverlay`,
- `infoLockedText`.

Behavior:
- locked entity -> dark icon + `???`,
- unlocked egg -> drop list with chances,
- unlocked animal -> income + eggs/chances list.

## 5) Reward block

Assign:
- `rewardButton`,
- `rewardButtonText`,
- `rewardMentionBadge`.

Reward config:
- `defaultEggRewardGems` (default reward for eggs),
- `defaultAnimalRewardGems` (default reward for animals),
- `eggRewardOverrides` / `animalRewardOverrides` (per-ID override, ID is normalized from item `Name`).

## 6) Tab and album mentions

Assign mention objects:
- `eggsTabMention`,
- `animalsTabMention`,
- `albumIconMention` (dot on album entry button in HUD/menu).

Mention hierarchy now behaves as:
- album mention depends on tab mentions,
- tab mention depends on card/reward/rare mentions,
- card mention stays active while deeper mentions exist (reward or matching rare mention).

## 7) Runtime dependencies

Required runtime objects are created automatically:
- `AlbumProgressService` is spawned in `GameBootstrap`.

Required data source:
- `ItemPrefabStorage` must contain all egg and pet prefabs in its lists (`_eggs`, `_pets`).

Unlock sources:
- first time egg/animal is added to inventory -> entity unlock in album,
- holding item in hand/quick access -> rare-type unlock.

## 8) Localization keys used

Add/verify keys in localization data:
- `UI/Album/TabEggs`
- `UI/Album/TabAnimals`
- `UI/Album/Unknown`
- `UI/Album/EggInfoDescription`
- `UI/Album/AnimalInfoDescription`
- `UI/Album/Income`
- `UI/Album/ClaimReward`
- `UI/Album/RewardClaimed`
- `UI/Album/RareLocked`

If key is missing, fallback text from serialized fields is used.

## 9) Quick smoke checklist

1. Fresh profile:
- all cards locked,
- right panel shows `???`.
2. Add one egg to inventory:
- egg card unlocks,
- mentions appear on album/tab/card/reward (and rare tab if new rare).
3. Open card + claim reward:
- mention chain clears step-by-step.
4. Hatch/get new animal:
- animal tab unlock state updates and saves after restart.
5. Hold item with new rare type:
- matching rare tab unlocks.
