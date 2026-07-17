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

### 2026-02-26 (next TODO: Claim all coins hardening)
- В клиенте усилен `ClaimAllCoinsZone` для снижения ручной настройки в Unity:
  - добавлена автопривязка `InteractionPanel` (claim/no-ads) по дочерним панелям и именам;
  - добавлена автопривязка вспомогательных ссылок (`AudioSource`, `readyIndicator`);
  - добавлено `ContextMenu` действие `ClaimAll/Auto Setup References` для ручной перепривязки в инспекторе.
- В репозиторий добавлен отсутствующий `Assets/_Scripts/ClaimAllCoinsZone.cs.meta` (фикс GUID-стабильности скрипта между машинами).
- В `LocalizationData.asset` добавлены ключи:
  - `UI/ClaimAll/Collect`
  - `UI/ClaimAll/CollectAd`
  - `UI/ClaimAll/NoAds`
- `TODO_List.md` обновлен: пункт ClaimAll переведен в стадию "остался smoke в живой сцене" (без обязательной ручной привязки как блокера).

### 2026-02-26 (ClaimAll: popup 3 действия + автосбор после покупки)
- `ClaimAllCoinsZone` переведен на новый UX-сценарий:
  - одно взаимодействие открывает popup с 3 действиями:
    - `X` в `UniversalDecisionPopup` = закрыть без действия;
    - `confirm` = `Забрать x2 (AD)`;
    - `cancel` = `Купить навсегда` (кнопка не закрывает popup, если покупка не удалась).
  - после покупки `навсегда` отключается interaction-кнопка в зоне и включается автосбор при входе в зону (`collectImmediatelyOnEnterAfterUnlock`) + периодический сбор, пока игрок стоит в зоне (`autoCollectIntervalSec`).
- Сохранение unlock теперь scoped по зоне:
  - ключ формируется как `ClaimAllNoAdsUnlocked.<zoneId>`;
  - добавлен fallback-миграционный ридер со старого глобального ключа `ClaimAllNoAdsUnlocked`.
- Добавлены локализационные ключи в `LocalizationData.asset`:
  - `UI/ClaimAll/OpenPopup`
  - `UI/ClaimAll/PopupTitle`
  - `UI/ClaimAll/PopupDescription`
  - `UI/ClaimAll/PopupCollectX2Ad`
  - `UI/ClaimAll/PopupBuyForever`

### 2026-02-26 (next TODO: экран шансов выпадения с/без бонусов удачи)
- В `ConveyorUI` добавлен сравнительный вывод шансов:
  - секция `без удачи`;
  - секция `с удачей`.
- `ConveyorDropChanceCalculator` расширен:
  - перегрузки `BuildBrainrotChances(..., applyLuckBonus)`;
  - единая функция lucky-weight по `RareType` и `Egg.Data.Luck`;
  - метод `PickRandomBrainrot(...)` для реального ролла с той же формулой.
- `Egg.GetRandomBrainrot()` переведен на калькулятор lucky-roll (`applyLuckBonus=true`), чтобы UI и фактический дроп использовали одинаковую математику.
- В локализацию добавлены ключи:
  - `UI/Conveyor/ChancesBase`
  - `UI/Conveyor/ChancesWithLuck`
- `TODO_List.md`: пункт экрана шансов переведен в `~` (остались smoke-тест и проверка баланса формулы удачи).

### 2026-02-27 (ClaimAll: remove global FindObjects from income scan)
- `ClaimAllCoinsZone` refactored to cache income sources from local root instead of global runtime search each refresh.
  - Added inspector-driven source config:
    - `Income Sources Root`
    - `Manual Income Cells`
    - `Manual Big Pet Points`
  - Added context action: `ClaimAll/Rebuild Income Sources Cache`.
  - `CollectAllIncomeRaw()` and `HasCollectibleIncome()` now iterate cached local references.
- `FieldCell` now exposes `CurrentBrainrot` for safe direct access from claim zone.
- Popup resolve path improved:
  - local root search first (`Popup Search Root`),
  - optional global fallback preserved for compatibility.
- Added setup doc: `Docs/CLAIM_ALL_COINS_ZONE_SETUP.md`.

### 2026-02-27 (separate egg-drop catalog UI)
- Added separate runtime UI for egg drop chances across all eggs from project storage list:
  - `Assets/_Scripts/UI/EggDropCatalogUI.cs`
  - `Assets/_Scripts/UI/EggDropCatalogInteractionPoint.cs`
- Added data access helpers:
  - `ItemsList.Items` + `ItemsList.GetAllOfType<T>()`
  - `ItemPrefabStorage.GetAllEggPrefabs()`
- Flow: world interaction point opens dedicated panel; panel builds per-egg animal chances using `ConveyorDropChanceCalculator.BuildBrainrotChances(egg, applyLuckBonus)`.
- Added setup doc: `Docs/EGG_DROP_CATALOG_UI_SETUP.md`.

### 2026-02-27 (local profile board interaction point)
- Added `Assets/_Scripts/UI/LocalProfileBoardPoint.cs`:
  - interaction point in world (`InteractionPanel` + trigger enter/exit),
  - opens existing `RemoteProfilePopup` with local player stats,
  - stats source: prefer `ZooBaseSnapshotSync.BuildSnapshotDto().playerStats`, fallback to runtime scan of local `FieldCell`/`BigPetPoint`.
- Added setup doc: `Docs/LOCAL_PROFILE_BOARD_SETUP.md`.
- Updated TODO: profile board moved to in-progress (`[~]`).

### 2026-02-27 (remote likes: once per day per player)
- Backend (`/Users/Roman/zoogame-backend`):
  - Added DTOs in `src/Zoogame.Api/Models.cs` for likes API.
  - Added lazy DB schema init for likes table:
    - `player_like_daily(liker_player_id, target_player_id, liked_on)` with PK on the full tuple.
  - Added endpoints in `src/Zoogame.Api/Program.cs`:
    - `POST /likes/state` -> returns `likesCount`, `canLike`, `likedToday`, `nextLikeAtUtc`.
    - `POST /likes/send` -> inserts daily like once, returns updated counter; repeat same day returns `ok=false`, `error=already_liked_today`.
