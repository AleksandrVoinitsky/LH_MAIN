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
```

Остановка Phase 02 контейнеров:

```cmd
docker compose --env-file .env.example --profile game-servers down
```
