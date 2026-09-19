# Phase 03 Match Flow Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the local matchmaking contour that queues authenticated players, reserves one static game-server slot, returns a short-lived one-time ticket, and lets the assigned game-server validate that ticket.

**Architecture:** Keep backend as the source of truth for queue, slots, matches, and tickets. Use EF Core/PostgreSQL transactions for allocation safety, public DTOs in `LH.Main.Contracts`, and a minimal internal shared-key endpoint for game-server ticket validation.

**Tech Stack:** ASP.NET Core minimal APIs, JWT bearer auth, EF Core 10, Npgsql/PostgreSQL, Testcontainers PostgreSQL, Docker Compose, Unity/FishNet Phase 02 game-server containers.

**Spec:** `docs/superpowers/specs/2026-09-20-phase-03-match-flow-design.md`

## Global Constraints

- Do not implement dynamic Docker allocation, Kubernetes, production server identity, full lobby UI, real combat, rewards, reconnect policy beyond ticket validation, or result submission.
- Public transport DTOs live in `LH.Main.Contracts` and must remain free of Unity, FishNet, `MonoBehaviour`, `ScriptableObject`, and Unity vector types.
- Static Phase 02 slots are `game-server-1 -> localhost:7771` and `game-server-2 -> localhost:7772` in local Compose.
- Game-server internal ticket validation uses `X-Game-Server-Key` with backend configuration `GameServers:SharedKey`.
- Store only cryptographic ticket hashes in PostgreSQL; never store or log plaintext tickets.
- Repeated queue requests are idempotent for active queue/assignment state.
- Ticket validation must be one-time-use, player-bound, match-bound, server-bound, and expiry-bound.
- Slot reservation must be safe under concurrent enqueue requests.
- Do not log passwords, JWTs, plaintext tickets, or shared keys.
- Run `git status --short --branch` before editing and do not revert unrelated user changes.

---

## File Structure

- Create `server/src/LH.Main.Contracts/MatchmakingContracts.cs`: public queue/status/cancel response records and internal ticket validation records if shared with Unity later.
- Create `server/src/LH.Main.Backend.Api/Matchmaking/GameServerOptions.cs`: binds `GameServers` configuration, ticket lifetime, shared key, and static slot definitions.
- Create `server/src/LH.Main.Backend.Api/Matchmaking/MatchmakingService.cs`: owns public queue/status/cancel behavior and transactional allocation.
- Create `server/src/LH.Main.Backend.Api/Matchmaking/TicketService.cs`: creates random tickets, hashes them, and validates one-time use.
- Create `server/src/LH.Main.Backend.Api/Matchmaking/SystemClock.cs`: small injectable time abstraction for expiry tests.
- Create `server/src/LH.Main.Backend.Api/Persistence/Entities/GameServerSlot.cs`: static capacity row.
- Create `server/src/LH.Main.Backend.Api/Persistence/Entities/MatchQueueEntry.cs`: player queue/assignment row.
- Create `server/src/LH.Main.Backend.Api/Persistence/Entities/MatchSession.cs`: assigned match lifecycle row.
- Create `server/src/LH.Main.Backend.Api/Persistence/Entities/MatchTicket.cs`: hashed ticket row.
- Modify `server/src/LH.Main.Backend.Api/Persistence/AppDbContext.cs`: add DbSets, mappings, indexes, and relationships.
- Create EF migration under `server/src/LH.Main.Backend.Api/Persistence/Migrations/`: match flow schema.
- Modify `server/src/LH.Main.Backend.Api/Program.cs`: register options/services, seed slots, and map public/internal endpoints.
- Modify `server/tests/LH.Main.Backend.Api.Tests/`: add matchmaking endpoint, persistence, concurrency, and ticket validation tests.
- Modify `.env.example`, `docker-compose.yml`, and `README.md`: document Phase 03 config and local smoke commands.

---

### Task 1: Contracts and Configuration

**Files:**
- Create: `server/src/LH.Main.Contracts/MatchmakingContracts.cs`
- Create: `server/src/LH.Main.Backend.Api/Matchmaking/GameServerOptions.cs`
- Test: `server/tests/LH.Main.Contracts.Tests/ContractsAssemblyTests.cs`
- Test: `server/tests/LH.Main.Backend.Api.Tests/MatchmakingConfigurationTests.cs`

