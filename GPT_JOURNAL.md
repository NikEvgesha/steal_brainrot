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

### 2026-02-25 (фикс: не терять local player id во время bootstrap)
- По обратной связи пользователя («загрузилась часть сейва», «нельзя купить землю молотком») доработан профильный bootstrap:
  - `LobbyClient.GetLocalPlayerId()` и `RemoteBasesApplier.GetLocalPlayerId()` больше не очищают кеш id при временно пустом `LoadBackendProfile`;
  - чтение id теперь привязано к `SaveManager.IsReady`, чтобы не ломать local-slot resolve на раннем кадре;
  - `LobbyClient.SetPlayerHeader(...)` переведен на кешированный `GetLocalPlayerId()`.
- В `SaveManager` добавлен runtime-cache backend-профиля и отложенная запись в провайдер:
  - `SaveBackendProfile(...)` сохраняет профиль в память даже до готовности провайдера;
  - pending-профиль и pending-флаг `SetSave(true)` автоматически флашатся в `ProgressSavingRoutine()` после `IsInitialized`.
- Ожидаемый эффект:
  - локальный слот не должен «проваливаться» в remote/unresolved;
  - интерактивы локальной базы (включая покупку земли молотком) остаются активными;
  - пропадает частичная загрузка, вызванная race на пустом `playerId`.

### 2026-02-25 (доп. фикс: fallback local-slot + стабильные Field ID)
- Добавлен fail-safe в `RemoteBasesApplier`:
  - при unresolved local-slot (временный bootstrap без `playerId`) больше не делается `return`, а выбирается fallback-слот;
  - `GetLocalSlotIndex()` теперь гарантирует fallback `0`, если id еще не доступен.
- Для консистентной загрузки зон исправлена нумерация полей:
  - `FieldManager.InitFields()` переведен на `GetComponentsInChildren<Field>(true)` (как и `ReloadFromSave`), чтобы не расходились `fieldId`/ключи между init и restore.
- Цель:
  - убрать состояние «всё не покупается» из-за ошибочного remote-режима локального слота;
  - убрать частичную загрузку зон из-за рассинхрона field-id.

### 2026-02-25 (rollback hotfix после регрессии)
- По живому логу пользователя с ошибкой:
  - `Coroutine couldn't be started because the game object ... is inactive`
  - стек: `FieldManager.ReloadFromSave -> Field.ReloadFromSaveState -> FieldCell.SetLoadedData -> Brainrot.NewPlace`.
- Исправление:
  - `FieldManager` теперь загружает `CellSaveData` только для **разблокированных** полей;
  - для заблокированных полей выполняется только очистка runtime-актеров (`Field.ClearLoadedActors`), без `SetLoadedData`;
  - в `InitFields` убран автозапуск загрузки для заблокированных полей.
- Сопутствующее:
  - откатан fallback-local-slot из `RemoteBasesApplier` (возвращен безопасный `skip apply` до резолва local slot), чтобы не загонять локальный слот в ошибочный режим.
- Ожидаемый эффект:
  - исчезают исключения/ошибки на загрузке питомцев в неактивных полях;
  - возвращается обычная работа молотка и локальных интеракций.

### 2026-02-25 (фикс репликации: default-open land + snapshot readiness gate)
- По повторному репорту (`у другого игрока неверно отображается моя база`, `после OFF/ON сеть показывает урезанную локацию`) найдено:
  - в `ZooBaseSnapshotSync.LoadBoughtCells_SOMEHOW()` в `land.boughtCells` попадали только флаги из save;
  - поля, открытые по умолчанию в сцене (`default-open`), в snapshot не попадали;
  - ранний force-snapshot мог отправляться до готовности `save/local-slot`.
- Исправлено:
  - `land.boughtCells` теперь включает `field.DefaultUnblocked || save.LoadFieldUnblockStatus(field.ID)`;
  - snapshot не строится/не отправляется, пока нет `SaveManager.IsReady`, `playerId` и resolved local-slot (`RemoteBasesApplier.TryGetResolvedLocalSlotRoot`);
  - убран fallback на `FindObjectsByType` при сборке snapshot-полей/ячеек (чтобы не захватывать не тот слот);
  - если snapshot временно нельзя собрать, dirty-state сохраняется (requeue через `BaseDirtyTracker.MarkDirty()`).
