# Настройка альбома (Unity)

Обновлено: 2026-02-27

Этот документ описывает, как подключить UI альбома в Unity для:
- вкладки яиц,
- вкладки животных,
- закрытых силуэтов,
- меншенов (уведомлений),
- наград за первое открытие.

Быстрый визуальный шаблон иерархии: `Docs/ALBUM_PREFAB_TEMPLATE.md`.

Опционально для ускорения:
- В Unity меню `Tools/Album/Build Missing Layout For Selected AlbumScreen` (создает недостающий каркас UI в выбранном `AlbumScreen`).
- В Unity меню `Tools/Album/Auto Wire Selected AlbumScreen`.
- Затем `Tools/Album/Validate Selected AlbumScreen`.
- Автопривязка ориентируется на имена объектов (см. шаблон в `ALBUM_PREFAB_TEMPLATE.md`).
- Если в `AlbumEntryView/AlbumRareTabView` нет `LockOverlay` или `SelectedFrame`, tool создаст их автоматически.

## 1) Объекты в сцене

1. Создай объект `AlbumScreen` внутри игрового canvas.
2. Добавь компонент `AlbumScreenController` на `AlbumScreen`.
3. Укажи `panelRoot` на корневой объект попапа, который должен открываться/закрываться.
4. Оставь `hideOnStart = true`, если альбом должен быть закрыт при старте сцены.

## 2) Список карточек (левая сетка)

1. Создай префаб карточки и добавь `AlbumEntryView`.
2. Назначь внутри префаба:
- `button` -> кнопка карточки,
- `iconImage` -> иконка,
- `titleText` -> текст названия,
- `mentionBadge` -> объект красной точки/бейджа "новое",
- `lockOverlay` -> силуэт/черный оверлей,
- `selectedFrame` -> рамка выбранного элемента.
3. В `AlbumScreenController` назначь:
- `cardsRoot` -> контейнер `cards` (если есть wrapper `cardsRoot`, то указывай именно вложенный `cards`),
- `cardPrefab` -> префаб карточки из шага 1 (рекомендуется prefab asset, не scene template).

Опционально для `cardsRoot`:
- можно использовать `DynamicGridSpawner` вместо `GridLayoutGroup`;
- `AlbumScreenController` теперь умеет спавнить карточки через `DynamicGridSpawner`, если компонент есть на `cardsRoot`;
- при DynamicGrid также укажи поле `cardsDynamicGrid` на этот же компонент.

## 3) Вкладки редкости (иконки справа сверху)

1. Создай префаб вкладки редкости и добавь `AlbumRareTabView`.
2. Назначь внутри префаба:
- `button`,
- `titleText` (или подпись иконки),
- `lockOverlay`,
- `mentionBadge`,
- `selectedFrame`.
3. В `AlbumScreenController` назначь:
- `rareTabsRoot` -> контейнер кнопок редкостей,
- `rareTabPrefab` -> префаб вкладки редкости.

## 4) Основная инфо-панель (справа)

Назначь поля в `AlbumScreenController`:
- `infoIcon`,
- `infoTitle`,
- `infoDescription`,
- `infoIncome`,
- `infoSources`,
- `infoLockedOverlay`,
- `infoLockedText`.

Поведение:
- закрытая сущность -> затемненная иконка + `???`,
- открытое яйцо -> список животных и шансы выпадения,
- открытое животное -> доход + список яиц/шансов.

## 5) Блок награды

Назначь:
- `rewardButton`,
- `rewardButtonText`,
- `rewardMentionBadge`.

Настройка наград:
- `defaultEggRewardGems` (дефолтная награда за яйцо),
- `defaultAnimalRewardGems` (дефолтная награда за животное),
- `eggRewardOverrides` / `animalRewardOverrides` (перезапись по ID; ID нормализуется из `Name` предмета).
- `defaultRareRewardGems` (дефолтная награда за редкость),
- `rareRewardOverrides` (перезапись по `RareType`, отдельная сумма для каждой редкости).

