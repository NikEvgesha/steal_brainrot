# Album System Spec (Eggs + Animals)

Updated: 2026-02-27

Status: draft from product requirements, ready for implementation.

## 1) Feature goal

Add an album UI with two tabs:
- eggs,
- animals.

At start, all entities are locked and shown as dark silhouettes.

Player can click any card. The left info panel opens for the selected card:
- unlocked entity: normal image + real data,
- locked entity: dark silhouette + `???` text placeholders.

## 2) Unlock logic

### 2.1 Entity unlock

- Egg becomes unlocked when player gets this egg in inventory at least once.
- Animal becomes unlocked when player gets this animal in inventory at least once.
- Unlock state must be saved and restored between sessions.

### 2.2 Rare Type unlock

In animal info there are `Rare Type` tabs.

`Rare Type` can stay locked until player has held (in hand/quick-access) an item with that rare type:
- either an animal of this rare type,
- or an egg of this rare type.

Rare-type unlock state must be saved and restored.

## 3) Info panel content

### 3.1 For locked cards

- silhouette image,
- `???` for name/description/stats,
- hidden or masked reward block.

### 3.2 For unlocked egg

- real egg image and name,
- list of animals that can hatch from this egg (+ chance),
- first-discovery reward block for this egg.

### 3.3 For unlocked animal

- real animal image and name,
- income value,
- list of eggs this animal can hatch from (+ chance),
- first-discovery reward block for this animal.

## 4) Reward rules

- On first discovery of a new entity, reward becomes available to claim from album.
- Initial requirement: animal first-discovery reward is `5` hard currency.
- Egg first-discovery reward also exists (value configurable in data).
- Reward can be claimed only once per entity.
- Claimed state must be saved.

## 5) Mention (notification) system

Requirement: mention should be hierarchical and disappear only after processing the next level.

Mention levels:
1. Album icon mention.
2. Tab mention (`Eggs` / `Animals`).
3. Card mention (specific egg/animal).
4. Rare-type mention (new unlocked rare type for selected entity).
5. Reward mention (claimable reward).

Behavior:
- New unlock should raise mention on all relevant levels.
- Parent mention is considered resolved only when child mentions are resolved.
- Repeated open/close must not incorrectly clear deeper mentions.
- Claiming reward clears reward mention, then higher levels can clear if no pending children remain.

## 6) Persistence model (save)

Recommended save block (single JSON in player save):

- `albumVersion`
- `eggs[eggId]`
  - `unlocked`
  - `viewed`
  - `rewardClaimed`
  - `pendingCardMention`
  - `pendingRewardMention`
- `animals[animalId]`
  - `unlocked`
  - `viewed`
  - `rewardClaimed`
  - `pendingCardMention`
  - `pendingRewardMention`
  - `rareTypes[rareType]`
    - `unlocked`
    - `viewed`
    - `pendingMention`

Tab/album mentions are derived from pending children and do not require separate storage.

## 7) Runtime integration points

Needed runtime events:
- `OnInventoryItemAdded(item)` -> unlock egg/animal.
- `OnActiveHandItemChanged(item)` -> unlock rare type from held item.
- `OnAlbumCardSelected(entity)` -> open info panel and process card-level mention.
- `OnAlbumRareTabSelected(rareType)` -> process rare-level mention.
- `OnAlbumRewardClaim(entity)` -> grant currency and process reward mention.

## 8) Unity data setup requirements

Need configurable data in Unity (no hardcoding ids in code):
- album entry list for eggs,
- album entry list for animals,
- per-animal income source,
- egg->animal drop links (already available in drop data),
- reward amount per entity (default configurable),
- icon/silhouette assets per entry.

## 9) Implementation notes

- Keep unlock/save logic in one service (example: `AlbumProgressService`).
- UI layer only reads state + sends user actions.
- Mention computation should be deterministic from state flags.
- Do not use global scene searches where inspector references can be assigned.

## 10) Pending from design refs

Design refs were mentioned by product side and will be attached later.

After refs are provided, update:
- final layout names,
- exact text blocks,
- rare-type tab visuals,
- mention icon style/placement.

## 11) Current implementation baseline (2026-02-27)

Implemented code foundation:
- `Assets/_Scripts/UI/Album/AlbumProgressService.cs`
  - discovery/unlock persistence for eggs/animals,
  - rare-type unlock from held items,
  - mention flags (card/reward/rare),
  - reward claim flags.
- `Assets/_Scripts/UI/Album/AlbumScreenController.cs`
  - tabs (`Eggs`/`Animals`),
  - card list generation from `ItemPrefabStorage`,
  - locked/unlocked info panel state (`???` fallback),
  - egg<->animal source/chance text blocks,
  - reward claim action (gems).
- `Assets/_Scripts/UI/Album/AlbumEntryView.cs`
- `Assets/_Scripts/UI/Album/AlbumRareTabView.cs`

Runtime integration:
- `Assets/Inventory.cs`: new `ItemAdded`/`ItemRemoved` events.
- `Assets/_Scripts/Inventory/QuickAccessManager.cs`: rare discover trigger on held item change.
- `Assets/_Scripts/GameBootstrap.cs`: runtime spawn of `AlbumProgressService`.
- `Assets/Igrodelnya2.0/G.cs`: global reference `G.Album`.

Setup instructions:
- `Docs/ALBUM_SETUP.md` (scene wiring, inspector bindings, localization keys, smoke checklist).

## 12) Update 2026-03-06 (separated info contracts)

Egg info contract:
- name
- first rare obtain date
- egg price
- egg icon
- hatchable animals as icons
- discovery reward status/value

Animal info contract:
- name
- first obtain date
- animal description
- income/sec
- animal icon
- discovery reward status/value

Persistence additions:
- entity first discovery timestamp
- entity+rare first seen timestamp

Implementation note:
- timestamps are persisted with local `PlayerPrefs` keys (`AlbumDateV1.*`) to avoid changing save-provider interfaces.
