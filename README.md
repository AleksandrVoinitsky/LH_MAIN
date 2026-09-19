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
Контейнеры `game-server-1` и
`game-server-2` — намеренные placeholders для будущих Unity Linux headless
build; они запускаются только с профилем `game-servers` и не являются игровыми
серверами.
