# GPT Journal (Client + Server)

Дата старта: 2026-02-22
Проект клиента: `/Users/Roman/steal_brainrot`
Проект сервера: `/Users/Roman/zoogame-backend`

## Цель журнала
- Держать единый контекст для нескольких GPT-разработчиков.
- Фиксировать не только "что сделали", но и "почему это следующий приоритет".
- Уменьшить повторную диагностику на старте каждой новой сессии.

## Текущий статус (снимок)
- Основной backlog: `Assets/_Scripts/TODO_List.md`.
- Локализация: `Assets/Igrodelnya2.0/Localization/Reports/Localization_TODO.md`.
- Главный риск в коде: сетевой state-machine (`LobbyClient`, `RemoteBasesApplier`) + UI друзей/подарков.
- Сервер API: минимальный .NET 8 backend, большая часть в `src/Zoogame.Api/Program.cs`.

## Зафиксированные наблюдения
1. Клиент gift-флоу отправляет `food`, сервер в `/gifts/send` принимает только `egg|brainrot`.
2. Клиент отправляет метаданные руки (`element/weight/income`), серверный `LobbyHandItem` хранит только `type,id`.
3. `/lobby/update` на сервере работает без `lock(LobbyLock)`, тогда как соседние lobby-операции под lock.
4. В клиенте есть признаки проблем с кодировкой строк (битые тексты), это влияет на UI/локализацию.
5. В TODO от 2026-02-20 уже отражены реальные P0/P1, но часть localization-пунктов частично закрыта кодом и не обновлена в markdown.

## Рекомендуемый порядок работ
1. P0.1: Стабильность multiplayer state
- Убрать фантомы/дубли в join/rejoin/offline.
- Зафиксировать clean-up remote state при уходе offline.

2. P0.2: Подарки end-to-end
- Привести контракты клиента и сервера к одному набору типов.
- Проверить полный цикл: отправка -> входящее -> accept/decline -> инвентарь.

3. P0.3: Friends interaction/UI
- Стабилизировать наведение/показ действий "Добавить в друзья" и "Подарить".
- Разобрать арбитраж `E` между конкурирующими интеракциями.

4. P0.4: UI-тексты и локализация
- Исправить битые строки/кодировку.
- Убрать критичные hardcoded строки в friends/gifts UI.

5. P1: Reconnect и smoke regression
- Дожать reconnect после `server_unreachable`.
- Добавить smoke-набор на 1/2 игроков и обязательный прогон перед релизом.

## Definition of Done для ближайших задач
- Есть явный сценарий воспроизведения "до" и "после".
- Изменения синхронно внесены в клиент и сервер при изменении контракта.
- Обновлены оба TODO-файла и этот журнал.
- Добавлены команды/шаги ручной проверки.

## Правила ведения журнала
- После каждого заметного шага добавлять запись в секцию "Лог сессий".
- Формат записи:
  - Дата/время
  - Что изменено
  - Почему это важно
  - Что осталось
- Если менялись серверные контракты, явно фиксировать совместимость с клиентом.

## Лог сессий
### 2026-02-22
- Проведен первичный аудит клиента и сервера.
- Подтверждены главные P0: фантомы/состояния lobby, gift flow, friend interaction.
- Зафиксированы межрепо-расхождения контрактов (gift type, hand metadata, lock в lobby update).

### 2026-02-23
- Добавлен WS-пилот на сервере (`/ws/lobby`) с событиями `ws_ready`, `lobby_state`, `ack`, `error`, `pong`, `left`.
- HTTP lobby-endpoints сохранены как fallback; обновления из `/lobby/*` начали ретранслироваться в WS-клиенты.
- Добавлен документ протокола: `/Users/Roman/zoogame-backend/WEBSOCKET_LOBBY.md`.
- В `nginx` добавлены upgrade headers для websocket-проксирования.
- В клиенте (`LobbyClient`) добавлен WS-transport pilot: подключение, входящий `lobby_state`, heartbeat (`ping`), `sync_request`, отправка `lobby_update` по WS, fallback на HTTP при ошибках/на WebGL.
- На сервере добавлены операционные скрипты и регламент: `/Users/Roman/zoogame-backend/SERVER_BASELINE.md`, `/Users/Roman/zoogame-backend/SERVER_OPERATIONS.md`, `ops/certbot.sh`, `ops/backup-db.sh` + cron для renew/backup.
- В клиенте усилен offline-cleanup:
  - `RemoteBasesApplier.ApplyOfflineLocalOnly()` теперь делает hard-reset runtime state (pending snapshot routines, remote players, slot mappings, teleport coroutine/state) и принудительно возвращает remote-слоты в baseline.
  - `RemotePlayerMover` получил `ResetTransientState()` для сброса буфера позиций/анимации/предмета в руке.
  - `LobbyClient.DisableOnline()` теперь публикует пустой `LobbyStateUpdated`, чтобы UI/friends не оставались в stale-состоянии.