**Interfaces:**
- Produces: `MatchmakingStatusResponse`
- Produces: `MatchAssignmentResponse`
- Produces: `TicketValidationRequest`
- Produces: `TicketValidationResponse`
- Produces: `GameServerOptions`, `GameServerSlotOptions`
- Produces: `GameServerOptions.GetTicketLifetime() : TimeSpan`
- Produces: `GameServerOptions.Validate() : IReadOnlyList<string>`

- [ ] **Step 1: Read current state**

Run: `git status --short --branch`

Expected: clean branch or only unrelated user files. Do not touch unrelated files.

- [ ] **Step 2: Add contract tests for Unity-free DTO assembly**

Modify `server/tests/LH.Main.Contracts.Tests/ContractsAssemblyTests.cs` with assertions that the contracts assembly still has no Unity/FishNet references after adding matchmaking DTOs.

```csharp
[Fact]
public void ContractsAssemblyDoesNotReferenceUnityOrFishNet()
{
    var referencedAssemblies = typeof(LH.Main.Contracts.MatchmakingStatusResponse)
        .Assembly
        .GetReferencedAssemblies()
        .Select(assembly => assembly.Name)
        .ToArray();

    Assert.DoesNotContain("UnityEngine", referencedAssemblies);
    Assert.DoesNotContain("FishNet.Runtime", referencedAssemblies);
}
```

Run: `dotnet test server\LH.Main.Server.sln --configuration Release --filter ContractsAssemblyDoesNotReferenceUnityOrFishNet`

Expected: fail because `MatchmakingStatusResponse` does not exist.

- [ ] **Step 3: Create matchmaking DTOs**

Create `server/src/LH.Main.Contracts/MatchmakingContracts.cs`:

```csharp
namespace LH.Main.Contracts;

public sealed record MatchmakingStatusResponse(
    string Status,
    DateTimeOffset? QueuedAtUtc = null,
    MatchAssignmentResponse? Assignment = null);

public sealed record MatchAssignmentResponse(
    Guid MatchId,
    string ServerId,
    string PublicHost,
    int PublicPort,
    string Ticket,
    DateTimeOffset TicketExpiresAtUtc);

public sealed record TicketValidationRequest(
    string? Ticket,
    Guid MatchId,
    Guid PlayerId,
    string? ServerId);

public sealed record TicketValidationResponse(
    bool Valid,
    Guid? MatchId = null,
    Guid? PlayerId = null);
```

- [ ] **Step 4: Add configuration tests**

Create `server/tests/LH.Main.Backend.Api.Tests/MatchmakingConfigurationTests.cs`:

```csharp
using LH.Main.Backend.Api.Matchmaking;
using Xunit;

namespace LH.Main.Backend.Api.Tests;

public sealed class MatchmakingConfigurationTests
{
    [Fact]
    public void GameServerOptionsRequireSharedKeyTicketLifetimeAndSlots()
    {
        var options = new GameServerOptions();

        var errors = options.Validate();

        Assert.Contains("GameServers:SharedKey is required.", errors);
        Assert.Contains("GameServers:TicketLifetimeSeconds must be greater than zero.", errors);
        Assert.Contains("At least one GameServers:Slots entry is required.", errors);
    }

    [Fact]
    public void GameServerOptionsAcceptLocalTwoSlotConfiguration()
    {
        var options = new GameServerOptions
        {
            SharedKey = "local-shared-game-server-key",
            TicketLifetimeSeconds = 60,
            Slots =
            [
                new GameServerSlotOptions { ServerId = "game-server-1", PublicHost = "localhost", PublicPort = 7771 },
                new GameServerSlotOptions { ServerId = "game-server-2", PublicHost = "localhost", PublicPort = 7772 }
            ]
        };

        Assert.Empty(options.Validate());
        Assert.Equal(TimeSpan.FromSeconds(60), options.GetTicketLifetime());
    }
}
```

Run: `dotnet test server\LH.Main.Server.sln --configuration Release --filter MatchmakingConfigurationTests`

Expected: fail because options types do not exist.

- [ ] **Step 5: Create options types**

Create `server/src/LH.Main.Backend.Api/Matchmaking/GameServerOptions.cs`:

