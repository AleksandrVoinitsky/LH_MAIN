# Фаза 02: Unity dedicated server

## Цель

Получить воспроизводимый локальный контур из двух заранее запущенных Linux headless Unity/FishNet game-server контейнеров. Каждый контейнер запускает один экземпляр технического match-server, публикует health/status HTTP endpoints и готов к последующему назначению матча backend allocator в Phase 03.

## Границы фазы

В фазу входят только технический Unity server bootstrap, отдельная server-сцена, FishNet `NetworkManager`, локальная Linux headless сборка, runtime Docker image и Compose profile для двух game-server контейнеров.

В фазу не входят очередь, match tickets, backend allocator, подключение реального клиента к матчу, персонажи, бой, карта, инвентарь, награды, PvE, финальный UI и production orchestration.

## Технологии

- Unity 6000.3.14f1.
- FishNet 4.7.3 как единый сетевой foundation для server transport, replication и будущих RPC.
- Unity Editor build script для Linux headless/server build.
- Docker runtime image без Unity Editor и без Unity license/activation внутри Docker build.
- Docker Compose profile для `game-server-1` и `game-server-2`.
- Минимальный HTTP listener внутри Unity process для health/status.

## Unity server bootstrap

Добавляется отдельная техническая server-сцена, не связанная с контентными сценами. Она содержит только объекты, необходимые для запуска server process:

```text
Assets/Scenes/Server/ServerBootstrap.unity
  ServerBootstrap
  FishNet NetworkManager
  FishNet transport
```

`ServerBootstrap` отвечает за чтение конфигурации, запуск FishNet server mode, запуск HTTP listener, публикацию текущего статуса и корректное освобождение ресурсов при завершении процесса.

Runtime-конфигурация передаётся через environment variables:

```text
GAME_SERVER_ID
GAME_SERVER_HTTP_PORT
GAME_SERVER_NETWORK_PORT
GAME_SERVER_PUBLIC_HOST
GAME_SERVER_PUBLIC_NETWORK_PORT
```

`GAME_SERVER_ID` используется в логах и status response. `GAME_SERVER_HTTP_PORT` задаёт container port встроенного health/status listener. `GAME_SERVER_NETWORK_PORT` задаёт container port FishNet transport. `GAME_SERVER_PUBLIC_HOST` и `GAME_SERVER_PUBLIC_NETWORK_PORT` фиксируют адрес и опубликованный порт, которые позже сможет вернуть backend allocator; Phase 02 только отображает их в status.

Если обязательная конфигурация отсутствует или невалидна, server устанавливает статус `failed`, пишет структурированный log без секретов и завершает процесс с ненулевым кодом либо остаётся not ready, если корректное завершение невозможно на ранней стадии Unity lifecycle.

## Health и status contract

Unity process публикует локальный HTTP interface:

### `GET /health/ready`

Возвращает `200 OK`, когда Unity bootstrap завершён, FishNet server запущен и server может быть выбран allocator в будущей фазе. До готовности или при статусе `failed` возвращает `503 Service Unavailable`.

Успешный ответ:

```json
{
  "status": "ok"
}
```

### `GET /status`

Возвращает диагностический статус game-server:

```json
{
  "serverId": "game-server-1",
  "state": "idle",
  "networkPort": 7770,
  "publicHost": "localhost",
  "publicNetworkPort": 7771,
  "startedAtUtc": "2026-09-19T00:00:00Z"
}
```

Допустимые значения `state`:

- `idle` — server готов и не выполняет матч;
- `reserved` — slot зарезервирован allocator, но матч ещё не начался;
- `running` — матч выполняется;
- `failed` — server не может принимать назначение.

Phase 02 реализует и проверяет `idle` и `failed`. `reserved` и `running` фиксируются как контракт для Phase 03+, но не требуют matchmaking logic сейчас.

HTTP listener не принимает пароли, JWT или match tickets. Логи не должны содержать секреты. Для Phase 02 authentication между backend и game-server не вводится, потому что внутренних команд назначения и result submission ещё нет.

## Linux headless build

В Unity добавляется Editor build script, который собирает server player для Linux headless/server target в локальную директорию, исключённую из Git, например:

```text
Builds/GameServer/LinuxHeadless/
```