- Client (`/Users/Roman/steal_brainrot`):
  - `Assets/_Scripts/Net/FriendsApi.cs`: added `GetLikeState(...)` and `SendLike(...)`.
  - `Assets/_Scripts/Net/RemoteFriendBoard.cs`:
    - `RemoteProfilePopup.Show(...)` now accepts `targetPlayerId/targetFriendCode`.
    - `RemoteProfilePopup` got:
      - like button,
      - likes counter,
      - temporary notification when like is pressed again same day,
      - anti-spam behavior: repeated presses refresh the same notification timer (no stacking).
- TODO updated:
  - `Assets/_Scripts/TODO_List.md` "Лайки 1 раз в день" moved to in-progress (`[~]`), pending integration smoke.

### 2026-02-27 (likes hardening: self-like guard on client)
- `RemoteProfilePopup` now hides/blocks like actions when target resolves to local player (`playerId`/`friendCode` match local profile).
- Server already had `cannot_like_self`; now client and server both enforce it.

### 2026-02-27 (album requirements spec: eggs + animals + mentions)
- Added product/tech spec: `Docs/ALBUM_SYSTEM_SPEC.md`.
- Captured requirements:
  - two tabs (`Eggs`, `Animals`), locked silhouette cards, `???` placeholders for locked info;
  - unlock by first inventory obtain (egg/animal);
  - rare-type tabs unlock by holding item with corresponding rare type;
  - hierarchical mention flow (album -> tab -> card -> rare -> reward) with child-first clearing;
  - first-discovery rewards (animal default `5` hard currency, egg reward configurable);
  - persistent album progress schema and runtime integration events.
- Updated TODO: album moved to in-progress (`[~]`) with next implementation steps.

### 2026-02-27 (album foundation implementation)
- Added album runtime core:
  - `Assets/_Scripts/UI/Album/AlbumProgressService.cs`
  - tracks discovered eggs/animals, rare unlocks, mention flags, reward claim flags;
  - persists via `SaveManager.SaveLevelStatus/GetLevelStatus` keys.
- Added album UI foundation:
  - `Assets/_Scripts/UI/Album/AlbumScreenController.cs`
  - `Assets/_Scripts/UI/Album/AlbumEntryView.cs`
  - `Assets/_Scripts/UI/Album/AlbumRareTabView.cs`
- Integration points:
  - `Assets/Inventory.cs`: emits `ItemAdded`/`ItemRemoved`.
  - `Assets/_Scripts/Inventory/QuickAccessManager.cs`: sends held-item rare unlock signal.
  - `Assets/_Scripts/GameBootstrap.cs`: creates `AlbumProgressService` at runtime.
  - `Assets/Igrodelnya2.0/G.cs`: added `G.Album`.
- Updated spec with implemented baseline and file map:
  - `Docs/ALBUM_SYSTEM_SPEC.md`.

### 2026-02-27 (album polish: mention hierarchy + setup guide)
- Added missing Unity meta:
  - `Assets/_Scripts/UI/Album/AlbumScreenController.cs.meta`.
- Updated album card mention logic:
  - `AlbumScreenController` card badge now stays active when any deeper mention exists (`card` or `reward` or matching `rare`), to better match nested mention flow.
- Added step-by-step Unity wiring document:
  - `Docs/ALBUM_SETUP.md` (scene object bindings, card/rare prefabs, localization keys, smoke checklist).
- Updated status docs:
  - `Assets/_Scripts/TODO_List.md`
  - `Docs/ALBUM_SYSTEM_SPEC.md`

### 2026-02-27 (LocalProfileBoardPoint: slot owner + likes)
- `LocalProfileBoardPoint` переработан под владельца конкретного слота:
  - точка теперь резолвит слот (`Slot Index Override` или авто-резолв через `RemoteBasesApplier`),
  - интеракция скрывается, если в этом слоте нет активного игрока,
  - при взаимодействии открывается профиль именно владельца слота.
- Для поддержки лайков в popup:
  - `LocalProfileBoardPoint` передает в `RemoteProfilePopup.Show(...)` `playerId/friendCode`,
  - это включает `Like`-блок для чужого игрока и корректно блокирует лайк самому себе.
- Добавлены публичные accessor-ы у `RemoteFriendBoard` (id/code/name/stats/online/hasData).
- В `RemoteBasesApplier` добавлены публичные helper-методы:
  - `TryResolveSlotIndex(...)`,
  - `IsLocalSlotForClient(...)`,
  - `TryGetProfileTargetForSlot(...)`.
- Обновлена документация:
  - `Docs/LOCAL_PROFILE_BOARD_SETUP.md`.

### 2026-03-04 (album hardening before scene wiring)
- Выполнен аудит альбома по `Docs/ALBUM_SETUP.md` и `Docs/ALBUM_SYSTEM_SPEC.md`; закрыты найденные технические риски в коде.
- `AlbumProgressService`:
  - rare-mention переведен с tab-level на entity-level ключи (`type + entityId + rareType`) для соответствия spec-поведению;
  - добавлен метод пакетного снятия rare-mention для выбранной редкости в текущей вкладке;
  - добавлен guard гидратации: сервис читает инвентарь только после `Inventory.IsInitialized`.
- `AlbumScreenController`:
  - карточка теперь проверяет rare-mention по конкретной сущности;
  - бейджи редкостей считаются агрегированно по сущностям текущей вкладки (а не по глобальному tab-ключу);
  - добавлена локализация названий редкостей (`UI/Album/Rare/*`);
  - добавлен `EnsureCatalogReady()` + late-init update-loop, чтобы альбом корректно подхватывал `ItemPrefabStorage`, даже если он появился позже `Awake`.
- `Inventory`:
  - добавлен `IsInitialized`;
  - `GetItems(...)` стал безопасным до `Init()` (без nullref при ранней подписке внешних сервисов).
- Локализация:
  - в `LocalizationData.asset` добавлены ключи `UI/Album/Rare/Common|Uncommon|Rare|Epic|Legendary|Mythic`;
  - `Docs/ALBUM_SETUP.md` обновлен списком этих ключей.
- Добавлен отдельный шаблон-схема иерархии:
  - `Docs/ALBUM_PREFAB_TEMPLATE.md` (эталон `AlbumScreen` + checklist привязок).
