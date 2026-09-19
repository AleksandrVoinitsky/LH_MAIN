# Phase 01 Backend Identity Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Реализовать постоянного dev-пользователя: PostgreSQL migrations, регистрацию, login с JWT и защищённый профиль.

**Architecture:** Один `backend-api` использует EF Core/Npgsql для трёх identity-таблиц. API остаётся Minimal API; contracts содержат только HTTP DTO. Миграции применяются при старте до открытия HTTP listener, а тесты используют отдельный PostgreSQL Testcontainer.

**Tech Stack:** .NET 10, ASP.NET Core Minimal API, EF Core 10, Npgsql, JWT Bearer, `PasswordHasher<TUser>`, xUnit, Testcontainers PostgreSQL.

**Spec:** `docs/superpowers/specs/2026-09-19-phase-01-backend-design.md`

## Global Constraints

- Не добавлять ссылки на Unity, FishNet или `UnityEngine` в `server/`.
- Не реализовывать email, recovery, роли, инвентарь, очередь, матчи или Unity-подключение.
- Пароли, JWT и password hashes не выводятся в ответы ошибок, OpenAPI-примеры или логи.
- Production JWT secrets не имеют значений по умолчанию в исходниках или Compose.
- Каждая новая функция: тест RED, минимальная реализация, GREEN.

---

## File Structure

```text
server/src/LH.Main.Contracts/
  AuthContracts.cs                 # register/login DTO и profile DTO
server/src/LH.Main.Backend.Api/
  AuthenticationOptions.cs          # обязательная JWT/dev-конфигурация
  Persistence/AppDbContext.cs       # EF Core context
  Persistence/Entities/*.cs         # User, PasswordCredential, PlayerProfile
  Persistence/Migrations/*.cs       # версия схемы identity
  Identity/IdentityService.cs       # register, credential verify, JWT
  Observability/CorrelationIdMiddleware.cs
  Program.cs                        # DI, migrations, auth, health, endpoints, OpenAPI
server/tests/LH.Main.Backend.Api.Tests/
  PostgreSqlFixture.cs              # один PostgreSQL Testcontainer на test collection
  AuthEndpointsTests.cs             # register/login/profile интеграционные проверки
  ReadinessAndCorrelationTests.cs   # readiness и correlation ID
docker-compose.yml                  # конфигурация JWT/dev registration
.env.example                        # безопасные локальные значения
README.md                           # команды Phase 01
```

### Task 1: Подключить persistence и миграцию identity-схемы

**Files:** API project file, entities/context/migration, `Program.cs`, test fixture и migration tests.

**Produces:** `AppDbContext`, tables `users`, `password_credentials`, `player_profiles`; startup migration до HTTP listener.

- [ ] Написать integration test, который запускает API с пустой Testcontainers PostgreSQL и проверяет наличие трёх таблиц.
- [ ] Запустить test и подтвердить RED из-за отсутствия context/migration.
- [ ] Добавить EF Core/Npgsql, entities и fluent mapping по спецификации; создать initial migration.
- [ ] Добавить startup migration через scoped `Database.MigrateAsync()` до `app.Run()`.
- [ ] Повторно запустить migration test; добавить проверку повторного запуска без удаления test-записи.
- [ ] Запустить оба теста и подтвердить GREEN.
- [ ] Commit: `feat: add identity persistence migration`.

### Task 2: Добавить contracts и dev registration

**Files:** contracts project, `IdentityService`, registration endpoint, `AuthEndpointsTests`.

**Consumes:** `AppDbContext` из Task 1.

**Produces:** `POST /v1/auth/dev-register`, `RegisterRequest`, `PlayerProfileResponse`.