Сборка запускается локально через Unity Editor batchmode или через Editor UI. Dockerfile не содержит Unity Editor и не выполняет Unity build. Он только копирует готовый Linux player и runtime entrypoint в образ.

Такой подход сохраняет воспроизводимую команду сборки и избегает преждевременной зависимости от Unity activation внутри Docker/CI.

## Docker и Compose

Добавляется runtime Dockerfile для game-server. Образ содержит только Linux headless Unity build, необходимые runtime-зависимости и entrypoint. В образ не попадают Unity `Library`, `Temp`, IDE-артефакты, исходные секреты и локальные `.env` файлы.

`docker-compose.yml` получает profile, например `game-server`, с двумя сервисами:

```text
game-server-1
game-server-2
```

Оба контейнера используют одинаковые container ports и разные явные host-published ports из `.env.example`. Compose healthcheck вызывает `/health/ready` внутри контейнера. Сервисы подключаются к той же внутренней Compose network, что и `backend-api`, но backend не назначает серверы в Phase 02.

Пример локальных переменных:

```text
GAME_SERVER_1_HTTP_HOST_PORT=8091
GAME_SERVER_1_NETWORK_HOST_PORT=7771
GAME_SERVER_2_HTTP_HOST_PORT=8092
GAME_SERVER_2_NETWORK_HOST_PORT=7772
GAME_SERVER_HTTP_CONTAINER_PORT=8081
GAME_SERVER_NETWORK_CONTAINER_PORT=7770
```

Имена, порты и profile должны быть описаны в README вместе с командами build, start, health check, logs, restart и stop.

## Наблюдаемость

Game-server пишет startup, readiness, status transition и shutdown logs. Каждый log содержит `serverId`; `matchId` и `playerId` не появляются до фаз, где они существуют. Ошибки конфигурации и запуска FishNet должны быть видны в контейнерных логах и приводить к unhealthy контейнеру.

## Тестирование и проверки

Phase 02 проверяется комбинацией Unity build и Compose smoke test:

1. Unity Editor build script создаёт Linux headless player из server-сцены.
2. Docker runtime image собирается из готового player.
3. `docker compose --profile game-server ... up --build --detach --wait game-server-1 game-server-2` поднимает два независимых контейнера.
4. `GET /health/ready` для обоих опубликованных HTTP ports возвращает `200` и `{"status":"ok"}`.
5. `GET /status` для обоих контейнеров возвращает разные `serverId`, корректные ports и `state: "idle"`.
6. Restart одного контейнера не ломает второй; после restart health/status снова успешны.
7. Штатная остановка контейнеров не оставляет занятых host ports.

Автоматические .NET tests не требуются для Unity bootstrap, если нет новой backend/domain logic. Если появится shared contract вне Unity runtime, он должен оставаться без зависимостей на `UnityEngine`, `MonoBehaviour`, `ScriptableObject` и FishNet runtime.

## Критерии готовности

- В репозитории есть отдельная техническая server-сцена и bootstrap-код, не зависящие от клиентского UI и контентных сцен.
- Linux headless Unity build воспроизводимо создаётся локальной командой или документированным Editor action.
- Runtime Docker image запускает готовую сборку без Unity Editor.
- Compose profile поднимает два game-server контейнера с разными `serverId` и явными host-портами.
- Оба контейнера становятся healthy, `/health/ready` возвращает `200`, `/status` возвращает `idle`.
- Restart одного контейнера проходит без нарушения второго контейнера.
- README и `.env.example` описывают локальные переменные и команды Phase 02 без production secrets.
- `docker compose config`, `git diff --check` и применимые server/.NET проверки проходят.

## Риски и ограничения

- Unity batchmode сборка зависит от установленного Unity 6000.3.14f1 и локальной лицензии. Phase 02 не решает Unity licensing в CI.
- FishNet transport configuration может потребовать ручной настройки scene object references; реализация должна минимизировать изменения в пользовательских сценах.
- Встроенный HTTP listener должен корректно освобождать socket при shutdown, иначе повторный запуск контейнера может временно конфликтовать с портом.
- Контракт `reserved`/`running` фиксируется заранее, но переходы в эти состояния реализуются только когда появятся allocator и match lifecycle.
