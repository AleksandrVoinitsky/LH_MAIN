# Фаза 00: основа разработки

## Цель

Создать минимальную воспроизводимую основу серверной разработки без игровых правил: C#-решение, ASP.NET Core health API, независимые transport contracts и Docker Compose для PostgreSQL, backend и двух зарезервированных игровых серверов.

## Выбранный стек

- .NET 10 LTS для backend, contracts и тестов.
- ASP.NET Core Minimal API для `backend-api`.
- PostgreSQL 17 в Docker.
- Docker Compose v2.
- xUnit для unit-тестов.

Unity остаётся независимым проектом. Общие контракты не должны ссылаться на Unity, FishNet или типы `UnityEngine`.

## Структура

```text
server/
  LH.Main.Server.sln
  Directory.Build.props
  src/
    LH.Main.Contracts/
    LH.Main.Backend.Api/
  tests/
    LH.Main.Contracts.Tests/
    LH.Main.Backend.Api.Tests/
docker/
  backend/Dockerfile
  game-server/Dockerfile
docker-compose.yml
.env.example
```

## Backend

`LH.Main.Backend.Api` публикует:

- `GET /health/live` — процесс запущен, без внешних зависимостей.
- `GET /health/ready` — зарезервированная точка готовности; на фазе 00 возвращает успешный статус без доступа к БД.

Health endpoints не раскрывают секреты, версии внутренней инфраструктуры или стек ошибок.

## Contracts

`LH.Main.Contracts` определяет один независящий от Unity DTO:

```csharp
public sealed record HealthStatusResponse(string Status);
```

API возвращает `HealthStatusResponse("ok")` для обоих health endpoints. Это минимальный контракт, который доказывает независимость contracts и даёт реальный API-тест без преждевременного проектирования будущих DTO.

## Docker Compose

Compose содержит:

- `postgres`: PostgreSQL 17, named volume, health check через `pg_isready`.
- `backend-api`: сборка из `docker/backend/Dockerfile`, зависит от healthy PostgreSQL, передаёт connection string через окружение, health check вызывает `/health/ready`.
- `game-server-1`, `game-server-2`: profile `game-servers`; до готовности Linux Unity build используют минимальный нейтральный placeholder image, который не имитирует игровой сервер. Оба сервиса закрепляют будущие имена, внутреннюю сеть и обязательные переменные `GAME_SERVER_ID`.

Плейсхолдеры game-server намеренно не запускаются в обычном `docker compose up`. Фаза 02 заменит их образом Unity Linux headless build и health/ready контрактом.

## Конфигурация и секреты

- `.env.example` содержит только безопасные примерные значения и обязательные переменные.
- `.env` игнорируется Git.
- Production-секреты не имеют значений по умолчанию в Compose или исходниках.
- Разработка PostgreSQL публикует порт только на localhost.

## Тестирование

1. Contracts test проверяет, что assembly contracts не ссылается на `UnityEngine` или `FishNet`.
2. API integration test через `WebApplicationFactory<Program>` проверяет `/health/live` и `/health/ready`: HTTP 200 и JSON `{"status":"ok"}`.
3. `dotnet test server/LH.Main.Server.sln` — обязательная проверка фазы.
4. При запущенном Docker daemon: `docker compose config` и `docker compose up --build postgres backend-api`, затем вызов health endpoints.

## Не входит

- Миграции и доступ backend к PostgreSQL.
- Аккаунты, JWT, профиль, очередь и match ticket.
- Linux Unity build, FishNet NetworkManager и реальный game-server health endpoint.
- CI pipeline; на этой фазе добавляются только локальные инструкции запуска.