- По TODO: пункт offline-cleanup переведен в частично закрытый (`[~]`), следующий шаг — smoke 1/2 игрока на сценарии join/rejoin/disconnect.
- Исправлен gift-контракт на сервере: `/gifts/send` теперь принимает `food` (и алиас `animal -> brainrot`), чтобы клиентская отправка подарков не падала на `invalid_itemType`.
- Исправлены причины «пропадания» игроков после некоторого времени:
  - Сервер WS: `LastUpdateUtc` теперь обновляется не только на `lobby_update`, но и на heartbeat (`ping`, `sync_request`), чтобы активный WS-клиент не выпадал из лобби по timeout.
  - Клиент `LobbyClient`: добавлена реакция на WS-ошибку `not_in_lobby` -> переход в offline с автопереподключением.
  - Клиент `LobbyClient`: добавлен dedupe `members` по `playerId` при парсинге состояния.
- Исправлена логика видимости удаленных игроков:
  - `RemoteBasesApplier`: база может уходить в baseline по distance-culling, но remote-player остается активным (отдельная логика для игроков vs баз).
  - Offline members больше не участвуют в визуальном apply remote-слотов.
- Полировка hand/slot контрактов:
  - Серверный `LobbyHandItem` расширен до `type,id,element,weight,income`, чтобы удаленно отображалось качество животного/яйца в руках игрока.
  - Выдача спавн-слота при новом входе в лобби переведена с hash-based на random preferred slot.
  - Проверено на проде: `hand` с метаданными читается вторым игроком, slot на последовательных join/leave меняется.

### 2026-02-24
- Закрыт дефект "сбрасывается нажатие" в gift-interaction:
  - `InteractionPanel` получил `InteractionStarted` + `IsInteracting`.
  - Добавлен lock действия/предмета в `RemoteFriendBoard` на старте удержания, чтобы дрожание raycast/смена `CurrentActive` не отменяли отправку.
  - Для touch/pointer удержание больше не перетирается каждым кадром `G.Input.InteractionHold` (введен `_pointerHold`).
- Закрыт дефект "при отказе подарок не возвращается":
  - Сервер `/gifts/decline` теперь создает новый `PendingGift` обратно отправителю.
  - Ответ `GiftDeclineResponse` расширен полями `returnedGiftId` и `returnedToPlayerId`.
- Что осталось:
  - Обязательный smoke 2 игрока: `send -> pending -> decline -> sender pending -> accept` для `egg/brainrot/food`.
  - Проверить UX-обратную связь при неуспешной отправке (сейчас клиент возвращает только bool без детализации ошибки).

### 2026-02-24 (UI/экономика polish)
- Лобби debug-панель переведена в singleton и стала сворачиваемой:
  - добавлен runtime toggle-кнопка и hotkey (`F3`), состояние сворачивания хранится в `PlayerPrefs`.
  - создание панели в `GameBootstrap`/`GameEntryPoint` переведено на `LobbyDebugPanel.EnsureExists()`, чтобы не плодить дубли.
- Денежный форматтер `CurrencyManager.ToString(double)` переписан:
  - убран хрупкий `Substring(0,4)` (он давал артефакты вида `42.`/локализационные проблемы),
  - добавлена безопасная обработка `NaN/Infinity`, отрицательных значений и ограничения по длине списка суффиксов.
- Укреплена офлайн-логика дохода питомцев:
  - `Brainrot.Init(...)`: безопасная обработка timestamps (`<=0`, будущее время), clamp `offlineSeconds >= 0`, показ офлайн-накопления.
  - `BigPetPoint`: хранение времени последнего сбора в Unix seconds (InvariantCulture), fallback-парсинг старого строкового формата для совместимости.