```csharp
namespace LH.Main.Backend.Api.Matchmaking;

public sealed class GameServerOptions
{
    public string? SharedKey { get; set; }

    public int TicketLifetimeSeconds { get; set; }

    public List<GameServerSlotOptions> Slots { get; set; } = [];

    public TimeSpan GetTicketLifetime() => TimeSpan.FromSeconds(TicketLifetimeSeconds);

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(SharedKey)) errors.Add("GameServers:SharedKey is required.");
        if (TicketLifetimeSeconds <= 0) errors.Add("GameServers:TicketLifetimeSeconds must be greater than zero.");
        if (Slots.Count == 0) errors.Add("At least one GameServers:Slots entry is required.");

        foreach (var slot in Slots)
        {
            if (string.IsNullOrWhiteSpace(slot.ServerId)) errors.Add("GameServers:Slots entries require ServerId.");
            if (string.IsNullOrWhiteSpace(slot.PublicHost)) errors.Add($"GameServers:Slots:{slot.ServerId}:PublicHost is required.");
            if (slot.PublicPort is <= 0 or > 65535) errors.Add($"GameServers:Slots:{slot.ServerId}:PublicPort must be from 1 to 65535.");
        }

        return errors;
    }
}

public sealed class GameServerSlotOptions
{
    public string? ServerId { get; set; }

    public string? PublicHost { get; set; }

    public int PublicPort { get; set; }
}
```

- [ ] **Step 6: Verify and commit**

Run: `dotnet test server\LH.Main.Server.sln --configuration Release`

Run: `git diff --check`

Commit:

```powershell
git add server/src/LH.Main.Contracts/MatchmakingContracts.cs server/src/LH.Main.Backend.Api/Matchmaking/GameServerOptions.cs server/tests/LH.Main.Contracts.Tests/ContractsAssemblyTests.cs server/tests/LH.Main.Backend.Api.Tests/MatchmakingConfigurationTests.cs
git commit -m "feat: add match flow contracts"
```

---

### Task 2: Match Flow Persistence and Slot Seeding

**Files:**
- Create: `server/src/LH.Main.Backend.Api/Persistence/Entities/GameServerSlot.cs`
- Create: `server/src/LH.Main.Backend.Api/Persistence/Entities/MatchQueueEntry.cs`
- Create: `server/src/LH.Main.Backend.Api/Persistence/Entities/MatchSession.cs`
- Create: `server/src/LH.Main.Backend.Api/Persistence/Entities/MatchTicket.cs`
- Create: `server/src/LH.Main.Backend.Api/Matchmaking/GameServerSlotSeeder.cs`
- Modify: `server/src/LH.Main.Backend.Api/Persistence/AppDbContext.cs`
- Modify: `server/src/LH.Main.Backend.Api/Program.cs`
- Test: `server/tests/LH.Main.Backend.Api.Tests/PersistenceMigrationTests.cs`
- Test: `server/tests/LH.Main.Backend.Api.Tests/GameServerSlotSeederTests.cs`

**Interfaces:**
- Consumes: `GameServerOptions`, `GameServerSlotOptions`
- Produces: entity classes listed above
- Produces: `GameServerSlotSeeder.SeedAsync(AppDbContext database, GameServerOptions options, CancellationToken cancellationToken) : Task`

- [ ] **Step 1: Add failing migration table test**

Modify `PersistenceMigrationTests.cs` with a test that applies migrations and checks table existence:

```csharp
[Fact]
public async Task StartupAppliesMatchFlowMigrationToEmptyDatabase()
{
    using var application = CreateApplicationWithConnection(database.ConnectionString);

    await using var connection = new NpgsqlConnection(database.ConnectionString);
    await connection.OpenAsync();
    foreach (var tableName in new[] { "game_server_slots", "match_queue_entries", "matches", "match_tickets" })
    {
        await using var command = new NpgsqlCommand(
            "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = @tableName)",
            connection);
        command.Parameters.AddWithValue("tableName", tableName);
        Assert.True((bool)(await command.ExecuteScalarAsync())!);
    }
}
```

Run: `dotnet test server\LH.Main.Server.sln --configuration Release --filter StartupAppliesMatchFlowMigrationToEmptyDatabase`

