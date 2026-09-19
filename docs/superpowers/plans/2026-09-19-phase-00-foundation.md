# Phase 00 Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Создать тестируемый .NET backend skeleton и Docker Compose основу для PostgreSQL и будущих Unity dedicated server.

**Architecture:** Contracts отделены от Unity и используются API. ASP.NET Core предоставляет минимальные health endpoints. Docker Compose запускает PostgreSQL и API, а имена будущих игровых серверов закреплены profile-заглушками до фазы 02.

**Tech Stack:** .NET 10 LTS, ASP.NET Core Minimal API, xUnit, PostgreSQL 17, Docker Compose v2.

**Spec:** `docs/superpowers/specs/2026-09-19-phase-00-foundation-design.md`

## Global Constraints

- Не добавлять Unity/FishNet зависимости в `server/`.
- Не реализовывать доступ к PostgreSQL, аккаунты, JWT или матчмейкинг.
- Не хранить секреты в Git; `.env` игнорируется.
- Game-server контейнеры остаются выключенными по умолчанию и не имитируют готовый Unity dedicated server.
- Все новые функции получают тест, который был запущен и упал до реализации.

---

### Task 1: Создать solution и contracts через TDD

**Files:**
- Create: `server/LH.Main.Server.sln`
- Create: `server/Directory.Build.props`
- Create: `server/src/LH.Main.Contracts/LH.Main.Contracts.csproj`
- Create: `server/src/LH.Main.Contracts/HealthStatusResponse.cs`
- Create: `server/tests/LH.Main.Contracts.Tests/LH.Main.Contracts.Tests.csproj`
- Create: `server/tests/LH.Main.Contracts.Tests/ContractsAssemblyTests.cs`

- [ ] **Step 1: Создать failing test независимости contracts**

```csharp
[Fact]
public void ContractsAssemblyDoesNotReferenceUnityOrFishNet()
{
    var references = typeof(HealthStatusResponse).Assembly
        .GetReferencedAssemblies()
        .Select(reference => reference.Name);

    Assert.DoesNotContain("UnityEngine", references);
    Assert.DoesNotContain("FishNet", references);
}
```

- [ ] **Step 2: Запустить тест и подтвердить RED**

Run: `dotnet test server/tests/LH.Main.Contracts.Tests/LH.Main.Contracts.Tests.csproj`

Expected: FAIL, поскольку `HealthStatusResponse` ещё не существует.

- [ ] **Step 3: Реализовать минимальный contract**

```csharp
namespace LH.Main.Contracts;

public sealed record HealthStatusResponse(string Status);
```

- [ ] **Step 4: Запустить contracts test и подтвердить GREEN**

Run: `dotnet test server/tests/LH.Main.Contracts.Tests/LH.Main.Contracts.Tests.csproj`

Expected: PASS.

### Task 2: Создать и проверить health API через TDD

**Files:**
- Create: `server/src/LH.Main.Backend.Api/LH.Main.Backend.Api.csproj`
- Create: `server/src/LH.Main.Backend.Api/Program.cs`
- Create: `server/tests/LH.Main.Backend.Api.Tests/LH.Main.Backend.Api.Tests.csproj`
- Create: `server/tests/LH.Main.Backend.Api.Tests/HealthEndpointsTests.cs`

- [ ] **Step 1: Написать failing integration tests**

```csharp
[Theory]
[InlineData("/health/live")]
[InlineData("/health/ready")]
public async Task HealthEndpointReturnsOkStatus(string path)
{
    using var response = await _client.GetAsync(path);
    var body = await response.Content.ReadFromJsonAsync<HealthStatusResponse>();

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.Equal("ok", body?.Status);
}
```

- [ ] **Step 2: Запустить API test и подтвердить RED**

Run: `dotnet test server/tests/LH.Main.Backend.Api.Tests/LH.Main.Backend.Api.Tests.csproj`

Expected: FAIL, потому что API project и `Program` отсутствуют.

- [ ] **Step 3: Реализовать минимальный API**

```csharp
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/health/live", () => Results.Ok(new HealthStatusResponse("ok")));
app.MapGet("/health/ready", () => Results.Ok(new HealthStatusResponse("ok")));

app.Run();

public partial class Program;
```

- [ ] **Step 4: Запустить API test и подтвердить GREEN**

Run: `dotnet test server/tests/LH.Main.Backend.Api.Tests/LH.Main.Backend.Api.Tests.csproj`

Expected: PASS.

### Task 3: Добавить контейнерную среду

**Files:**
- Create: `docker/backend/Dockerfile`
- Create: `docker/game-server/Dockerfile`
- Create: `docker-compose.yml`
- Create: `.env.example`
- Modify: `.gitignore`
- Modify: `README.md`

- [ ] **Step 1: Создать Compose и .env.example по спецификации**

Backend image собирается multi-stage Dockerfile на `mcr.microsoft.com/dotnet/sdk:10.0` и запускается на `mcr.microsoft.com/dotnet/aspnet:10.0`. PostgreSQL использует `postgres:17-alpine`. Game-server placeholders используют `alpine:3.21` только с profile `game-servers`.

- [ ] **Step 2: Проверить Compose синтаксис**

Run: `docker compose config`

Expected: compose configuration rendered without errors.

- [ ] **Step 3: При доступном Docker daemon проверить backend и PostgreSQL**

Run: `docker compose up --build -d postgres backend-api && curl http://localhost:8080/health/ready`

Expected: PostgreSQL healthy; backend returns JSON status `ok`.

- [ ] **Step 4: Если Docker daemon недоступен, зафиксировать окруженческий блокер**

Run: `docker version --format "{{.Server.Version}}"`

Expected: connection failure is recorded; no code workaround or mock daemon is added.

### Task 4: Выполнить полный локальный контроль качества

**Files:**
- Modify: `README.md`
- Modify: `docs/07-development/phase-00-foundation.md` only if actual commands differ from specification.

- [ ] **Step 1: Запустить полный test suite**

Run: `dotnet test server/LH.Main.Server.sln --configuration Release`

Expected: all tests pass with no warnings.

- [ ] **Step 2: Проверить release build**

Run: `dotnet build server/LH.Main.Server.sln --configuration Release --no-restore`

Expected: build succeeds with zero warnings and errors.

- [ ] **Step 3: Проверить Git-границы**

Run: `git diff --check && git status --short`

Expected: only phase 00 source/config/docs files are modified; no `.env`, `bin`, `obj`, Unity caches or generated IDE files are present.

- [ ] **Step 4: Commit**

Run: `git add server docker docker-compose.yml .env.example .gitignore README.md docs && git commit -m "feat: add backend foundation"

Expected: a single focused phase 00 commit.

## Plan Self-Review

- Specification coverage: structure — Task 1; API — Task 2; Compose/secrets — Task 3; validation/docs/commit — Task 4.
- No feature from phase 01+ is included.
- Contracts, API endpoint names and health response type are consistent across tasks.