- Добавлен editor-tool для сборки сцены без ручной рутины:
  - `Assets/_Scripts/UI/Album/Editor/AlbumScreenAutoWireEditor.cs`;
  - меню: `Tools/Album/Build Missing Layout For Selected AlbumScreen`, `Tools/Album/Auto Wire Selected AlbumScreen`, `Tools/Album/Validate Selected AlbumScreen`.
- `AlbumScreenController` расширен поддержкой `DynamicGridSpawner` на `cardsRoot`:
  - если компонент присутствует, карточки спавнятся через него;
  - при ребилде очищаются предыдущие runtime-строки/карточки (с сохранением template-префаба).
- Статусы:
  - обновлен пункт альбома в `Assets/_Scripts/TODO_List.md` (добавлена пометка про hardening, основной следующий шаг — сцена/UX smoke).

### 2026-03-06 (album fixes: click dedupe + robust id mapping)
- По запросу пользователя на “слегка некорректную” работу альбома внесены 2 точечных фикса в runtime:
  - `Assets/_Scripts/UI/Album/AlbumEntryView.cs`:
    - добавлен frame-level dedupe клика карточки (`_lastClickFrame`), чтобы один тап не вызывал обработчик дважды через комбинацию `Button.onClick` + `IPointerClickHandler`.
  - `Assets/_Scripts/UI/Album/AlbumProgressService.cs`:
    - усилено маппирование ID предмета в `TryMapItem(...)`: теперь используется `ResolveItemId(...)` с fallback на `gameObject.name` и нормализацией суффикса `_clone`.
- Цель изменений:
  - убрать повторный вызов выбора карточки за один клик;
  - исключить пропуски открытия сущностей/редкостей в альбоме, если `InventoryItem.Name` пустой или пришел в runtime-формате.
- Проверка:
  - `dotnet build Assembly-CSharp.csproj -nologo` проходит успешно (ошибок нет).
- Что осталось:
  - ручной UX/smoke в Unity-сцене (`AlbumScreen`) на реальном профиле: single-click выбор карточки, открытие новых сущностей и корректность mention после получения предметов.

### 2026-03-06 (album layout tuning: DynamicGridSpawner compact mode)
- Goal: align Album grid visuals with reference (denser card matrix, stable card size, less empty gaps).
- Updated `Assets/Igrodelnya2.0/DynamicGrid/DynamicGridSpawner.cs`:
  - added optional `compactLayout` settings (`horizontalSpacing`, `verticalSpacing`);
  - compact mode now applies top-left alignment and disables force expand for vertical/horizontal groups;
  - row height is derived from spawned child preferred/min height to avoid oversized row gaps.
- Updated `Assets/_Prefabs/UI/Album/AlbumScreen.prefab`:
  - `maxItemsPerRow` increased `3 -> 5` for cards root;
  - enabled `compactLayout` with spacing `8/8`.
- Updated `Assets/_Scripts/UI/Album/AlbumEntryView.cs`:
  - enforced card `LayoutElement` preferred/min size (`156x156`) for deterministic grid packing.
- Safety:
  - build check `dotnet build Assembly-CSharp.csproj -nologo` passes (warnings only, no errors).
- Follow-up in Unity scene:
  - verify visual parity at target resolution and adjust `preferredSize`/spacing if needed by art direction.

### 2026-03-06 (album bugfix: tab/card rebuild instability after hierarchy update)
- Fixed runtime robustness for updated Album hierarchy where `cardsRoot` can be a wrapper and actual grid lives in child `cards`.
- `Assets/_Scripts/UI/Album/AlbumScreenController.cs`:
  - `AutoSetupReferences()` now auto-resolves `DynamicGridSpawner` from descendants and aligns `cardsRoot` to the actual grid transform.
  - `ClearCardsRootForRebuild()` now clears only runtime rows/cards (and tracked spawned views), avoiding destruction of static wrapper children.
  - template hiding is now applied only for embedded scene template via `HideEmbeddedCardTemplate()`; prefab-asset references are no longer toggled.
- `Assets/Igrodelnya2.0/DynamicGrid/DynamicGridSpawner.cs`:
  - row reuse now considers only active children with `HorizontalLayoutGroup`; prevents treating decorative/template objects as rows.
  - `maxItemsPerRow` is clamped to at least 1 at runtime.
- `Assets/_Scripts/UI/Album/Editor/AlbumScreenAutoWireEditor.cs`:
  - auto-wire now prefers child `cards` as `cardsRoot` and also fills `cardsDynamicGrid`.
  - avoids overwriting `cardPrefab` / `rareTabPrefab` with null when scene template objects are absent.
- Docs updated for new hierarchy/assignments:
  - `Docs/ALBUM_PREFAB_TEMPLATE.md`
  - `Docs/ALBUM_SETUP.md`
- Verification:
  - `dotnet build Assembly-CSharp.csproj -nologo` passes (no errors).
- Follow-up runtime fix (same date):
  - `DynamicGridSpawner` now activates spawned objects before row-height estimation.
  - row-height fallback now prefers `LayoutUtility.GetPreferredHeight` and clamps raw `sizeDelta` fallback.
  - This prevents unstable row sizing when `cardPrefab` root is disabled in prefab asset.

### 2026-03-06 (album runtime stability follow-up)
- Additional fix for intermittent card disappearance while switching tabs/selecting cards:
  - `AlbumScreenController.ClearCardsRootForRebuild()` now detaches runtime row/card children from `cardsRoot` before `Destroy(...)`, so `DynamicGridSpawner` cannot reuse objects already scheduled for destruction in the same frame.
- Removed duplicate refresh cycles caused by `AlbumProgressService.Changed` during local click handlers:
  - added guarded helper `ExecuteWithoutProgressRefresh(...)`;
  - applied in `OnCardPressed`, `OnRarePressed`, and `OnRewardPressed`;
  - `OnProgressChanged()` now ignores callbacks while local guarded updates are in progress.
- Verification:
  - `dotnet build Assembly-CSharp.csproj -nologo` passes (warnings only, no errors).

### 2026-03-06 (album follow-up bugs: rare tab UX + reward button visibility)
- Fixed rare tab behavior in album to match expected UX:
  - clicking a rare tab no longer filters/hides cards in the grid (`RebuildCards()` ignores rare selection for list visibility);
  - rare tab selection is now more visible (`AlbumRareTabView`: stronger selected visuals via title bold + target graphic tint + selected frame brought to front).