Expected: fail because tables do not exist.

- [ ] **Step 2: Create entity classes**

Create focused entity files with these exact properties:

```csharp
public sealed class GameServerSlot
{
    public string ServerId { get; set; } = string.Empty;
    public string PublicHost { get; set; } = string.Empty;
    public int PublicPort { get; set; }
    public string Status { get; set; } = GameServerSlotStatuses.Available;
    public Guid? CurrentMatchId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
```

Also add small status constants in the same files or adjacent static classes:

```csharp
public static class GameServerSlotStatuses
{
    public const string Available = "available";
    public const string Occupied = "occupied";
    public const string Disabled = "disabled";
}
```

Use equivalent constants for queue statuses `queued`, `assigned`, `cancelled`, `expired` and match statuses `reserved`, `ticket_validated`, `expired`.

- [ ] **Step 3: Map DbSets and indexes**

Modify `AppDbContext.cs`:

```csharp
public DbSet<GameServerSlot> GameServerSlots => Set<GameServerSlot>();
public DbSet<MatchQueueEntry> MatchQueueEntries => Set<MatchQueueEntry>();
public DbSet<MatchSession> Matches => Set<MatchSession>();
public DbSet<MatchTicket> MatchTickets => Set<MatchTicket>();
```

Map tables with snake_case columns, max lengths for strings, and indexes:

```csharp
entity.HasIndex(slot => slot.Status);
entity.HasIndex(ticket => ticket.TicketHash).IsUnique();
entity.HasIndex(entry => new { entry.PlayerId, entry.Status });
entity.HasIndex(match => new { match.PlayerId, match.Status });
```

Do not use filtered indexes unless the migration is explicitly verified against PostgreSQL.

- [ ] **Step 4: Create migration**

Run:

```powershell
dotnet ef migrations add AddMatchFlow --project server\src\LH.Main.Backend.Api\LH.Main.Backend.Api.csproj --startup-project server\src\LH.Main.Backend.Api\LH.Main.Backend.Api.csproj --context AppDbContext
```

Expected: migration creates the four tables and FK constraints to `users` and `game_server_slots`.

- [ ] **Step 5: Add slot seeder test**

Create `GameServerSlotSeederTests.cs` proving configured slots are inserted and existing slot endpoint metadata updates without overwriting `CurrentMatchId`:

```csharp
[Fact]
public async Task SeedAsyncUpsertsConfiguredSlotsWithoutClearingCurrentMatch()
{
    await using var context = CreateDbContext();
    var matchId = Guid.NewGuid();
    context.GameServerSlots.Add(new GameServerSlot
    {
        ServerId = "game-server-1",
        PublicHost = "old-host",
        PublicPort = 1111,
        Status = GameServerSlotStatuses.Occupied,
        CurrentMatchId = matchId,
        CreatedAtUtc = DateTimeOffset.UtcNow,
        UpdatedAtUtc = DateTimeOffset.UtcNow
    });
    await context.SaveChangesAsync();

    await GameServerSlotSeeder.SeedAsync(context, LocalOptions(), CancellationToken.None);

    var slot = await context.GameServerSlots.SingleAsync(s => s.ServerId == "game-server-1");
    Assert.Equal("localhost", slot.PublicHost);
    Assert.Equal(7771, slot.PublicPort);
    Assert.Equal(GameServerSlotStatuses.Occupied, slot.Status);
    Assert.Equal(matchId, slot.CurrentMatchId);
}
```

- [ ] **Step 6: Implement seeder and startup registration**

Create `GameServerSlotSeeder` and call it after `MigrateAsync()` in `Program.cs` only when `GameServers` config validates. If config is absent, skip seeding so auth-only tests can still run.

- [ ] **Step 7: Verify and commit**

Run: `dotnet test server\LH.Main.Server.sln --configuration Release`

Run: `git diff --check`

Commit:

```powershell
git add server/src/LH.Main.Backend.Api server/tests/LH.Main.Backend.Api.Tests
git commit -m "feat: add match flow persistence"
```

---

### Task 3: Ticket Service

**Files:**
- Create: `server/src/LH.Main.Backend.Api/Matchmaking/SystemClock.cs`
- Create: `server/src/LH.Main.Backend.Api/Matchmaking/TicketService.cs`
- Test: `server/tests/LH.Main.Backend.Api.Tests/TicketServiceTests.cs`

