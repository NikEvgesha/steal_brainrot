# Album Prefab Template (Hierarchy + Bindings)

Этот файл - эталонный шаблон для сборки `AlbumScreen` в Unity.
Цель: быстро собрать рабочий префаб без пропуска обязательных ссылок.

## 1) Рекомендуемая иерархия

```text
AlbumScreen (GameObject, AlbumScreenController)
└─ panelRoot (GameObject)
   ├─ Header
   │  ├─ EggsTabButton (Button)
   │  │  ├─ Text (TMP_Text)
   │  │  └─ Mention (GameObject)
   │  ├─ AnimalsTabButton (Button)
   │  │  ├─ Text (TMP_Text)
   │  │  └─ Mention (GameObject)
   │  ├─ CloseButton (Button)
   │  └─ AlbumIconMention (GameObject)   // точка на кнопке входа в альбом (может быть вне panelRoot)
   ├─ Content
   │  ├─ cardsRoot (RectTransform, Grid/Vertical Layout)
   │  │  └─ AlbumEntryView (prefab template, disabled in scene)
   │  │     ├─ Button (Button)
   │  │     ├─ Image (Image)             // iconImage
   │  │     ├─ Titl (TMP_Text)           // titleText
   │  │     ├─ Mention (GameObject)      // mentionBadge
   │  │     ├─ LockOverlay (GameObject)  // lockOverlay
   │  │     └─ SelectedFrame (Image)     // selectedFrame
   │  ├─ rareTabsRoot (RectTransform, Horizontal Layout)
   │  │  └─ AlbumRareTabView (prefab template, disabled in scene)
   │  │     ├─ Button (Button)
   │  │     ├─ Text (TMP_Text)           // titleText
   │  │     ├─ Mention (GameObject)      // mentionBadge
   │  │     ├─ LockOverlay (GameObject)  // lockOverlay
   │  │     └─ SelectedFrame (Image)     // selectedFrame
   │  └─ InfoPanel
   │     ├─ InfoIcon (Image)
   │     ├─ InfoTitle (TMP_Text)
   │     ├─ InfoDescription (TMP_Text)
   │     ├─ InfoIncome (TMP_Text)
   │     ├─ InfoSources (TMP_Text)
   │     ├─ InfoLockedOverlay (GameObject)
   │     ├─ InfoLockedText (TMP_Text)
   │     └─ RewardBlock
   │        ├─ RewardButton (Button)
   │        │  └─ Text (TMP_Text)        // rewardButtonText
   │        └─ RewardMentionBadge (GameObject)
```

Примечание по лейауту карточек:
- если хочешь, можно оставить `cardsRoot` с `DynamicGridSpawner` + `VerticalLayoutGroup`;
- `AlbumScreenController` поддерживает оба варианта: обычный `GridLayoutGroup` и `DynamicGridSpawner`.

## 2) Что обязательно заполнить в AlbumScreenController

- Root:
  - `panelRoot`
  - `itemStorage` (если не хочешь рассчитывать на автоподхват из `G.Storage`)
  - `progressService` (если не хочешь рассчитывать на автоподхват из `G.Album`)
- Tab Buttons:
  - `eggsTabButton`, `animalsTabButton`, `closeButton`
  - `eggsTabText`, `animalsTabText`
  - `eggsTabMention`, `animalsTabMention`, `albumIconMention`
- Cards:
  - `cardsRoot`, `cardPrefab`
- Rare Tabs:
  - `rareTabsRoot`, `rareTabPrefab`
- Info Panel:
  - `infoIcon`, `infoTitle`, `infoDescription`, `infoIncome`, `infoSources`
  - `infoLockedOverlay`, `infoLockedText`
- Reward:
  - `rewardButton`, `rewardButtonText`, `rewardMentionBadge`

## 3) Что обязательно заполнить в префабах

`AlbumEntryView`:
- `button`
- `iconImage`
- `titleText`
- `mentionBadge`
- `lockOverlay`
- `selectedFrame`

`AlbumRareTabView`:
- `button`
- `titleText`
- `lockOverlay`
- `mentionBadge`
- `selectedFrame`

## 4) По твоим скринам: что уже ок и что не хватает

Уже хорошо:
- Есть `AlbumScreen -> panelRoot -> cardsRoot` и `rareTabsRoot`.
- Префабы `AlbumEntryView` и `AlbumRareTabView` созданы и подключены в контроллер.
- В `AlbumEntryView` назначены `Button`, `Image`, `Titl`, `Mention`.
- В `AlbumRareTabView` назначены `Button`, `Text`, `Mention`, `LockOverlay`.

Нужно дозаполнить обязательно:
- В `AlbumScreenController` почти весь блок `Tab Buttons`, весь `Info Panel` и весь `Reward` пока `None`.
- В `AlbumEntryView` не назначены `lockOverlay` и `selectedFrame`.
- В `AlbumRareTabView` не назначен `selectedFrame`.
- Желательно явно назначить `itemStorage` и `progressService` в `AlbumScreenController`.

## 5) Мини-проверка после сборки

1. Открывается/закрывается `panelRoot`.
2. Видны 2 вкладки (`Eggs/Animals`) и переключение работает.
3. При новом профиле карточки показывают lock + `???`.
4. После получения яйца/животного карточка открывается.
5. Блок награды активен только для открытых карточек и меняет статус после claim.
6. Бейджи mention появляются и снимаются по действиям.