### 2026-02-24 (friends/gifts polish #2)
- Закрыт UX-запрос по отказу от подарка:
  - сервер помечает возвратные подарки флагом `isReturned`;
  - клиент (`GiftInboxUI`) автоматически принимает такие подарки без показа попапа, предмет сразу возвращается в инвентарь отправителя.
- Устранены ложные “100% hold -> сброс”:
  - `InteractionPanel` теперь вызывает `InteractionComplete` в тот же кадр, когда прогресс достиг 1.0, чтобы событие не терялось при кратком hide/show UI.
- Дожат interaction flow у удаленного игрока:
  - `RemoteFriendBoard` показывает `Gift` только при валидной цели (есть `playerId` или `friendCode`);
  - отправка подарка получила fallback на `toFriendCode`, если `playerId` еще не синхронизировался;
  - добавлена локализация кнопки "Подарить" через ключ `UI/Friends/Gift`.
- Добавлен live-refresh панели друзей:
  - `FriendsPanelController.RequestLiveRefresh()` + вызов после accept/decline входящих friend-запросов, чтобы список обновлялся без переоткрытия панели.
- Для ручных тестов добавлен debug-тумблер сети:
  - в `LobbyDebugPanel` появился `[ ] NET OFF` (симуляция оффлайна);
  - `LobbyClient` поддерживает `SetDebugSimulateOffline(...)` и удерживает клиента в offline до отключения тумблера.
- TODO расширен на multi-lobby soak:
  - добавлены сценарии 10-20 игроков (fill/dofill/создание нового лобби) и `join-with-friend` с требованием двух свободных слотов.

### 2026-02-25 (универсальный popup + аудит локализации)
- Добавлен универсальный контроллер `UniversalDecisionPopup` для экрана `YenOrNot`:
  - умеет динамически менять title/description/тексты `Ok`/`Cancel`,
  - принимает как localization keys, так и fallback raw-текст,
  - поддерживает callbacks `onConfirm/onCancel`, `X` как cancel и hide-on-start.
- `FriendsPanelController` теперь автоматически ищет `YenOrNot` и при необходимости добавляет на него `UniversalDecisionPopup` рантаймом (без обязательной ручной привязки в prefab).
- Уточнена интеграция под текущую иерархию `GameCanvas`:
  - `EnsureDecisionPopup()` теперь ищет popup не только внутри `FriendsPanel`, но и по всему `Canvas` (включая sibling-объект `YenOrNot`), плюс имеет scene-wide fallback на `FindObjectsByType<UniversalDecisionPopup>(IncludeInactive)`.
  - Это устраняет сценарий, когда `TryShowPopup(...)` возвращал `false` при корректно добавленном `YenOrNot` в `GameCanvas`, но вне дочернего дерева `FriendsPanel`.
- В `LocalizationData.asset` добавлены базовые ключи popup:
  - `UI/Popup/ConfirmTitle`, `UI/Popup/ConfirmDescription`, `UI/Popup/Yes`, `UI/Popup/No`.
- Для inbox-сценариев добавлены и используются ключи:
  - `UI/Popup/GiftTitle`, `UI/Popup/GiftTake`, `UI/Popup/GiftDecline`,
  - `UI/Popup/FriendRequestTitle`, `UI/Popup/FriendAccept`, `UI/Popup/FriendDecline`.
- Быстрый аудит локализации:
  - все `selectedKey` в prefab/scene резолвятся в `LocalizationData.asset` (пропусков не найдено),
  - дубли ключей `Item/Dragon` и `Item/Potion` удалены,
  - по новым popup-ключам есть пары `Ru/En`.

### 2026-02-25 (возврат к P0: анти-фантом + bridge)
- Усилен anti-ghost слой для удаленных игроков в `RemoteBasesApplier`:
  - добавлено отслеживание владельца `remote-player` по `playerId` (`_slotRemotePlayerOwnerIds`),
  - при смене владельца слота выполняется hard-reset `RemotePlayerMover`, очистка `RemoteFriendBoard` и репозиционирование к spawn-анкеры слота,
  - при назначении нового `member` в слот удаляется старый `playerId -> slot` mapping, чтобы не тянуть stale-привязки после rejoin.