**Interfaces:**
- Consumes: `MatchTicket`, `MatchTicketStatuses` from Task 2
- Produces: `ISystemClock.UtcNow : DateTimeOffset`
- Produces: `TicketIssueResult(string PlaintextTicket, string TicketHash, DateTimeOffset ExpiresAtUtc)`
- Produces: `TicketService.Issue(Guid matchId, Guid playerId, string serverId, TimeSpan lifetime) : TicketIssueResult`
- Produces: `TicketService.Hash(string plaintextTicket) : string`

- [ ] **Step 1: Write ticket creation tests**

Create tests verifying two issued tickets differ, hashes differ from plaintext, and expiry uses injected clock.

```csharp
[Fact]
public void IssueCreatesOpaqueTicketAndHashWithExpectedExpiry()
{
    var clock = new FixedClock(DateTimeOffset.Parse("2026-09-20T00:00:00Z"));
    var service = new TicketService(clock);

    var ticket = service.Issue(Guid.NewGuid(), Guid.NewGuid(), "game-server-1", TimeSpan.FromSeconds(60));

    Assert.False(string.IsNullOrWhiteSpace(ticket.PlaintextTicket));
    Assert.NotEqual(ticket.PlaintextTicket, ticket.TicketHash);
    Assert.Equal(DateTimeOffset.Parse("2026-09-20T00:01:00Z"), ticket.ExpiresAtUtc);
}
```

Run: `dotnet test server\LH.Main.Server.sln --configuration Release --filter TicketServiceTests`

Expected: fail because service does not exist.

- [ ] **Step 2: Implement clock and ticket service**

Use `RandomNumberGenerator.GetBytes(32)`, base64url encoding without padding, and SHA-256 hash encoded as hex.

```csharp
public sealed record TicketIssueResult(string PlaintextTicket, string TicketHash, DateTimeOffset ExpiresAtUtc);
```

Do not log plaintext ticket in this service.

- [ ] **Step 3: Verify and commit**

Run: `dotnet test server\LH.Main.Server.sln --configuration Release --filter TicketServiceTests`

Run: `dotnet test server\LH.Main.Server.sln --configuration Release`

Run: `git diff --check`

Commit:

```powershell
git add server/src/LH.Main.Backend.Api/Matchmaking server/tests/LH.Main.Backend.Api.Tests/TicketServiceTests.cs
git commit -m "feat: add match ticket service"
```

---

### Task 4: Matchmaking Allocation Service

**Files:**
- Create: `server/src/LH.Main.Backend.Api/Matchmaking/MatchmakingService.cs`
- Test: `server/tests/LH.Main.Backend.Api.Tests/MatchmakingServiceTests.cs`
- Test: `server/tests/LH.Main.Backend.Api.Tests/MatchmakingConcurrencyTests.cs`

**Interfaces:**
- Consumes: `AppDbContext`, `TicketService`, `ISystemClock`, `GameServerOptions`
- Produces: `MatchmakingService.EnqueueAsync(Guid playerId, CancellationToken cancellationToken) : Task<MatchmakingStatusResponse>`
- Produces: `MatchmakingService.GetStatusAsync(Guid playerId, CancellationToken cancellationToken) : Task<MatchmakingStatusResponse>`
- Produces: `MatchmakingService.CancelAsync(Guid playerId, CancellationToken cancellationToken) : Task<MatchmakingCancelResult>`
- Produces: `MatchmakingCancelResult(bool ConflictAssigned, MatchmakingStatusResponse Response)`

- [ ] **Step 1: Write service tests for idempotent enqueue and assignment**

Create tests that insert a user/profile, seed two slots, call `EnqueueAsync`, and assert assignment contains `game-server-1` or `game-server-2` and one match/ticket row exists.

```csharp
[Fact]
public async Task EnqueueAssignsAvailableSlotAndIsIdempotentForSamePlayer()
{
    var playerId = await CreateUserAsync();
    await SeedSlotsAsync("game-server-1", "game-server-2");
    var service = CreateService();

    var first = await service.EnqueueAsync(playerId, CancellationToken.None);
    var second = await service.EnqueueAsync(playerId, CancellationToken.None);

    Assert.Equal("assigned", first.Status);
    Assert.Equal(first.Assignment!.MatchId, second.Assignment!.MatchId);
    Assert.Equal(1, await Context.Matches.CountAsync());
    Assert.Equal(1, await Context.MatchTickets.CountAsync());
}
```