- Fixed reward button visibility:
  - reward button is now shown only when a real claim is available (`rewardAmount > 0` and `CanClaimReward == true`);
  - if reward is absent/already claimed, button and mention badge are hidden.
- Verification:
  - `dotnet build Assembly-CSharp.csproj -nologo` passes (warnings only, no errors).

### 2026-05-24 (BigPet animal progression)
- Goal: remove meme/brainrot visuals from BigPet and make unlock order follow animal income from weakest to strongest.
- Updated `Assets/_Scripts/BigPet/BigPetPoint.cs`:
  - resolves BigPet progression from `G.Storage.GetAllPetPrefabs()` first, with prefab `_pets` as fallback;
  - filters known brainrot ids (`balerina`, `frutodrillo`, `sahur`, etc.) and entries with non-positive `StartIncome`;
  - sorts available animals by `Brainrot.Data.StartIncome`, then by name for ties;
  - normalizes unlock math to `(level - 1) / _lvlsPerPet` for local load, live leveling, and remote base rendering;
  - clamps selected pet id to the currently unlocked range.
- Updated BigPet UI helpers:
  - `BigPetSetUI.InitUI(...)` now rebuilds slots safely;
  - `BigPetSetSlot` unsubscribes from active-state events on destroy.
- Updated `Assets/_Prefabs/BigPet/BigPetPoint.prefab` fallback list:
  - removed `BalerinaCapuchina`, `Frutodillo`, `Sahur`;
  - added animal fallback order: Capybara, Cat, Rabbit, Wolf, Bailey, Fox, Cheetah, Giraffe, Tiger, Pig, Horse, Cow, Alpaca, Hippo, Dog, Penguin, Bee.
- Notes:
  - `Chicken` is present in `PetsList`, but has no positive `StartIncome`, so BigPet excludes it until its prefab data is fixed.
- Verification:
  - `dotnet build Assembly-CSharp.csproj -nologo -p:RunAnalyzers=false` still fails before gameplay compile because local generated projects reference missing Visual Studio Unity analyzer metadata.
  - `dotnet build Assembly-CSharp.csproj -nologo --no-dependencies -p:RunAnalyzers=false` reaches `Assembly-CSharp` but fails on stale missing source entries `Assets/_Scripts/Brainrot/Palette/ZooAnimalPaletteLibrary.cs` and `ZooAnimalPaletteRuntime.cs`.

### 2026-05-27 (desktop cursor stays visible after UI close)
- Fixed `Assets/Igrodelnya2.0/Managers/ControlManager.cs` so `CursorActive=false` no longer hides or locks the mouse cursor on desktop/WebGL.
- `ControlManager` still keeps its open-window counter and logical `CursorActive` state, but now applies `CursorLockMode.None` and visible cursor for both SDK provider and default Unity cursor paths.
- Verification:
  - `git diff --check -- Assets/Igrodelnya2.0/Managers/ControlManager.cs` passes.

### 2026-05-28 (moving road / levator setup)
- Added `MovingRoad` for trigger-based `CharacterController` movement in arrow direction.
- Added `MovingRoadVisualScroller` for WebGL-friendly material UV offset via `MaterialPropertyBlock`.
- Configured `Assets/_Prefabs/strelka doroga.prefab`:
  - `Strelka right` moves along local `+X`;
  - `Strelka Left` moves along local `-X`;
  - both lanes now have `MoveTrigger` children and inspector speeds (`moveSpeed = 5`, `scrollSpeed = 0.35`).
- Added `MovingRoadSetupUtility` editor menu for current VOX scene instances:
  - `Tools/Moving Road/Configure Open Scenes Strelka Doroga`;
  - useful because `Evgesha.unity` currently references imported `strelka doroga.vox` instances, not the new `_Prefabs/strelka doroga.prefab`.
- Verification:
  - YAML fileIDs added to the prefab were checked for duplicates.
  - Unity MCP was not used for this project because the running Bridge was connected to `E:\GitFork\dead_boat`, not `E:\GitFork\steal_brainrot`.
- Follow-up fix:
  - reversed lane movement and default UV scroll directions after in-scene testing showed the levators pushing opposite to the visible arrows.
- Follow-up raycast rewrite:
  - `MovingRoad` now stores lane direction/speed only and no longer moves players through trigger callbacks.
  - Added `MovingRoadRider` to `Player.prefab`; it raycasts down, ignores triggers, finds `MovingRoad` on the arrow/lane under the player, and applies horizontal `CharacterController.Move`.
  - Legacy `MoveTrigger` children in `strelka doroga.prefab` are disabled; `MovingRoad` is now attached to `Strelka right` and `Strelka Left`.
  - Editor setup utility now configures lane components directly and disables legacy trigger children.

### 2026-05-28 (player movement tuning)
- Doubled default `TPPlayerController` movement values:
  - `walkSpeed`: `3.5 -> 7`;
  - `runSpeed`: `6 -> 12`;
  - `acceleration`: `12 -> 24`;
  - `jumpForce`: `5 -> 10`.
- Updated both script defaults and `Assets/_Prefabs/Player.prefab` serialized values.

### 2026-06-02 12:00 MSK (egg element balance + player luck)
- Reduced improved/elemental egg frequency:
  - active `Items.prefab` weights now total 10000 with `NoElement=9900`, `Gold=40`, `Diamond=30`, `Electric=20`, `Fire=10`;
  - mirrored the same weights in `ListManager.prefab` to keep the unused manager prefab consistent.
- Added player luck plumbing for non-elemental eggs:
  - `PlayerLuckHub` is created from `GameEntryPoint` and exposed through `G.Luck`;
  - default conveyor progression bonus adds up to `+0.009` absolute `NoElement` chance at max unlocked conveyor level;
  - future boosters can register `INoElementLuckBonusSource` and contribute extra absolute `NoElement` chance.
- Updated `ElementTypeMultiplaer` so luck increases `NoElement` chance while preserving relative proportions inside the remaining elemental pool.
- Fixed same-session conveyor upgrade state for luck by updating `_lastUnlockedLevel` immediately on purchase.
- Verification:
  - `git diff --check` passes.
  - `dotnet build Assembly-CSharp.csproj --no-dependencies -p:RunAnalyzers=false` is blocked before gameplay compile by missing `.NETFramework,Version=v4.7.1` reference assemblies on this machine.