- Обновлены вызовы `EnsureRemotePlayer(...)` для lobby/location потоков с явной передачей `playerId`, чтобы reset происходил детерминированно.
- TODO обновлен: пункт про фантома переведен в частично закрытый (`[~]`) до повторного smoke 2 клиента (`join/rejoin/disconnect`).
- Unity Bridge проверен на реальных операциях иерархии:
  - `execute` починен под macOS/Unity 6000 (поиск компилятора в `Contents/Resources/Scripting/...`, поддержка `mono + csc.exe` и `dotnet + csc.dll`),
  - подтверждены read/write операции через bridge (scene-hierarchy smoke и prefab-check на `GameCanvas/YenOrNot`).

### 2026-02-25 (по результатам smoke 1/2/3/4 от пользователя)
- От пользователя: сценарии 1/2 прошли; в 3/4 остались два дефекта:
  - у второго клиента оффлайн-игрок продолжает отображаться как online,
  - при возврате сети оффлайн-клиента иногда телепортирует к базе.
- Внесены доработки:
  - `LobbyClient.ParseMembers(...)`: добавлен stale-offline фильтр для remote members по `updatedAt` (`remoteMemberStaleOfflineSec`, default 15s). Если timestamp старее порога, member локально переводится в offline, что очищает remote-slot/remote-player на клиенте наблюдателя.
  - `RemoteBasesApplier.ApplyOfflineLocalOnly()`: возвращен безопасный сброс телепорт-маркеров; добавлен явный флаг suppress-next-teleport.
  - `LobbyClient.DisableOnline(...)`: для временных сетевых причин (`debug_simulated_offline`, `ws_not_in_lobby`, `server_unreachable`) теперь вызывается `RemoteBasesApplier.SuppressNextAutoTeleport()`, чтобы первый тик после reconnect не делал snap-back к базе.
- По новому баг-репорту пользователя ("после подарка животного пропадает игрок" / "периодически пропадает игрок"):
  - `LobbyClient.ParseMembers(...)` переписан на устойчивый dedupe по `playerId`: вместо `first wins` выбирается лучший кандидат (приоритеты: local > online > более свежий `updatedAt` > наличие позиций > наличие hand-data).
  - Это закрывает сценарий, когда в одном state-пакете приходят дубли одного `playerId` (например, старый offline и новый online), и клиент раньше случайно брал неактуальную запись.
- Статус: требуется повторный smoke на сценарии 3/4 (`NET OFF` 20-30s + возврат) для подтверждения.

### 2026-02-25 (фикс повторного срыва hold + пустой локальный слот после reconnect)
- По репорту пользователя закрыты 2 направления:
  - периодический сброс удержания на действии "подарить";
  - редкий кейс "после NET OFF 30s и возврата — новый слот пустой как стартовый".
- Изменения в hold-механике:
  - `InteractionPanel`: добавлены `inputDropGrace` и `nearCompleteThreshold`, чтобы краткий дроп ввода в последние кадры не обнулял прогресс.
- Изменения по восстановлению локального слота:
  - `RemoteBasesApplier`: при назначении локального ownership теперь выполняется `RestoreLocalSlotFromSave(...)` (offline и lobby apply paths).
  - `FieldManager/Field/FieldCell`: добавлен принудительный reload поля/ячеек из сейва с очисткой stale-актеров перед загрузкой.
  - `Conveyor`: локальный `Init()` теперь всегда перечитывает `current/unlocked` уровни из сейва (не только первый запуск), чтобы после remote->local не оставался baseline/чужой уровень.
  - `BigPetPoint`: при remote->local выполняется `ReloadLocalStateFromSave()` вместо оставления stale remote-state.
- Проверка:
  - Unity Bridge `scene_hierarchy` отвечает успешно;
  - `execute` (`return 30`) выполняется успешно после перекомпиляции.
- Что осталось:
  - обязательный ручной smoke: 2 клиента, `gift hold` soak + `NET OFF 30-40s` reconnect c проверкой, что слот не пустой и данные базы восстановлены сразу после rejoin.