Run: `dotnet test server\LH.Main.Server.sln --configuration Release --filter MatchmakingServiceTests`

Expected: fail because service does not exist.

- [ ] **Step 2: Write no-capacity and cancel tests**

Cover these exact cases:

- both slots occupied -> `Status == "queued"` and no ticket plaintext exposed;
- cancel queued entry -> `Status == "cancelled"` or `"none"` by implementation choice, but repeated cancel returns same non-error state;
- cancel assigned entry -> `ConflictAssigned == true` and slot remains occupied.

- [ ] **Step 3: Write concurrency test**

Use a single configured available slot and two separate service scopes with two users. Start both enqueue tasks together with `Task.WhenAll`. Assert at most one assigned response has the single server ID and the other response is `queued`.

```csharp
Assert.Equal(1, results.Count(result => result.Status == "assigned"));
Assert.Equal(1, results.Count(result => result.Status == "queued"));
Assert.Equal(1, await database.Matches.CountAsync());
```

- [ ] **Step 4: Implement allocation transaction**

Implement `EnqueueAsync` with an EF Core transaction. Prefer PostgreSQL row-level safety with an atomic conditional update:

```csharp
var slot = await database.GameServerSlots
    .Where(candidate => candidate.Status == GameServerSlotStatuses.Available)
    .OrderBy(candidate => candidate.ServerId)
    .FirstOrDefaultAsync(cancellationToken);
```

Then update it only if still available. If `SaveChangesAsync` affects a concurrency token or conditional update count differently than expected, retry once by re-reading state. If EF cannot express the atomic guard cleanly, use `ExecuteUpdateAsync` with `WHERE server_id = @id AND status = 'available'` and verify exactly one row updated before creating the match.

- [ ] **Step 5: Register service dependencies**

In `Program.cs`, register:

```csharp
builder.Services.Configure<GameServerOptions>(builder.Configuration.GetSection("GameServers"));
builder.Services.AddSingleton<ISystemClock, SystemClock>();
builder.Services.AddScoped<TicketService>();
builder.Services.AddScoped<MatchmakingService>();
```

- [ ] **Step 6: Verify and commit**

Run: `dotnet test server\LH.Main.Server.sln --configuration Release --filter MatchmakingServiceTests`

Run: `dotnet test server\LH.Main.Server.sln --configuration Release --filter MatchmakingConcurrencyTests`

Run: `dotnet test server\LH.Main.Server.sln --configuration Release`

Run: `git diff --check`

Commit:

```powershell
git add server/src/LH.Main.Backend.Api/Matchmaking server/src/LH.Main.Backend.Api/Program.cs server/tests/LH.Main.Backend.Api.Tests
git commit -m "feat: add matchmaking allocation service"
```

---

### Task 5: Public Matchmaking Endpoints

**Files:**
- Modify: `server/src/LH.Main.Backend.Api/Program.cs`
- Test: `server/tests/LH.Main.Backend.Api.Tests/MatchmakingEndpointsTests.cs`
- Test: `server/tests/LH.Main.Backend.Api.Tests/HealthEndpointsTests.cs`

**Interfaces:**
- Consumes: `MatchmakingService.EnqueueAsync`, `GetStatusAsync`, `CancelAsync`
- Produces: `POST /v1/matchmaking/queue`
- Produces: `GET /v1/matchmaking/status`
- Produces: `POST /v1/matchmaking/cancel`

- [ ] **Step 1: Write auth-required endpoint tests**

Create tests asserting all three endpoints return `401 Unauthorized` without bearer token.

```csharp
[Theory]
[InlineData("POST", "/v1/matchmaking/queue")]
[InlineData("GET", "/v1/matchmaking/status")]
[InlineData("POST", "/v1/matchmaking/cancel")]
public async Task MatchmakingEndpointsRequireAuthentication(string method, string path)
{
    using var application = CreateApplication();
    using var client = application.CreateClient();
    using var request = new HttpRequestMessage(new HttpMethod(method), path);

    using var response = await client.SendAsync(request);

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
}
```