### 2026-06-02 12:36 MSK (weighted egg drops + overlap balance V1)
- Added weighted brainrot drops support:
  - `EggData` now has optional `BrainrotDrops` entries (`Brainrot + Weight`);
  - if explicit drops are absent, 4-brainrot eggs use default slot weights `55/25/15/5` (`Normal/Good/Strong/Jackpot`);
  - hatch rolls, conveyor chance UI, and new-egg icon display now use the same drop-list helper.
- Applied overlap balance V1 to active conveyor eggs:
  - `EGG_1..EGG_8` now follow the chain where the last two mobs of one egg become the first two mobs of the next;
  - prices are now approximately: `200`, `2500`, `20000`, `175000`, `1300000`, `10000000`, `75000000`, `575000000`;
  - `EGG_8` uses `Dog/Penguin/Bee/Chicken` as the late animal-only tier.
- Updated core animal CPS data:
  - all balanced animals now use `MinWeight=1`, `MaxWeightMult=3`, so `StartIncome` reads as expected average CPS under the current `WeightMultiplier / 2` formula;
  - Chicken was promoted into the late-game table with positive income/rarity.
- Verification:
  - scoped `git diff --check` passes for `Egg.cs`, `ConveyorUI.cs`, `EGG_1..EGG_8`, and updated brainrot prefabs.
  - global `git diff --check` is blocked by pre-existing dirty whitespace in `Assets/_Prefabs/strelka doroga.prefab`.
  - `dotnet build Assembly-CSharp.csproj --no-dependencies -p:RunAnalyzers=false` is still blocked before gameplay compile by missing `.NETFramework,Version=v4.7.1` reference assemblies on this machine.

### 2026-06-02 12:47 MSK (conveyor egg progression weights)
- Tuned `Assets/_Prefabs/Conveyor.prefab` egg weights so conveyor upgrades produce visible progression spikes instead of equal egg pools:
  - Common: `egg1=85`, `egg2=15`;
  - Rare 1000: `egg1=20`, `egg2=60`, `egg3=20`;
  - Rare 2000: `egg2=20`, `egg3=60`, `egg4=20`;
  - Epic: `egg3=15`, `egg4=60`, `egg5=25`;
  - Legendary: `egg3=5`, `egg4=20`, `egg5=55`, `egg6=20`;
  - Mythic: `egg4=5`, `egg5=15`, `egg6=55`, `egg7=25`;
  - God: `egg4=2`, `egg5=8`, `egg6=20`, `egg7=50`, `egg8=20`.
- Expected egg EV by conveyor level now climbs roughly:
  - `14.1/s -> 71.2/s -> 354.4/s -> 2019.1/s -> 8534.4/s -> 49597.9/s -> 217110.4/s`.
- Verification:
  - scoped `git diff --check -- Assets/_Prefabs/Conveyor.prefab` passes.
  - global `git diff --check` still has unrelated pre-existing whitespace in `Assets/_Prefabs/strelka doroga.prefab`.

### 2026-06-02 (field unlock price balance V1)
- Added distance-based grass/field unlock prices:
  - `FieldManager` now finds the unlock center from fields that are open by default;
  - field distance is measured in the manager's local X/Z plane, so moved/staggered rows still price correctly;
  - locked fields use ring pricing: `baseUnlockPrice * distancePriceMultiplier^(ring - 1)`, rounded by `priceRoundStep`;
  - V1 defaults are `baseUnlockPrice=900`, `distancePriceMultiplier=2.9`, `priceRoundStep=100`, `fallbackFieldStep=4`.
- Added `Field.SetUnlockPrice(...)` so the runtime price and buy panel stay synchronized.
- Current `SceneAny` field layout has locked cells up to ring 6; expected locked ring prices are roughly:
  - `2600 -> 7600 -> 22000 -> 63700 -> 184600`.
- Verification:
  - scoped `git diff --check` passes for `FieldManager.cs`, `Field.cs`, `TODO_List.md`, and `GPT_JOURNAL.md`.
  - `dotnet build Assembly-CSharp.csproj --no-dependencies -p:RunAnalyzers=false` is still blocked before gameplay compile by missing `.NETFramework,Version=v4.7.1` reference assemblies on this machine.

### 2026-06-02 (conveyor upgrade price + bonus balance V1)
- Reworked conveyor upgrade rewards so bonuses are based on the last purchased conveyor level, not the active level and not a sum:
  - `Conveyor` now implements `IConveyorPercentSource`;
  - money bonus reads `_lastUnlockedLevel` through `UnlockedIncomeMultiplier`;
  - element luck reads `_lastUnlockedLevel` through `UnlockedElementChanceBonus01`.
- Changed player elemental luck semantics:
  - `PlayerLuckHub` now exposes `ElementChanceBonus01`;
  - `ElementTypeMultiplaer` uses that value to increase the elemental egg chance while preserving relative weights inside the elemental pool;
  - the old conveyor luck direction toward `NoElement` was removed.
- Added automatic conveyor money modifier registration:
  - `IncomeModifiersHub` auto-adds `ConveyorUpgradeBonusModifierMB`;
  - the modifier lazily resolves the local non-remote `Conveyor`.
- Conveyor upgrade V1 table:
  - Common: `0 coins`, `x1.00 income`, `+0.0% element chance`;
  - Rare I: `75,000 coins`, `+100% income`, `+50% element luck` (`+0.5% absolute element chance`);
  - Rare II: `500,000 coins`, `+150% income`, `+100% element luck` (`+1.0% absolute element chance`);
  - Epic: `3,500,000 coins`, `+250% income`, `+175% element luck` (`+1.75% absolute element chance`);
  - Legendary: `25,000,000 coins`, `+400% income`, `+250% element luck` (`+2.5% absolute element chance`);
  - Mythic: `175,000,000 coins`, `+650% income`, `+350% element luck` (`+3.5% absolute element chance`);
  - God: `1,000,000,000 coins`, `+1000% income`, `+500% element luck` (`+5.0% absolute element chance`).
- Notes:
  - with the current base element setup (`NoElement=99%`), God reaches about `6.0%` total elemental egg chance before future boosters.
