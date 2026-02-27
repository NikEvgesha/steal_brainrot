# Настройка отдельного UI шансов по яйцам

## Что делает система
- Открывает отдельный экран с полным списком яиц.
- Для каждого яйца показывает животных и шанс выпадения именно из этого яйца.
- Список берется из `ItemPrefabStorage` -> `_eggs` (`ItemsList`), поэтому при добавлении нового яйца в этот список оно автоматически появится в UI.

## Какие скрипты используются
- `EggDropCatalogUI` — сам экран и построение текста шансов.
- `EggDropCatalogInteractionPoint` — точка взаимодействия в мире, которая открывает экран.

## Шаги настройки в Unity
1. Создай UI-панель (например `EggDropCatalogPanel`) внутри `GameCanvas`.
2. Внутри панели добавь:
   - `TMP_Text` для заголовка (например `TitleText`)
   - `TMP_Text` для контента (например `ContentText`)
   - кнопку закрытия (опционально)
3. Повесь `EggDropCatalogUI` на объект панели.
4. В `EggDropCatalogUI` проставь:
   - `Panel Root` = корень панели
   - `Title Text` и `Content Text`
   - `Item Storage` (лучше явной ссылкой на `ItemStorage` префаб/инстанс)
5. Создай world-объект точки взаимодействия (например `EggCatalogPoint`) с trigger-коллайдером.
6. На этот объект добавь `InteractionPanel` (как у остальных интеракций) и `EggDropCatalogInteractionPoint`.
7. В `EggDropCatalogInteractionPoint` проставь:
   - `Interaction Panel`
   - `Catalog Ui` (ссылка на `EggDropCatalogUI`)
8. На кнопку закрытия панели повесь вызов `EggDropCatalogInteractionPoint.CloseCatalog()` или `EggDropCatalogUI.Close()`.

## Важный момент по "всем яйцам проекта"
- Экран показывает яйца из `ItemPrefabStorage._eggs`.
- Если ты создал новый prefab яйца, но не добавил его в этот `ItemsList`, в каталоге он не появится.

## Как обновляются проценты
- Проценты считаются в рантайме через `ConveyorDropChanceCalculator.BuildBrainrotChances(egg, applyLuckBonus)`.
- Значит при изменении состава животных в яйце или параметров удачи (`Luck`) UI автоматически покажет новые шансы при следующем открытии/refresh.

## Быстрая проверка
1. Добавь новое яйцо в `ItemPrefabStorage._eggs`.
2. Открой каталог — яйцо должно появиться.
3. Измени список `Brainrots` у яйца.
4. Открой каталог снова — проценты должны пересчитаться.