Run: `dotnet test server\LH.Main.Server.sln --configuration Release --filter MatchmakingEndpointsRequireAuthentication`

Expected: fail with `404 NotFound` because endpoints do not exist.

- [ ] **Step 2: Write authenticated flow endpoint test**

Register/login a dev user, call queue, status, and cancel. For a newly assigned user, cancel must return `409` with problem code `match_already_assigned`.

- [ ] **Step 3: Map endpoints**

Add endpoint mappings inside the existing auth-enabled block in `Program.cs`. Extract user ID from `JwtRegisteredClaimNames.Sub` exactly like `/v1/profile`.

Return shapes:

```csharp
app.MapPost("/v1/matchmaking/queue", async (...) => Results.Ok(await service.EnqueueAsync(userId, cancellationToken))).RequireAuthorization();
app.MapGet("/v1/matchmaking/status", async (...) => Results.Ok(await service.GetStatusAsync(userId, cancellationToken))).RequireAuthorization();
app.MapPost("/v1/matchmaking/cancel", async (...) => result.ConflictAssigned ? Results.Problem(statusCode: 409, extensions: new Dictionary<string, object?> { ["code"] = "match_already_assigned" }) : Results.Ok(result.Response)).RequireAuthorization();
```

- [ ] **Step 4: Extend OpenAPI test**

Update existing OpenAPI tests to assert public matchmaking endpoints include bearer security.

- [ ] **Step 5: Verify and commit**

Run: `dotnet test server\LH.Main.Server.sln --configuration Release --filter MatchmakingEndpointsTests`

Run: `dotnet test server\LH.Main.Server.sln --configuration Release --filter OpenApi`

Run: `dotnet test server\LH.Main.Server.sln --configuration Release`

Run: `git diff --check`

Commit:

```powershell
git add server/src/LH.Main.Backend.Api/Program.cs server/tests/LH.Main.Backend.Api.Tests
git commit -m "feat: add matchmaking endpoints"
```

---

### Task 6: Internal Ticket Validation Endpoint

**Files:**
- Modify: `server/src/LH.Main.Backend.Api/Matchmaking/MatchmakingService.cs`
- Modify: `server/src/LH.Main.Backend.Api/Program.cs`
- Test: `server/tests/LH.Main.Backend.Api.Tests/TicketValidationEndpointsTests.cs`

**Interfaces:**
- Consumes: `TicketValidationRequest`, `TicketValidationResponse`, `GameServerOptions.SharedKey`
- Produces: `MatchmakingService.ValidateTicketAsync(TicketValidationRequest request, string serverKey, CancellationToken cancellationToken) : Task<TicketValidationEndpointResult>`
- Produces: `POST /internal/v1/matches/tickets/validate`

- [ ] **Step 1: Write server-key tests**

Create endpoint tests asserting missing/wrong `X-Game-Server-Key` returns `401 Unauthorized` and does not reveal ticket validity.

- [ ] **Step 2: Write one-time validation tests**

Use public queue endpoint or service setup to create assignment, then call internal validation twice:

```csharp
Assert.Equal(HttpStatusCode.OK, first.StatusCode);
Assert.True((await first.Content.ReadFromJsonAsync<TicketValidationResponse>())!.Valid);
Assert.Equal(HttpStatusCode.OK, second.StatusCode);
Assert.False((await second.Content.ReadFromJsonAsync<TicketValidationResponse>())!.Valid);
```

- [ ] **Step 3: Write invalid tuple tests**

Cover wrong player, wrong match, wrong server, expired ticket, and unknown ticket. All return `200 OK` with `Valid == false`.

- [ ] **Step 4: Implement validation**

Hash the supplied plaintext ticket, load matching ticket, verify tuple/expiry/consumed, and update `ConsumedAtUtc` plus match status in a transaction. Do not return details on failure.

- [ ] **Step 5: Map internal endpoint**

Add:

```csharp
app.MapPost("/internal/v1/matches/tickets/validate", async (
    TicketValidationRequest request,
    HttpContext context,
    MatchmakingService service,
    CancellationToken cancellationToken) =>
{
    var serverKey = context.Request.Headers["X-Game-Server-Key"].ToString();
    var result = await service.ValidateTicketAsync(request, serverKey, cancellationToken);
    return result.Unauthorized ? Results.Unauthorized() : Results.Ok(result.Response);
});
```