- Verification:
  - scoped `git diff --check` passes for the conveyor/luck/income files, `Conveyor.prefab`, `TODO_List.md`, and `GPT_JOURNAL.md`.
  - `dotnet build Assembly-CSharp.csproj --no-dependencies -p:RunAnalyzers=false` is still blocked before gameplay compile by missing `.NETFramework,Version=v4.7.1` reference assemblies on this machine.

### 2026-06-02 (conveyor auto-activate + balance pass notes)
- Conveyor purchases now auto-activate the purchased level immediately:
  - `Conveyor.OnLevelPurchase(...)` calls `SetLevel(lvl)` after saving `_lastUnlockedLevel`;
  - previous purchased conveyor levels remain manually selectable through the existing activation UI.
- Holistic balance estimate:
  - with 24 default-open starting slots, 60s hatch time, and active slot replacement, the currently implemented economy reaches God conveyor in roughly `2.4h`;
  - simply raising egg/conveyor prices is not enough to extend this much because the new conveyor income multipliers make the next upgrade affordable during the 24-slot hatch cycle;
  - to target a longer run, field/slot gating, hatch pacing, or per-level unlock requirements need to carry more of the progression load.

### 2026-06-02 (late progression stretch after Epic)
- Kept the first four conveyor levels fast:
  - Common `0`, Rare I `75,000`, Rare II `500,000`, Epic `3,500,000`.
- Stretched the post-Epic economy tail:
  - Legendary now costs `1,500,000,000 coins / 270,000 gems`;
  - Mythic now costs `10,500,000,000 coins / 600,000 gems`;
  - God now costs `60,000,000,000 coins / 1,500,000 gems`.
- Late egg prices were raised after the Epic-unlocked tier:
  - `EGG_6 = 600,000,000`;
  - `EGG_7 = 4,500,000,000`;
  - `EGG_8 = 34,500,000,000`.
- Late grass pricing now uses `_latePriceMultiplier=60` from ring 5 onward:
  - current expected ring prices: `2600`, `7600`, `22000`, `3,819,300`, `11,076,000`.
- Balance note:
  - a simple active-slot simulation with 24 starting slots and 60s hatch time estimates roughly `13h` to God conveyor after this pass;
  - casual play should land longer because the estimate assumes constant optimal buying/replacing.

### 2026-06-02 (current economy V1 confirmed)
- Confirmed the current balance target without adding the two hypothetical extra conveyors.
- Current conveyor ladder:
  - Common: `0 coins / 0 gems`, `+0% income`, `+0% element luck`;
  - Rare I: `75,000 coins / 150 gems`, `+100% income`, `+50% element luck`;
  - Rare II: `500,000 coins / 500 gems`, `+150% income`, `+100% element luck`;
  - Epic: `3,500,000 coins / 1,500 gems`, `+250% income`, `+175% element luck`;
  - Legendary: `1,500,000,000 coins / 270,000 gems`, `+400% income`, `+250% element luck`;
  - Mythic: `10,500,000,000 coins / 600,000 gems`, `+650% income`, `+350% element luck`;
  - God: `60,000,000,000 coins / 1,500,000 gems`, `+1000% income`, `+500% element luck`.
- Current active egg prices:
  - `EGG_1=200`, `EGG_2=2,500`, `EGG_3=20,000`, `EGG_4=175,000`;
  - `EGG_5=1,300,000`, `EGG_6=600,000,000`, `EGG_7=4,500,000,000`, `EGG_8=34,500,000,000`.
- Current grass unlock target:
  - early rings stay readable (`2,600`, `7,600`, `22,000`);
  - late rings jump to `3,819,300` and `11,076,000`.
- The two extra conveyors remain a future late-game expansion idea, not part of V1.

### 2026-06-02 (water visual + float behavior V1)
- Generated and imported a stylized water tile for WebGL use:
  - source generated through built-in imagegen;
  - project asset: `Assets/_Sprites/Environment/Water/water_tile.png`;
  - importer set to 1024px, repeat wrap, mipmaps, compressed WebGL-friendly settings.
- Added `Custom/WebGL/CartoonWater` shader:
  - URP unlit transparent pass, no GrabPass/reflection/expensive screen reads;
  - two scrolling samples of the same texture plus small vertex wave.
- Added `Assets/_Materials/Environment/Water_WebGL.mat` and assigned it to the scene `Water` plane in `Evgesha.unity`.
- Added water movement behavior:
  - `WaterSurface` registers water bounds/surface height from the renderer;
  - `WaterFloatRider` on `Player.prefab` makes the local player float up when falling into water and applies a big upward/forward impulse near walls;
  - `TPPlayerController` now exposes vertical velocity and external horizontal impulse helpers.
- Scene note:
  - `Water` MeshCollider is disabled and marked trigger so the player does not stand on the water plane; `WaterSurface` uses the renderer bounds instead.
- Verification:
  - generated texture visually inspected;
  - `git diff --check` passes;
  - full `dotnet build Assembly-CSharp.csproj --no-dependencies -p:RunAnalyzers=false` is still blocked before gameplay compile by missing `.NETFramework,Version=v4.7.1` reference assemblies.

### 2026-07-09 (release UI windows + SpecialShop V2)
- Raised the local home marker world offset and lowered its canvas sorting order below `GameCanvas`, so it remains visible in the world without covering open menus.
- Rebuilt the `L` playtime rewards and `K` roulette source prefabs in the shared blocky UI style:
  - green tiled header, brown body, dark content area, black outlines and red close button;
  - all generated views are persisted in prefabs and remain editable in Inspector;
  - checked at `1280x720` and `800x600` through Unity Bridge.
- Reworked `SpecialShop` into a category-driven, scrollable shop with static editable card/row prefabs:
  - categories: Featured, Boosts, Permanent, Currency;
  - new consumables: x2 income for 10 minutes, +15% elemental chance for 10 minutes, and -30 minutes for active egg timers;
  - new permanent upgrades: +25% income and +10% elemental chance;
  - permanent ownership uses local fallback immediately and synchronizes to `SaveManager` when it becomes ready;
  - timed effects persist by UTC expiry and register in the existing income/luck modifier hubs.
- Added localized RU/EN shop/window strings and a fallback localization source for direct scene launches where `LocalizationManager` is absent.
- Verification:
  - Unity compilation completed successfully;
  - Play Mode smoke found no new exceptions from the changed systems;
  - shop effect smoke returned `owned 1 -> use true -> owned 0`, active income multiplier `x2`;
  - remaining Console noise is missing pre-existing animal localization keys.