### 2026-02-25 (доп. фикс: не перетирать unlock-сейв при Field.Init)
- По повторному репорту "после reconnect все еще пустая локация" найдено потенциально destructive-поведение:
  - `Field.Init()` при дефолтно заблокированном поле записывал `SaveFieldUnblockStatus(..., false)` в сейв.
  - При инициализации нового локального слота после смены ownership это могло обнулять прогресс unlock-полей.
- Исправление:
  - `Field.Init()` больше не пишет в сейв при старте; только применяет визуал (`ApplyUnblockedVisual`).
  - Запись в сейв остаётся только в явных действиях (`Unblock()` при покупке/подтвержденной загрузке).
- Проверка:
  - Unity Bridge `execute` (`return 31`) проходит успешно после перекомпиляции.

### 2026-02-25 (доп. фикс #2: self-heal локального сейва из lobby snapshot)
- Добавлен fallback-восстановитель для кейса, когда после reconnect локальный слот пустой:
  - `LobbyClient.ParseMembers(...)` теперь парсит `baseData` и для локального участника, чтобы этот snapshot был доступен клиенту.
  - `RemoteBasesApplier`: если локальный сейв выглядит пустым, а `localMember.baseData` непустой, выполняется восстановление сейва из snapshot перед `RestoreLocalSlotFromSave`.
  - Маппинг snapshot -> локальные ячейки делает сначала прямое совпадение `cellId`, затем fallback по порядку свободных ячеек.
- Цель: восстановить локальную базу даже если предыдущий slot-switch уже успел повредить локальные ключи и обычный reload из сейва отдаёт baseline.
- Проверка:
  - Unity Bridge `execute` (`return 33`) проходит успешно.

### 2026-02-25 (regression fix: additive merge вместо destructive restore)
- После обратной связи пользователя обнаружена регрессия: восстановление из snapshot могло закрывать открытые зоны/чистить локальные ячейки.
- Исправление в `RemoteBasesApplier`:
  - `MirrorSnapshotToLocalSave` заменен на `MergeSnapshotIntoLocalSave` (только additive изменения),
  - больше нет записи `false` для `FieldUnblockStatus`,
  - больше нет принудительной очистки локальных `CellSaveData`,
  - merge ячеек выполняется только по точному `cellId` и только если локальная ячейка пустая,
  - добавлен `ShouldMergeSnapshotIntoLocalSave` (merge только если snapshot объективно богаче локального сейва).
- Цель: исключить повторную потерю прогресса и при этом сохранить возможность авто-восстановления после поврежденного сейва.

### 2026-02-25 (архитектурное решение: сервер не трогает локальный save)
- По уточнению пользователя утверждена граница ответственности:
  - локальный прогресс/сейв полностью авторитетен на клиенте (Mirra SDK),
  - сервер только ретранслирует состояние между игроками (позиции/hand/baseData для других клиентов).
- В коде клиента убран путь `server snapshot -> local save`:
  - из `RemoteBasesApplier` удалены `ShouldMergeSnapshotIntoLocalSave`, `MergeSnapshotIntoLocalSave`, `SaveSnapshotCellToLocal` и связанные проверки;
  - `RestoreLocalSlotFromSave(...)` снова работает только от локального сейва без вмешательства server `baseData`.
- `LobbyClient.ParseMembers(...)` возвращен к модели «`baseData` только для remote members»:
  - локальный игрок больше не парсит/кэширует свой `baseData` из lobby state.
- Ожидаемый эффект:
  - исключен класс регрессий, где reconnect/дубли state могли перетирать локальный прогресс;
  - если локальный save поврежден, его восстановление должно делаться только клиентским механизмом save, а не через lobby snapshot.

### 2026-02-25 (фикс: restore локального слота только после готовности save)
- По репорту «вообще нет сохранения» добавлена защита от раннего чтения дефолтов:
  - `SaveProvider` получил признак `IsInitialized`, `SaveManager` — `IsReady`;
  - `MirraSDKSaveProvider` выставляет готовность после `WaitForProviders`.
- В `RemoteBasesApplier` локальный restore теперь отложенный:
  - если save еще не готов, слот ставится в pending и пере-применяется после `SaveManager.IsReady`;
  - `_lastPreparedLocalSlotIndex` больше не фиксируется до успешного restore.
- Цель:
  - убрать сценарий, когда локальная база и зоны выглядят пустыми из-за того, что `Load*` был вызван до инициализации Mirra save.