- [ ] **Step 6: Verify and commit**

Run: `dotnet test server\LH.Main.Server.sln --configuration Release --filter TicketValidationEndpointsTests`

Run: `dotnet test server\LH.Main.Server.sln --configuration Release`

Run: `git diff --check`

Commit:

```powershell
git add server/src/LH.Main.Backend.Api/Matchmaking server/src/LH.Main.Backend.Api/Program.cs server/tests/LH.Main.Backend.Api.Tests/TicketValidationEndpointsTests.cs
git commit -m "feat: validate match tickets"
```

---

### Task 7: Local Configuration, Documentation, and Smoke

**Files:**
- Modify: `.env.example`
- Modify: `docker-compose.yml`
- Modify: `README.md`
- Optional test: `server/tests/LH.Main.Backend.Api.Tests/HealthEndpointsTests.cs`

**Interfaces:**
- Consumes: backend `GameServers` configuration names
- Produces: local Compose env for two static slots and shared key
- Produces: README Phase 03 smoke commands

- [ ] **Step 1: Add local env values**

Add to `.env.example`:

```text
GAME_SERVER_SHARED_KEY=local-game-server-shared-key-change-before-deployment
GAME_SERVER_TICKET_LIFETIME_SECONDS=60
GAME_SERVER_1_ID=game-server-1
GAME_SERVER_2_ID=game-server-2
```

- [ ] **Step 2: Wire backend-api Compose environment**

Add these backend env mappings in `docker-compose.yml`:

```yaml
GameServers__SharedKey: ${GAME_SERVER_SHARED_KEY}
GameServers__TicketLifetimeSeconds: ${GAME_SERVER_TICKET_LIFETIME_SECONDS}
GameServers__Slots__0__ServerId: ${GAME_SERVER_1_ID}
GameServers__Slots__0__PublicHost: ${GAME_SERVER_PUBLIC_HOST}
GameServers__Slots__0__PublicPort: ${GAME_SERVER_1_NETWORK_HOST_PORT}
GameServers__Slots__1__ServerId: ${GAME_SERVER_2_ID}
GameServers__Slots__1__PublicHost: ${GAME_SERVER_PUBLIC_HOST}
GameServers__Slots__1__PublicPort: ${GAME_SERVER_2_NETWORK_HOST_PORT}
```

- [ ] **Step 3: Document Phase 03 local smoke**

Add README commands to start backend + game servers, register/login two users, enqueue, inspect status, validate ticket with `X-Game-Server-Key`, and replay validation failure. Use `curl` examples and redact concrete ticket values as shell variables.

- [ ] **Step 4: Run final verification**

Run:

```powershell
dotnet test server\LH.Main.Server.sln --configuration Release
docker compose --env-file .env.example --profile game-servers config
git diff --check
```

If Unity Linux artifact exists locally, also run:

```powershell
docker compose --env-file .env.example --profile game-servers up --build --detach --wait backend-api game-server-1 game-server-2
```

Then perform manual smoke with two dev users and record exact results in the task report.

- [ ] **Step 5: Commit**

```powershell
git add .env.example docker-compose.yml README.md server/tests/LH.Main.Backend.Api.Tests
git commit -m "docs: document match flow smoke"
```

---

## Final Branch Verification

After all tasks are approved, run:

```powershell
dotnet test server\LH.Main.Server.sln --configuration Release
docker compose --env-file .env.example --profile game-servers config
git diff --check 0ef52bd..HEAD
git status --short --branch
```

If the Unity Linux player artifact exists, run the full local smoke:

```powershell
docker compose --env-file .env.example --profile game-servers up --build --detach --wait backend-api game-server-1 game-server-2
curl.exe --fail --silent http://127.0.0.1:8080/health/ready
curl.exe --fail --silent http://127.0.0.1:8091/health/ready
curl.exe --fail --silent http://127.0.0.1:8092/health/ready
```

Then register/login two dev users, enqueue both, verify different `serverId`/ports when capacity allows, validate each ticket once, and verify replay fails.