### 2026-07-10 (SpecialShop one-page navigation + ad-only fallback)
- Preserved the manually adjusted `SpecialShop.prefab` window layout and converted its left buttons from category filters into one-page scroll links:
  - first button scrolls to the page start;
  - second button scrolls to Boosts;
  - third button is hidden until its future feature is ready;
  - fourth button scrolls to Gems.
- Added editable `SpecialShopSectionHeader.prefab`; product cards and row spacing remain controlled by their existing prefabs.
- Added three generated blocky navigation icons under `Assets/_Textures/UI/ShopNavigation` and assigned them to the existing left buttons.
- Changed all non-gem product prices to Gems. Gem packs remain platform purchases when purchases are available.
- Added rewarded-ad fallback for platforms without purchases:
  - only the small gem offer remains visible;
  - the price icon changes to the ad icon;
  - a completed rewarded ad grants the configured gem amount (`5` by default).
- Verification through Unity Bridge:
  - top, Boosts and Gems anchors scroll correctly without rebuilding the card list;
  - ad-only mode shows one `+5` gem offer and hides the large IAP pack;
  - no new runtime exceptions were found.

### 2026-07-10 (SpecialShop adaptive reward grid)
- Replaced `SpecialShopSlot/Rewards` horizontal layout with `AdaptiveGridSpawner` using percentage padding and spacing, centered one-row fit sizing, and no min/max cell constraints.
- Added an editable tiled background and outline directly to `SpecialShopRewardSlot.prefab`.
- Multi-reward cards now hide their summary `Effect`; single-reward cards keep it visible.
- Unity Bridge runtime verification showed three rewards at `33x33` inside the current `Rewards` rect, with `Effect` disabled and no new Console errors.

### 2026-07-11 (Editable local home marker prefab)
- Replaced the runtime-generated `LocalHomeWorldMarker` hierarchy with `Assets/_Prefabs/UI/LocalHomeWorldMarker.prefab` and assigned it in `RemoteBasesApplier.prefab`.
- The prefab owns its icon, Canvas and presentation settings: offset `(0, 20, 0)`, size `112x112`, world scale `0.1`, Canvas order `50`, refresh intervals `0.35/0.5`, and bob `0.18/2.2`.
- `RemoteBasesApplier` now only instantiates the assigned prefab and provides the local-base target; it no longer overrides marker presentation values.
- Unity Bridge Play Mode verification confirmed the prefab icon and configured size, scale and sorting order with no new runtime errors.

### 2026-07-11 (Preserve authored ad icons)
- Updated `AdButtonIconDecorator` so scene-wide decoration skips buttons that already contain an authored `AdIcon`.
- Explicit visibility updates can still show or hide an existing `AdIcon`, but no longer replace its sprite, color, size, anchors or position.
- Unity Bridge verification confirmed Roulette keeps `Icon_ImageIcon_Ad_00_l` at `(38, 0)` and `44x44` before and after decoration.

### 2026-07-16 (mobile controls + publisher handoff)
- Added a prefab-first mobile control scheme inspired by Roblox:
  - dynamic left joystick;
  - right-side camera swipe;
  - separate jump button;
  - multitouch ownership and safe-area handling;
  - automatic creation only for mobile/touch providers.
- Added project-level documentation:
  - `Docs/PROJECT_OVERVIEW.md` describes gameplay, startup, architecture, platform providers, online/offline invariants, key prefabs and verification workflow;
  - `Docs/NEXT_TASKS_HANDOFF.md` converts publisher feedback into isolated tasks for tutorial, analytics, retention, ad pacing, content balance and release hardening;
  - `START_PROMPT.md` now points new contexts to the overview and handoff before legacy TODO files.
- The current tutorial implementation was documented as a minimal skeleton, not a completed onboarding system.
- Mobile controls were previously verified through Unity Bridge for movement, camera rotation, jump input and final Game View layout; Play Mode was stopped after verification.

### 2026-07-16 (tutorial V1 implementation)
- Replaced the tutorial skeleton with a 13-step onboarding state machine covering movement, local home/conveyor navigation, starter egg placement/hatch, guaranteed first animal, income collection, first expansion, album reward and the next independent goal.
- Added exact persisted progress with stable step IDs, timestamps and idempotent starter reward flags to Mirra SDK and Dummy/PlayerPrefs providers; the legacy completion bool remains as migration fallback.
- Added gameplay signals at inventory acquisition, local placement, hatch, first income, field unlock, conveyor upgrade and album reward boundaries.
- The free `egg1` grant is fact-checked against inventory/local cells, its first hatch is capped at 5 seconds, and its first animal is guaranteed as `Capybara` without reroll duplication.
- Tutorial targets resolve only from the local slot. Remote field placement is rejected, and remote field/conveyor events cannot advance onboarding.
- Added prefab-first tutorial UI at `Assets/Resources/Tutorial/TutorialView.prefab`, safe-area target arrow, renderer highlight, conflicting-button blocker, album gate and confirmed skip flow.
- Timed interstitials are suppressed during onboarding and receive a 45-second grace period after completion or skip.
- Added six tutorial analytics events with `step_id`, `step_index`, `elapsed_sec`, `input_mode` and `online_mode`; missing analytics providers now warn once per session.
- Added 33 RU/EN localization keys with desktop/touch copy plus editor synchronization and setup validation tools.
- Verification:
  - Unity `6000.3.9f1` batch compilation completed successfully; only five pre-existing unused-field warnings remain in `AlbumScreenController`.
  - `TutorialV1EditorTools.BatchSynchronizeAndValidate` passed: 13 steps, editable prefab nodes and all 33 RU/EN keys.
  - `git diff --check` passed (line-ending conversion warnings only).
- Remaining acceptance: full Play Mode passes on desktop offline, desktop online and mobile/touch, with restart/idempotence/local-base/ad-grace assertions. Unity Bridge was unavailable on `localhost:7777`, so these runtime passes were not claimed.

