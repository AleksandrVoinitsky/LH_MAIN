# LH_MAIN

Стартовый Unity-проект игры с сетевой библиотекой FishNet.

## Открытие проекта

1. Откройте корень репозитория через Unity Hub.
2. Используйте версию Unity из `ProjectSettings/ProjectVersion.txt`.
3. Дождитесь восстановления пакетов и генерации файлов IDE.

## Документация

Проектная документация находится в [docs/README.md](docs/README.md).

## Локальная серверная основа

Для backend требуется .NET SDK 10. Проверка выполняется из корня репозитория:

```cmd
dotnet test server\LH.Main.Server.sln --configuration Release
```

Docker Compose поднимает PostgreSQL и `backend-api`. Скопируйте `.env.example` в
`.env`, при необходимости замените локальные примерные пароль БД и JWT-ключ, затем
выполните:

```cmd
docker compose up --build postgres backend-api
```

После запуска проверка готовности доступна по адресу
`http://localhost:8080/health/ready`. Локальный API также публикует OpenAPI по
адресу `http://localhost:8080/openapi/v1.json`. Endpoint `POST /v1/auth/dev-register`
в Compose включён только для разработки; production-конфигурация должна передавать
отдельные обязательные значения `Authentication__JwtSigningKey`,
`Authentication__Issuer`, `Authentication__Audience` и
`Authentication__AccessTokenLifetimeMinutes` и не включать dev-регистрацию.

## Phase 02: локальные Unity dedicated servers

Phase 02 добавляет два локальных Unity Linux headless game-server контейнера:
`game-server-1` и `game-server-2`. Они запускаются только через Compose profile
`game-servers`, публикуют HTTP health/status endpoint и UDP game port, но backend
matchmaking/allocation отложены до Phase 03.

Локальные значения портов для profile `game-servers` задаются в `.env.example`:

| Переменная | Значение | Назначение |
| --- | --- | --- |
| `GAME_SERVER_PUBLIC_HOST` | `localhost` | Публичный host для локального статуса сервера |
| `GAME_SERVER_HTTP_CONTAINER_PORT` | `8081` | HTTP health/status port внутри контейнера |
| `GAME_SERVER_NETWORK_CONTAINER_PORT` | `7770` | UDP game port внутри контейнера |
| `GAME_SERVER_1_HTTP_HOST_PORT` | `8091` | HTTP health/status port `game-server-1` на host |
| `GAME_SERVER_2_HTTP_HOST_PORT` | `8092` | HTTP health/status port `game-server-2` на host |
| `GAME_SERVER_1_NETWORK_HOST_PORT` | `7771` | UDP game port `game-server-1` на host |
| `GAME_SERVER_2_NETWORK_HOST_PORT` | `7772` | UDP game port `game-server-2` на host |

Сборка Unity Linux headless player выполняется локально. Выходной каталог
`Builds/` игнорируется Git и не должен коммититься. Если команда сборки Unity
завершается ошибкой `Unsupported build target: StandaloneLinux64`, установите
Unity Linux Build Support для версии из `ProjectSettings/ProjectVersion.txt`,
затем повторите сборку.

```cmd
Unity -batchmode -quit -projectPath . -executeMethod LH.Main.Unity.Editor.GameServerBuild.BuildLinuxHeadless
```

После появления `Builds/GameServer/LinuxHeadless/LH.Main.GameServer.x86_64`
можно собрать и запустить контейнеры:

```cmd
docker compose --env-file .env.example --profile game-servers build game-server-1 game-server-2
docker compose --env-file .env.example --profile game-servers up --detach --wait game-server-1 game-server-2
```

Проверка готовности и статуса:

```cmd
curl http://localhost:8091/health/ready
curl http://localhost:8092/health/ready
curl http://localhost:8091/status
curl http://localhost:8092/status
```

Перезапуск одного сервера и просмотр состояния:

```cmd
docker compose --env-file .env.example --profile game-servers restart game-server-1
docker compose --env-file .env.example --profile game-servers ps
docker compose --env-file .env.example --profile game-servers logs game-server-1 game-server-2
```

Остановка Phase 02 контейнеров:

```cmd
docker compose --env-file .env.example --profile game-servers down
```

## Phase 03: локальный smoke match flow

Phase 03 использует те же два статических слота Compose и добавляет backend
matchmaking, выдачу short-lived ticket и внутреннюю проверку ticket через
`X-Game-Server-Key`. Значения `GAME_SERVER_SHARED_KEY`,
`GAME_SERVER_TICKET_LIFETIME_SECONDS`, `GAME_SERVER_1_ID` и `GAME_SERVER_2_ID`
заданы в `.env.example` только для локальной разработки; перед deployment их
нужно заменить.