- Ожидаемый эффект:
  - удаленным игрокам показывается корректная геометрия твоей базы (включая default-open поля);
  - после OFF/ON при восстановлении сети сервер получает полный актуальный snapshot, а не урезанный baseline.

### 2026-02-25 (доп. фикс reconnect: только подтвержденный и восстановленный local-slot)
- По репорту «та же проблема было/стало после OFF/ON» усилена защита публикации snapshot:
  - в `RemoteBasesApplier` добавлены флаги `_hasServerResolvedLocalSlot` и `_forceLocalRestoreOnNextResolve`;
  - `TryGetResolvedLocalSlotRoot(...)` теперь возвращает root только если:
    - слот локального игрока подтвержден текущим `members` от сервера,
    - этот слот уже восстановлен из локального save (`_lastPreparedLocalSlotIndex == _serverLocalSlotIndex`);
  - при переходе в offline (`ApplyOfflineLocalOnly`) серверный local-slot считается невалидным до следующего state с сервера;
  - после reconnect локальный слот принудительно один раз пере-применяется из save даже при том же `slotIndex`.
- Цель:
  - не отправлять в лобби преждевременный/урезанный `baseData` между OFF/ON;
  - исключить сценарий, когда после реконнекта всем участникам показывается «обрезанная» версия базы.

### 2026-02-25 (доп. фикс offline->online: не перезатирать локальную базу лишним restore)
- В `ApplyOfflineLocalOnly()` (`RemoteBasesApplier`) отключен безусловный `RestoreLocalSlotFromSave(...)` для уже подготовленного локального слота.
- Теперь restore в offline выполняется только если локальный слот не был подготовлен/переключился, иначе сохраняется текущий runtime-визуал базы до reconnect.
- `_forceLocalRestoreOnNextResolve` выставляется условно (`!keepPreparedLocalVisual`), чтобы не навязывать лишний save-reload на ближайшем reconnect.
- Цель:
  - убрать сценарий, когда `NET OFF/ON` сам по себе откатывает локальную базу к «старой/урезанной» версии.

### 2026-02-25 (фикс hold-взаимодействия с remote player)
- По репорту: при подходе к игроку первый hold-сценарий срывался на середине, после повтора работал.
- Доработки:
  - `InteractionPanel`: добавлен `resumeAfterDisableWindow` (0.35s) и восстановление прогресса после краткого `OnDisable/OnEnable` вместо жесткого обнуления;
  - `RemoteFriendBoard.UpdatePanel`: не скрывает панель при временно пустом remote-state, если hold уже идет;
  - `RemoteFriendBoard.HidePanel`: не деактивирует панель во время активного interaction.
- Цель:
  - убрать срыв “первого нажатия” из-за кратких UI/сетевых дерганий панели.

### 2026-02-25 (фикс автосрабатывания следующей плашки после Gift)
- По репорту: после успешного `Gift` следующая плашка (например `Stats`) могла сработать сразу без нового удержания.
- Причина:
  - после `InteractionComplete` панель быстро пересобиралась, а состояние удержания/триггера от предыдущего действия еще считалось нажатым.
- Доработки:
  - `InteractionPanel`: добавлен флаг `_awaitReleaseAfterComplete`;
  - после `InteractionComplete` ставится «блок до отпускания»;
  - запуск нового `StartInteraction()` запрещен, пока игрок не отпустит кнопку/холд полностью.
- Цель:
  - исключить цепное автосрабатывание действий при быстрой смене плашек после `Gift`.

### 2026-02-26 (обновление статусов по результатам ручных тестов)
- Подтверждено пользователем:
  - фантомный локальный игрок больше не воспроизводится;
  - поток подарков и кнопки friend/gift работают стабильно;
  - сценарии online/offline/reconnect проходят без критичных регрессий.
- `TODO_List.md` обновлен:
  - закрыты оставшиеся пункты `P0` и часть `P1`, связанные с offline/reconnect;
  - `Claim all coins` поднят первым в `P2` как следующая задача реализации;
  - soak `10-20` игроков оставлен в `P1` как частично выполненный с ограничением по числу живых тестеров (план: синтетический прогон).

