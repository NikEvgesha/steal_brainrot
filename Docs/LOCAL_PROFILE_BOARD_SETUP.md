# Настройка таблички профиля (локальная статистика)

## Что реализовано
- Скрипт `LocalProfileBoardPoint` для отдельной точки взаимодействия на локации.
- Игрок подходит к точке, зажимает интеракцию, открывается popup со статистикой.
- Popup использует уже существующий `RemoteProfilePopup` (единый формат профиля).

## Какие данные показывает
- `Income/sec (all pets)`
- `Best pet income/sec`
- `Total hatched`
- `Big pet income/sec`

Сначала пытается взять `playerStats` из `ZooBaseSnapshotSync`.
Если snapshot сейчас недоступен, строит fallback-статы из локальных `FieldCell`/`BigPetPoint`.

## Шаги настройки в Unity
1. Создай world-объект точки (например `LocalProfileBoardPoint`) с trigger collider.
2. Добавь на объект:
   - `InteractionPanel`
   - `LocalProfileBoardPoint`
3. В `LocalProfileBoardPoint` проставь ссылки:
   - `Interaction Panel`
   - `Snapshot Sync` (опционально, но желательно)
   - `Remote Bases` (опционально, но желательно)
   - `Stats Root` (желательно: корень локального слота/базы)
4. Убедись, что у игрока тег `Player` и точка имеет корректный trigger.
5. Проверь в рантайме: подошел -> появилась интеракция -> открылся popup статистики.

## Параметры поведения
- `Prefer Snapshot Stats`: использовать snapshot-статы в приоритете.
- `Close Popup On Exit`: закрывать popup при выходе из зоны точки.
- `Interaction Localization Key`/`Fallback`: подпись кнопки интеракции.

## Рекомендации
- Для стабильности явно задавай `Stats Root`, чтобы считать только локальную базу.
- Если точка стоит в отдельной иерархии, не полагайся на `transform.root`.
