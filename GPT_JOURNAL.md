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