### 2026-02-26 (start реализации Claim all coins)
- Добавлен новый компонент `ClaimAllCoinsZone`:
  - сбор дохода со всех локальных `Brainrot` и `BigPetPoint` по hold-взаимодействию;
  - режим `rewarded ad` до покупки апгрейда `No Ads`;
  - отдельная панель апгрейда `No Ads` (цена/валюта настраиваются), статус unlock сохраняется в save.
- Для безопасного массового сбора добавлены публичные методы:
  - `Brainrot.CollectIncome(bool playAudio)` + `HasCollectibleIncome`;
  - `BigPetPoint.CollectIncome(bool playAudio)` + `HasCollectibleIncome`.
- В `Field` добавлен публичный `IsRemoteMode`, чтобы не собирать доход с remote-визуалов.
- Остается:
  - привязать панели/триггер в Unity сцене;
  - прогнать smoke (локально + в лобби), убедиться что claim не трогает remote-базы.

### 2026-02-26 (synthetic soak по лобби и join-with-friend)
- В `zoogame-backend` добавлен скрипт `ops/lobby_soak_test.py` + инструкции запуска в `SERVER_OPERATIONS.md`.
- Прогон выполнен по `https://api.igrodelnya-zoogame.ru`:
  - distribution: 12 новых игроков распределены в 2 лобби `6 + 6` (емкость сервера сейчас `6`, не `4`);
  - `join-with` при свободном месте: игрок заходит в лобби друга;
  - `join-with` при полном лобби: пара переносится вместе в другое лобби.
- По итогам пункт soak в `TODO_List.md` отмечен как закрытый (через синтетический прогон).

### 2026-02-26 (server lobby allocation policy update under new target flow)
- По уточненному целевому сценарию распределения (6/4/2 и дальнейшие friend-join переходы) в `zoogame-backend` обновлена серверная авто-алокация лобби:
  - добавлен упорядоченный выбор лобби по `CreatedOrder`;
  - добавлена reserve-проверка (сохранять минимум одно лобби с 2+ свободными слотами после авто-join);
  - `join-with` при полном лобби переводится в best-fit лобби с нужным числом слотов (минимальный подходящий свободный остаток).
- В тестовый скрипт добавлен точный сценарий пользователя: `--user-flow` (пошаговые ожидания 6/4/2 -> 5/4/3 -> 6/6/4/1 -> 5/5/6/1 и порядок следующих входов).
- Серверный коммит: `f9ee753` (`zoogame-backend/main`).
- Требуется деплой на прод и прогон:
  - `python3 ops/lobby_soak_test.py --base-url https://api.igrodelnya-zoogame.ru --user-flow --capacity 6 --expect-empty`

### 2026-02-26 (deploy + validation exact lobby flow on prod)
- На `zoogame-backend` выполнен доп.фикс keepalive для polling-клиентов:
  - `GET /lobby/state` теперь обновляет `LastUpdateUtc` для запрашивающего игрока.
  - Причина: в длинном synthetic сценарии часть "тихих" игроков истекала по `LobbyTimeoutSec=30`, из-за чего появлялся `404 not_in_lobby` на шагах 5-7.
- Тестовый скрипт `ops/lobby_soak_test.py` усилен:
  - добавлен ранний fail с понятной заметкой, если на шаге 1 меньше 3 лобби (вместо `index out of range`).
- Серверные коммиты:
  - `f9ee753` — новая политика авто-распределения/`join-with` под целевой flow.
  - `6b63ed9` — keepalive в `/lobby/state` + hardening `user-flow` скрипта.
- Прод-деплой выполнен на `https://api.igrodelnya-zoogame.ru` (`/srv/farmgame`, `docker compose build api && up -d api`).
- Результат прогона `--user-flow --expect-empty` на проде: **PASS**.
  - `step1`: `6,4,2`
  - `step2`: `5,4,3`
  - `step3`: `6,6,4,1`
  - `step4`: `5,5,6,1`
  - `step5`: `6,5,6,1` (новый игрок -> lobby1)
  - `step6`: `6,6,6,1` (новый игрок -> lobby2)
  - `step7`: `6,6,6,2` (новый игрок -> lobby4)