Логика награды:
- когда редкость выбрана (нажата кнопка редкости), кнопка награды работает как награда редкости;
- награда редкости выдается отдельно для каждой редкости и вкладки (`Eggs`/`Animals`);
- если редкость не выбрана, кнопка награды работает как награда выбранной карточки (как раньше).

## 6) Меншены вкладок и альбома

Назначь объекты меншенов:
- `eggsTabMention`,
- `animalsTabMention`,
- `albumIconMention` (точка на кнопке входа в альбом в HUD/меню).

Сейчас иерархия меншенов работает так:
- меншен альбома зависит от меншенов вкладок,
- меншен вкладки зависит от меншенов карточек/наград/редкостей,
- меншен карточки остается активным, пока есть более глубокий меншен (награда или подходящая редкость).
- меншен кнопки редкости считается по всей текущей вкладке (`Eggs`/`Animals`), а не только по выбранной карточке.
- для старых сейвов (где флаг меншена редкости еще не писался) кнопка редкости тоже покажет меншен, если награда редкости не забрана.

## 7) Runtime-зависимости

Нужные runtime-объекты создаются автоматически:
- `AlbumProgressService` создается в `GameBootstrap`.

Обязательный источник данных:
- `ItemPrefabStorage` должен содержать все префабы яиц и животных в списках (`_eggs`, `_pets`).

Источники открытия:
- первое получение яйца/животного в инвентарь -> открытие сущности в альбоме,
- удержание предмета в руке/quick access -> открытие вкладки редкости.

## 8) Ключи локализации

Добавь/проверь ключи в локализации:
- `UI/Album/TabEggs`
- `UI/Album/TabAnimals`
- `UI/Album/Unknown`
- `UI/Album/EggInfoDescription`
- `UI/Album/AnimalInfoDescription`
- `UI/Album/Income`
- `UI/Album/ClaimReward`
- `UI/Album/RewardClaimed`
- `UI/Album/RareLocked`
- `UI/Album/Rare/Common`
- `UI/Album/Rare/Uncommon`
- `UI/Album/Rare/Rare`
- `UI/Album/Rare/Epic`
- `UI/Album/Rare/Legendary`
- `UI/Album/Rare/Mythic`

Если ключа нет, используется fallback-текст из сериализованных полей.

## 9) Быстрый smoke-чеклист

1. Новый профиль:
- все карточки закрыты,
- справа в панели отображается `???`.
2. Добавить одно яйцо в инвентарь:
- карточка яйца открывается,
- появляются меншены на альбоме/вкладке/карточке/награде (и на редкости, если она новая).
3. Открыть карточку + забрать награду:
- цепочка меншенов снимается по шагам.
4. Вывести/получить новое животное:
- вкладка животных обновляет состояние открытия и сохраняет его после перезапуска.
5. Взять в руку предмет новой редкости:
- соответствующая вкладка редкости открывается.

## 10) Update 2026-03-06 (Egg/Animal split info)

New optional `InfoPanel` bindings in `AlbumScreenController`:
- `eggHatchSection` (`GameObject`) - container shown only for egg entries.
- `eggHatchIconsRoot` (`Transform`) - root where hatch-result icons are rendered.
- `eggHatchIconTemplate` (`Image`) - optional template for dynamic icon pool.

Recommended hierarchy (inside `InfoPanel`):
- `EggHatchSection`
- `EggHatchSection/EggHatchIconsRoot`
- `EggHatchSection/EggHatchIconsRoot/EggHatchIconTemplate` (disabled)

Current data rendering rules:
- Egg: `InfoTitle` = name, `InfoDescription` = first rare-date, `InfoIncome` = egg price, hatch results = icons in `EggHatchSection`, `InfoSources` = reward info.
- Animal: `InfoTitle` = name, `InfoDescription` = animal description, `InfoIncome` = income/sec, `InfoSources` = first obtained date + reward info.

Date source:
- `AlbumProgressService` now stores first discovery date and first rare-seen date (local key prefix: `AlbumDateV1`).