- [ ] Написать failing tests: корректная регистрация возвращает `201`, профиль и созданные credential/profile; case-insensitive duplicate возвращает `409 username_already_exists`; невалидные логин/пароль возвращают `400`.
- [ ] Запустить tests и подтвердить RED.
- [ ] Добавить независимые DTO и validator: username `3..64`, `[A-Za-z0-9_]`; password `12..128`.
- [ ] Реализовать transaction, `PasswordHasher<User>`, unique-conflict обработку и endpoint, доступный только при `Authentication:EnableDevRegistration=true`; выключенный endpoint возвращает `404`.
- [ ] Запустить registration tests и подтвердить GREEN.
- [ ] Commit: `feat: add dev user registration`.

### Task 3: Реализовать JWT login и защищённый профиль

**Files:** `AuthenticationOptions`, `IdentityService`, `Program.cs`, contracts, `AuthEndpointsTests`.

**Consumes:** user/credential/profile persistence и registration из Tasks 1–2.

**Produces:** `POST /v1/auth/dev-login`, `GET /v1/profile`, bearer authentication.

- [ ] Написать failing tests: registration -> login возвращает JWT и `expiresAtUtc`; действующий JWT возвращает профиль; неверный пароль и отсутствующий user возвращают одинаковый `401 invalid_credentials`; отсутствующий, испорченный, истёкший JWT и несуществующий `sub` возвращают `401`.
- [ ] Запустить tests и подтвердить RED.
- [ ] Добавить обязательную options validation и JWT bearer configuration: HS256, issuer/audience, lifetime, zero clock skew в tests.
- [ ] Реализовать JWT creation c `sub`, `iss`, `aud`, `iat`, `nbf`, `exp`, нейтральную login error response и profile lookup по `sub`.
- [ ] Запустить auth/profile tests и подтвердить GREEN.
- [ ] Commit: `feat: add jwt login and profile`.

### Task 4: Добавить readiness, correlation ID, OpenAPI и локальную конфигурацию

**Files:** correlation middleware/tests, `Program.cs`, Compose, `.env.example`, README.

**Consumes:** DbContext и API endpoints предыдущих задач.

**Produces:** реальная DB readiness, `X-Correlation-ID`, development OpenAPI и документированный local run.

- [ ] Написать failing tests: readiness при недоступной DB — `503`; с PostgreSQL — `200`; корректный UUID correlation ID возвращается без изменения, некорректный заменяется валидным UUID.
- [ ] Запустить tests и подтвердить RED.
- [ ] Добавить DB health check для `/health/ready` и middleware correlation ID, устанавливающий `TraceIdentifier`, response header и logging scope.
- [ ] Добавить Development-only OpenAPI с bearer scheme; обновить Compose/.env.example с безопасными local JWT options и `EnableDevRegistration=true`; обновить README командами register/login/profile.
- [ ] Запустить readiness/correlation tests и подтвердить GREEN.
- [ ] Commit: `feat: add backend observability configuration`.

### Task 5: Полная проверка и публикация

**Files:** только правки, необходимые по результатам проверки.

- [ ] Запустить `dotnet test server/LH.Main.Server.sln --configuration Release`.
- [ ] Запустить `dotnet build server/LH.Main.Server.sln --configuration Release --no-restore`.
- [ ] Запустить `docker compose --env-file .env.example config`.
- [ ] При доступном Docker daemon выполнить `docker compose up --build -d postgres backend-api`, проверить `/health/ready`, register/login/profile, затем `docker compose down`.
- [ ] При недоступном daemon зафиксировать внешний блокер без mock/обхода.
- [ ] Выполнить `git diff --check`, проверить отсутствие секретов, `bin`, `obj`, Unity-generated файлов и чужих локальных изменений.
- [ ] Commit: `feat: add backend identity foundation`; push ветки и интеграция после review.

## Self-review

- Схема, migrations, API, JWT, readiness, correlation ID, OpenAPI, Compose и Testcontainers покрыты Tasks 1–4.
- Task 5 проверяет все критерии готовности, кроме Docker runtime при недоступном daemon, который фиксируется как окруженческий блокер.
- В план не включены функции Phase 02+.