Запуск backend и двух game-server контейнеров:

```powershell
docker compose --env-file .env.example --profile game-servers up --build --detach --wait backend-api game-server-1 game-server-2
curl.exe --fail --silent http://127.0.0.1:8080/health/ready
curl.exe --fail --silent http://127.0.0.1:8091/health/ready
curl.exe --fail --silent http://127.0.0.1:8092/health/ready
```

Регистрация и login двух dev users. JWT остаются только в переменных shell:

```powershell
$Password = "correct-horse-battery-staple"
$User1 = "phase03_user1_$([guid]::NewGuid().ToString('N'))"
$User2 = "phase03_user2_$([guid]::NewGuid().ToString('N'))"

curl.exe --fail --silent --request POST http://127.0.0.1:8080/v1/auth/dev-register --header "Content-Type: application/json" --data "{`"username`":`"$User1`",`"password`":`"$Password`"}"
curl.exe --fail --silent --request POST http://127.0.0.1:8080/v1/auth/dev-register --header "Content-Type: application/json" --data "{`"username`":`"$User2`",`"password`":`"$Password`"}"

$User1Login = curl.exe --fail --silent --request POST http://127.0.0.1:8080/v1/auth/dev-login --header "Content-Type: application/json" --data "{`"username`":`"$User1`",`"password`":`"$Password`"}" | ConvertFrom-Json
$User2Login = curl.exe --fail --silent --request POST http://127.0.0.1:8080/v1/auth/dev-login --header "Content-Type: application/json" --data "{`"username`":`"$User2`",`"password`":`"$Password`"}" | ConvertFrom-Json
$User1Token = $User1Login.accessToken
$User2Token = $User2Login.accessToken
```

Enqueue, inspect status и получение `playerId` через profile. Plaintext tickets
не вставляются в команды вручную и остаются в переменных shell:

```powershell
$User1Queue = curl.exe --fail --silent --request POST http://127.0.0.1:8080/v1/matchmaking/queue --header "Authorization: Bearer $User1Token" | ConvertFrom-Json
$User2Queue = curl.exe --fail --silent --request POST http://127.0.0.1:8080/v1/matchmaking/queue --header "Authorization: Bearer $User2Token" | ConvertFrom-Json

curl.exe --fail --silent http://127.0.0.1:8080/v1/matchmaking/status --header "Authorization: Bearer $User1Token"
curl.exe --fail --silent http://127.0.0.1:8080/v1/matchmaking/status --header "Authorization: Bearer $User2Token"

$User1Profile = curl.exe --fail --silent http://127.0.0.1:8080/v1/profile --header "Authorization: Bearer $User1Token" | ConvertFrom-Json
$User2Profile = curl.exe --fail --silent http://127.0.0.1:8080/v1/profile --header "Authorization: Bearer $User2Token" | ConvertFrom-Json

$User1MatchId = $User1Queue.assignment.matchId
$User1ServerId = $User1Queue.assignment.serverId
$User1Ticket = $User1Queue.assignment.ticket
$User2MatchId = $User2Queue.assignment.matchId
$User2ServerId = $User2Queue.assignment.serverId
$User2Ticket = $User2Queue.assignment.ticket
```

Проверка ticket с `X-Game-Server-Key` и повторная проверка того же ticket,
которая должна вернуть `valid: false`:

```powershell
$GameServerKey = "local-game-server-shared-key-change-before-deployment"
$User1Validation = "{`"ticket`":`"$User1Ticket`",`"matchId`":`"$User1MatchId`",`"playerId`":`"$($User1Profile.userId)`",`"serverId`":`"$User1ServerId`"}"
$User2Validation = "{`"ticket`":`"$User2Ticket`",`"matchId`":`"$User2MatchId`",`"playerId`":`"$($User2Profile.userId)`",`"serverId`":`"$User2ServerId`"}"

curl.exe --fail --silent --request POST http://127.0.0.1:8080/internal/v1/matches/tickets/validate --header "Content-Type: application/json" --header "X-Game-Server-Key: $GameServerKey" --data $User1Validation
curl.exe --fail --silent --request POST http://127.0.0.1:8080/internal/v1/matches/tickets/validate --header "Content-Type: application/json" --header "X-Game-Server-Key: $GameServerKey" --data $User2Validation
curl.exe --fail --silent --request POST http://127.0.0.1:8080/internal/v1/matches/tickets/validate --header "Content-Type: application/json" --header "X-Game-Server-Key: $GameServerKey" --data $User1Validation
```