### 2026-07-16 (tutorial V1 live Bridge alignment)
- Connected to the running Unity Bridge and inspected the tutorial in Play Mode on the existing mature online save.
- Fixed the runtime view selecting the disabled `FPSOverlayCanvas`; it now prefers an active `GameCanvas*`, and the task panel is visible at `1280x720`.
- Adjusted the task panel, progress label and text skip button so long RU/EN copy fits without clipping; enlarged and correctly oriented the existing arrow for world and screen-space UI targets.
- Aligned onboarding with the product model of one ordered task plus a dynamic nearest-target arrow:
  - local home entry and conveyor;
  - nearest real conveyor `egg1`;
  - nearest free cell, egg cell and animal/income cell;
  - nearest valid egg/conveyor/locked-field expansion;
  - `AlbumButton`, then an available reward or relevant unlocked/mentioned album card.
- Replaced the automatic starter-egg grant with an actual marked conveyor interaction. The tutorial-owned `egg1` offer is free, remains tracked by the normal conveyor purchase flow and is cleared safely when the step ends.
- Added editor-only forced-step/freeze helpers for target QA without writing forced steps to the player's tutorial save.
- Live checks confirmed `SpawnPoint`, local `Conveyor`, `EGG_1(Clone)`, free `cell2`, animal `cell2`, locked `Field_1` and `AlbumButton` targets; the album gate was interactable on its step and timed interstitial suppression was active.
- The existing tutorial save is restored to `learn_movement` after QA; no full gameplay/inventory reset was performed.
- Remaining acceptance is unchanged: complete clean-profile desktop offline/online and mobile/touch runs, including restart/idempotence/reward/grace-period assertions.

### 2026-07-17 (Tutorial V2 requirements captured)
- Added `Docs/TUTORIAL_V2_BACKLOG.md` without changing the current V1 runtime flow.
- Recorded the move from a saved linear index to independent per-step state keyed by immutable `stable_id`, including migration, reorder safety, new lessons for existing players and explicit rename aliases.
- Defined the required contract for every lesson: activation trigger, prerequisites, start/context, goal, progress, completion signal, hints, skip behavior, reward idempotence, priority and analytics.
- Recorded the planned upper-right task panel, collapsible left-slide behavior, persistent UI preference and independent world/UI arrow behavior.
- Split skipping into current-step and current-pack actions. Skipping a pack must not suppress future lesson IDs added by later updates.
- Left the revised lesson list intentionally TBD until its content and trigger table is approved.

### 2026-07-17 01:17 MSK (Tutorial V2 technical checkpoint)
- Replaced the single linear tutorial position with V2 per-task state keyed by immutable `stable_id`: status, definition revision, progress payload/value, reward flag and timestamps are persisted independently.
- Added V1/legacy completion migration, catalog synchronization for future IDs and deterministic prerequisite/priority scheduling. Completed/skipped IDs do not replay after reorder or revision changes; a later unknown ID is created as `Unseen`.
- Preserved the existing 13-step content while moving runtime activation/completion to the new state. Movement progress resumes from its per-task value.
- Rebuilt `Resources/Tutorial/TutorialView.prefab` as an editable upper-right panel with skip-current, skip-current-pack and a persistent animated collapse control. Panel and target arrow respect safe area.
- Added RU/EN strings and editor validation for the new controls plus synthetic migration checks.
- Unity verification completed: compilation/setup validation passed; expanded/collapsed UI was inspected at `1280x720`, `800x600` and `390x844`; skip-current moved only `learn_movement` to `Skipped` and activated `find_home`.
- Restored the test save to a fresh V2 `learn_movement` state and reset the collapse preference before stopping Play Mode.
- Tomorrow: approve the new lesson/trigger table, implement individual activation/progress/completion/hint and safe `OnSkip` handlers, add rename/reorder tests, then run confirmation-popup, restart, RU/EN, offline/online and mobile end-to-end acceptance.

### 2026-07-17 (Tutorial V2 task contracts and migration hardening)
- Added explicit activation, start action, progress type/target, completion trigger, hint target and skip policy fields to every current tutorial definition; runtime activation, completion and target resolution now use those contracts.
- Added idempotent compensation for critical single-task skips: ensure a starter-progress egg exists, place it into a free local cell, or finish hatching a placed egg so later prerequisites cannot be stranded.
- Added one-way `stable_id` alias migration with conflict merging for status, progress, revision, reward and timestamps. Editor validation now covers alias correctness, merge preservation, future IDs and order-independent saved state.
- Verified real confirmation popup flows for skip-current and skip-pack, exact active-ID resume after restart, RU/EN task copy, and live localization of an open popup in a scene without `LocalizationManager`.
- Fixed `UniversalDecisionPopup` to use the shared localization fallback and react to fallback-language changes.
- Restored the exact pre-test tutorial JSON (`learn_movement`, zero terminal tasks), legacy completion flag and collapse preference, then stopped Play Mode.
- Remaining P0 acceptance: agree the revised lesson/content table; run the critical skip side effects on a disposable clean profile; complete desktop offline/online and mobile/touch end-to-end passes with local-target, reward and ad-grace assertions.

### 2026-07-17 (Tutorial completion gems; skip removed)
- Added a definition-level completion reward to every current lesson: `1` gem for steps 1–4, `2` for steps 5–9 and `3` for steps 10–13 (`26` total).
- Added schema V3 field `completionRewardGranted`, persisted before currency mutation. Repeated delivery of the same completion cannot grant the reward twice.
- Added an explicit `CurrencyManager.IsInitialized` barrier; tutorial startup now waits until the saved balance is loaded before any completion reward can be granted.
- V1/V2 terminal tasks migrate as already settled so existing completed/skipped saves do not receive an unexpected retroactive currency payout; historical `Skipped` remains readable only for save compatibility.
- Removed skip-current, skip-pack, confirmation popup flow, critical skip compensation and all skip buttons from tutorial UI/runtime. The only task button is `Done` on the final manual-confirmation step.
- Rebuilt the editable upper-right prefab with a gem reward badge and localized `UI/Tutorial/Reward` label.
- Live Unity check: no skip objects, `Награда: +1` visible, first completion changed gems `14 -> 15`, repeated completion stayed at `15` with `completionRewardGranted=true`.
- Final Play smoke confirmed `currencyReady=true`, `14` gems and visible `Награда: +1`. Restored the exact pre-test tutorial save, gems and collapse preference; Play Mode stopped. Validator passed for 13 steps, prefab and 31 active RU/EN keys.
