# Phase 04 Networked Core Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the Unity/FishNet networked match foundation where backend-issued tickets admit clients, the server owns player spawn and movement, and 64 bot/headless clients complete a reproducible load scenario with metrics.

**Architecture:** Keep the backend as the source of truth for assignment and one-time ticket validation. Add focused Unity runtime components for admission payload parsing, backend validation, FishNet connection admission, server-owned player registry, movement validation, observer management, and metrics reporting.

**Tech Stack:** Unity, FishNet, Tugboat, C# edit-mode tests where available, ASP.NET Core backend ticket validation endpoint, Docker Compose local services, PowerShell-compatible local smoke commands.

**Spec:** `docs/superpowers/specs/2026-09-20-phase-04-networked-core-design.md`

## Global Constraints

- Target match scale is 64 clients; manual debugging on fewer clients is allowed, but Phase 04 is not complete without a reproducible 64-client report.
- Use FishNet for transport, RPC, replication, observer management, and dedicated-server networking.
- Do not add custom transport, custom snapshots, or custom replication.
- Do not implement final characters, animation, weapons, combat, damage, loot, inventory, AI, content maps, match result submission, rewards, reconnect policy, dynamic game-server allocation, or production orchestration.
- Backend ticket validation uses `POST /internal/v1/matches/tickets/validate` and `X-Game-Server-Key`.
- Do not log passwords, JWTs, plaintext tickets, or shared keys.
- Public transport DTOs must remain free of Unity, FishNet, `MonoBehaviour`, `ScriptableObject`, and Unity vector types.
- Run `git status --short --branch` before editing and do not revert unrelated user changes.
- Do not commit unless the user explicitly asks for commits.

---

## File Structure

- Create `Assets/Scripts/Networking/MatchAdmissionPayload.cs`: Unity-free connection payload DTO and JSON parser.
- Create `Assets/Scripts/Networking/MatchTicketValidator.cs`: backend HTTP adapter for internal ticket validation.
- Create `Assets/Scripts/Networking/MatchAdmissionResult.cs`: explicit admission result type used by authenticator and tests.
- Create `Assets/Scripts/Networking/GameServerAuthenticator.cs`: FishNet admission component for decoding payloads, validating tickets, and accepting/rejecting connections.
- Create `Assets/Scripts/Networking/ServerPlayerRegistry.cs`: maps validated connections to server-owned player objects and cleanup.
- Create `Assets/Scripts/Networking/NetworkPlayerController.cs`: technical server-authoritative player controller.
- Create `Assets/Scripts/Networking/MovementCommand.cs`: input intent struct and validation constants.
- Create `Assets/Scripts/Networking/MovementValidator.cs`: deterministic movement validation independent of scene objects.
- Create `Assets/Scripts/Networking/GameServerMetrics.cs`: counters and tick/memory/traffic fields used by status and reports.
- Modify `Assets/Scripts/Server/GameServerConfig.cs`: add backend URL, shared key, and validation timeout configuration.
- Modify `Assets/Scripts/Server/GameServerBootstrap.cs`: wire admission, metrics, player registry, and status provider.
- Modify `Assets/Scripts/Server/GameServerStatus.cs`: add metrics fields to `/status` JSON.
- Modify `Assets/Scripts/Server/GameServerHealthServer.cs`: keep `/status` stable while serving extended JSON.
- Modify `Assets/Scenes/Server/ServerBootstrap.unity`: reference the authenticator, registry, player prefab, and observer setup.
- Create `Assets/Prefabs/NetworkTechnicalPlayer.prefab`: minimal networked player prefab.
- Create `Assets/Scripts/Client/MatchConnectionClient.cs`: development client flow that receives assignment and connects to FishNet with payload.
- Create `Assets/Scripts/Load/HeadlessMatchBot.cs`: deterministic bot behavior for load smoke.
- Create `Assets/Scripts/Load/LoadScenarioReport.cs`: serializable load report model.
- Create `Assets/Scripts/Editor/NetworkedCoreLoadRunner.cs`: editor/headless entry point for 1-client and 64-client smoke runs.
- Create or modify Unity edit-mode tests under `Assets/Tests/EditMode/`: parser, config, validator, registry, and status tests.
- Modify `.env.example`, `docker-compose.yml`, and `README.md`: document Phase 04 settings and smoke commands.

---

### Task 1: Admission Payload And Server Config

**Files:**
- Create: `Assets/Scripts/Networking/MatchAdmissionPayload.cs`
- Create: `Assets/Scripts/Networking/MatchAdmissionResult.cs`
- Modify: `Assets/Scripts/Server/GameServerConfig.cs`
- Test: `Assets/Tests/EditMode/MatchAdmissionPayloadTests.cs`
- Test: `Assets/Tests/EditMode/GameServerConfigTests.cs`

**Interfaces:**
- Produces: `MatchAdmissionPayload.TryParse(string json, out MatchAdmissionPayload payload, out string error) : bool`
- Produces: `MatchAdmissionPayload.ToJson() : string`
- Produces: `MatchAdmissionResult.Accepted(Guid matchId, Guid playerId) : MatchAdmissionResult`
- Produces: `MatchAdmissionResult.Rejected(string reason) : MatchAdmissionResult`
- Produces: `GameServerConfig.BackendBaseUrl : string`
- Produces: `GameServerConfig.SharedKey : string`
- Produces: `GameServerConfig.TicketValidationTimeoutSeconds : int`

- [ ] **Step 1: Inspect current worktree**

Run: `git status --short --branch`

Expected: existing documentation edits may be present. Do not revert unrelated files.

- [ ] **Step 2: Add failing payload parser tests**

Create `Assets/Tests/EditMode/MatchAdmissionPayloadTests.cs` with tests for valid payload, malformed JSON, empty ticket, and wrong GUID format.

```csharp
using System;
using LH.Main.Unity.Networking;
using NUnit.Framework;

public sealed class MatchAdmissionPayloadTests
{
    [Test]
    public void TryParseAcceptsValidPayload()
    {
        var matchId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var playerId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        string json = "{\"matchId\":\"" + matchId + "\",\"playerId\":\"" + playerId + "\",\"serverId\":\"game-server-1\",\"ticket\":\"secret-ticket\"}";

        bool parsed = MatchAdmissionPayload.TryParse(json, out MatchAdmissionPayload payload, out string error);

        Assert.That(parsed, Is.True, error);
        Assert.That(payload.MatchId, Is.EqualTo(matchId));
        Assert.That(payload.PlayerId, Is.EqualTo(playerId));
        Assert.That(payload.ServerId, Is.EqualTo("game-server-1"));
        Assert.That(payload.Ticket, Is.EqualTo("secret-ticket"));
    }

    [Test]
    public void TryParseRejectsMalformedJson()
    {
        bool parsed = MatchAdmissionPayload.TryParse("{", out _, out string error);

        Assert.That(parsed, Is.False);
        Assert.That(error, Is.EqualTo("payload_malformed"));
    }

    [Test]
    public void TryParseRejectsEmptyTicket()
    {
        string json = "{\"matchId\":\"11111111-1111-1111-1111-111111111111\",\"playerId\":\"22222222-2222-2222-2222-222222222222\",\"serverId\":\"game-server-1\",\"ticket\":\"\"}";

        bool parsed = MatchAdmissionPayload.TryParse(json, out _, out string error);

        Assert.That(parsed, Is.False);
        Assert.That(error, Is.EqualTo("ticket_required"));
    }
}
```

Run the Unity edit-mode test runner for `MatchAdmissionPayloadTests`.

Expected: fail because `MatchAdmissionPayload` does not exist.

- [ ] **Step 3: Add failing config tests**

Create `Assets/Tests/EditMode/GameServerConfigTests.cs` or extend the existing config tests with required backend settings.

```csharp
using LH.Main.Unity.Server;
using NUnit.Framework;

public sealed class GameServerConfigTests
{
    [Test]
    public void ValidateRequiresBackendAdmissionSettings()
    {
        var config = new GameServerConfig("game-server-1", 8081, 7771, "localhost", 7771, string.Empty, string.Empty, 0);

        bool valid = config.Validate(out string error);

        Assert.That(valid, Is.False);
        Assert.That(error, Is.EqualTo("GAME_SERVER_BACKEND_BASE_URL is required."));
    }
}
```

Run the Unity edit-mode test runner for `GameServerConfigTests`.

Expected: fail because the constructor and properties do not include backend admission settings.

- [ ] **Step 4: Implement payload and result types**

Create `MatchAdmissionPayload.cs` with `Guid MatchId`, `Guid PlayerId`, `string ServerId`, and `string Ticket`. Use `JsonUtility` with a private serializable surrogate because the wire format is JSON. Return stable errors: `payload_required`, `payload_malformed`, `match_id_invalid`, `player_id_invalid`, `server_id_required`, and `ticket_required`.

Create `MatchAdmissionResult.cs` with immutable properties: `bool IsAccepted`, `Guid MatchId`, `Guid PlayerId`, and `string Reason`.

- [ ] **Step 5: Extend game-server config**

Add constants `GAME_SERVER_BACKEND_BASE_URL`, `GAME_SERVER_SHARED_KEY`, and `GAME_SERVER_TICKET_VALIDATION_TIMEOUT_SECONDS`. Extend the constructor, `ReadFromEnvironment`, properties, and `Validate` so backend URL and shared key are required and timeout must be greater than zero.

- [ ] **Step 6: Verify task**

Run Unity edit-mode tests for `MatchAdmissionPayloadTests` and `GameServerConfigTests`.

Run: `git diff --check`

Expected: tests pass and whitespace check is clean.

---

### Task 2: Backend Ticket Validation Adapter

**Files:**
- Create: `Assets/Scripts/Networking/MatchTicketValidator.cs`
- Test: `Assets/Tests/EditMode/MatchTicketValidatorTests.cs`

**Interfaces:**
- Consumes: `MatchAdmissionPayload`
- Consumes: `MatchAdmissionResult`
- Consumes: `GameServerConfig.BackendBaseUrl`, `GameServerConfig.SharedKey`, `GameServerConfig.TicketValidationTimeoutSeconds`
- Produces: `MatchTicketValidator.ValidateAsync(MatchAdmissionPayload payload, CancellationToken cancellationToken) : Task<MatchAdmissionResult>`

- [ ] **Step 1: Add failing HTTP adapter tests**

Create `MatchTicketValidatorTests.cs` with an injectable HTTP sender abstraction so tests can assert request path, JSON body, and `X-Game-Server-Key` without a live backend.

```csharp
[Test]
public async Task ValidateAsyncSendsSharedKeyHeaderAndAcceptsValidResponse()
{
    var payload = new MatchAdmissionPayload(matchId, playerId, "game-server-1", "secret-ticket");
    var sender = new RecordingTicketValidationSender(200, "{\"valid\":true,\"matchId\":\"" + matchId + "\",\"playerId\":\"" + playerId + "\"}");
    var validator = new MatchTicketValidator("http://backend:8080", "shared-key", TimeSpan.FromSeconds(3), sender);

    MatchAdmissionResult result = await validator.ValidateAsync(payload, CancellationToken.None);

    Assert.That(result.IsAccepted, Is.True);
    Assert.That(sender.Path, Is.EqualTo("/internal/v1/matches/tickets/validate"));
    Assert.That(sender.SharedKeyHeader, Is.EqualTo("shared-key"));
    Assert.That(sender.Body, Does.Contain("secret-ticket"));
}
```

Expected: fail because `MatchTicketValidator` does not exist.

- [ ] **Step 2: Add rejection mapping tests**

Test these outcomes with the same fake sender: backend `valid:false` returns `Reason == "ticket_invalid"`; backend `401` returns `Reason == "server_key_rejected"`; timeout returns `Reason == "validation_timeout"`; mismatched match/player in a `valid:true` response returns `Reason == "validation_mismatch"`.

- [ ] **Step 3: Implement validator**

Implement `MatchTicketValidator` using `HttpClient` or a small injected sender wrapper compatible with Unity's runtime profile. The production path must POST to `/internal/v1/matches/tickets/validate`, set `X-Game-Server-Key`, serialize `ticket`, `matchId`, `playerId`, and `serverId`, and apply the configured timeout.

- [ ] **Step 4: Add secret-safe diagnostics**

Ensure exception/log messages include backend URL host, status category, match ID, player ID, and server ID, but never include plaintext ticket or shared key. Add a test that captured log/error strings do not contain the sample ticket or key.

- [ ] **Step 5: Verify task**

Run Unity edit-mode tests for `MatchTicketValidatorTests`.

Run: `git diff --check`

Expected: tests pass and whitespace check is clean.

---

### Task 3: FishNet Admission And Player Registry

**Files:**
- Create: `Assets/Scripts/Networking/GameServerAuthenticator.cs`
- Create: `Assets/Scripts/Networking/ServerPlayerRegistry.cs`
- Modify: `Assets/Scripts/Server/GameServerBootstrap.cs`
- Test: `Assets/Tests/EditMode/ServerPlayerRegistryTests.cs`

**Interfaces:**
- Consumes: `MatchAdmissionPayload.TryParse`
- Consumes: `MatchTicketValidator.ValidateAsync`
- Produces: `GameServerAuthenticator.TryAuthenticateAsync(string payloadJson, CancellationToken cancellationToken) : Task<MatchAdmissionResult>` for testable admission logic
- Produces: `ServerPlayerRegistry.RegisterAcceptedConnection(int connectionId, Guid matchId, Guid playerId) : bool`
- Produces: `ServerPlayerRegistry.RemoveConnection(int connectionId) : bool`
- Produces: `ServerPlayerRegistry.ActivePlayerCount : int`

- [ ] **Step 1: Add failing registry tests**

Create tests proving registry accepts one player per connection, rejects duplicate connection registration, rejects empty player IDs, and removes state on disconnect.

- [ ] **Step 2: Add failing authenticator tests for non-FishNet logic**

Test that wrong local server ID rejects before the validator is called, malformed payload rejects, validator rejection rejects, and validator acceptance returns accepted match/player IDs.

- [ ] **Step 3: Implement registry**

Implement `ServerPlayerRegistry` with dictionaries keyed by FishNet connection ID and player ID. Store match ID, player ID, connection ID, and accepted time. Keep prefab spawn/despawn calls behind methods that can be invoked from FishNet runtime hooks while the registry logic remains edit-mode testable.

- [ ] **Step 4: Implement authenticator wrapper**

Implement `GameServerAuthenticator.TryAuthenticateAsync` as a testable method, then wire it into the installed FishNet authentication or connection event API in the scene runtime. Use the local `GameServerConfig.ServerId` for wrong-server rejection.

- [ ] **Step 5: Wire bootstrap dependencies**

Modify `GameServerBootstrap` to construct or reference metrics, ticket validator, authenticator, and player registry after config validation. Server startup must fail if admission dependencies cannot be initialized.

- [ ] **Step 6: Verify task**

Run Unity edit-mode tests for authenticator and registry.

Run a one-server manual smoke where a malformed payload is rejected.

Run: `git diff --check`

Expected: tests pass, malformed admission does not spawn a player, and whitespace check is clean.

---

### Task 4: Server-Owned Player Spawn And Movement Validation

**Files:**
- Create: `Assets/Scripts/Networking/MovementCommand.cs`
- Create: `Assets/Scripts/Networking/MovementValidator.cs`
- Create: `Assets/Scripts/Networking/NetworkPlayerController.cs`
- Create: `Assets/Prefabs/NetworkTechnicalPlayer.prefab`
- Modify: `Assets/Scenes/Server/ServerBootstrap.unity`
- Test: `Assets/Tests/EditMode/MovementValidatorTests.cs`

**Interfaces:**
- Produces: `MovementCommand(uint sequence, float moveX, float moveY, double sentAtClientTime)`
- Produces: `MovementValidator.Validate(MovementCommand command, MovementState state, float deltaTime) : MovementValidationResult`
- Produces: `MovementValidationResult.Accepted`, `MovementValidationResult.Reason`, `MovementValidationResult.NextState`
- Produces: `NetworkPlayerController.ServerApplyInput(MovementCommand command)`

- [ ] **Step 1: Add failing movement tests**

Create tests for normal input, non-finite axis rejection, out-of-range axis rejection, duplicate sequence rejection, excessive command rate rejection, and speed limit enforcement.

- [ ] **Step 2: Implement movement types**

Implement `MovementCommand`, `MovementState`, and `MovementValidationResult` as Unity-free types. Use defaults from the spec: max speed `6`, max acceleration `24`, max commands per second `30`.

- [ ] **Step 3: Implement movement validator**

Normalize valid input vectors, reject invalid floats, reject duplicate or older sequence values, enforce command-rate spacing, and clamp or reject velocity changes that exceed configured acceleration and speed.

- [ ] **Step 4: Implement network player controller**

Create a FishNet network behaviour that accepts client input intent through FishNet-supported RPC, runs `MovementValidator` on the server, updates authoritative server state, and lets FishNet replicate the resulting transform/state.

- [ ] **Step 5: Create technical prefab and scene references**

Create `NetworkTechnicalPlayer.prefab` with required FishNet network object/component setup and attach `NetworkPlayerController`. Reference the prefab from the server registry or FishNet spawn configuration in `ServerBootstrap.unity`.

- [ ] **Step 6: Verify task**

Run Unity edit-mode tests for `MovementValidatorTests`.

Run one-client manual smoke: valid ticket connects, player spawns, movement input changes server-owned position, disconnect despawns player.

Run: `git diff --check`

Expected: tests pass, one-client smoke passes, and whitespace check is clean.

---

### Task 5: Observer Management And Metrics Status

**Files:**
- Create: `Assets/Scripts/Networking/GameServerMetrics.cs`
- Modify: `Assets/Scripts/Server/GameServerStatus.cs`
- Modify: `Assets/Scripts/Server/GameServerHealthServer.cs`
- Modify: `Assets/Scripts/Server/GameServerBootstrap.cs`
- Modify: `Assets/Scenes/Server/ServerBootstrap.unity`
- Test: `Assets/Tests/EditMode/GameServerStatusTests.cs`

**Interfaces:**
- Produces: `GameServerMetrics.RecordAdmissionAccepted()`
- Produces: `GameServerMetrics.RecordAdmissionRejected(string reason)`
- Produces: `GameServerMetrics.RecordInvalidInput(string reason)`
- Produces: `GameServerMetrics.SetConnectionCounts(int activeConnections, int spawnedPlayers)`
- Produces: extended `GameServerStatus.ToJson()` fields from the spec

- [ ] **Step 1: Add failing status JSON tests**

Test that `ToJson()` includes `activeConnections`, `spawnedPlayers`, `acceptedAdmissions`, `rejectedAdmissions`, `invalidInputCommands`, `disconnects`, `serverTickRate`, `serverTickP95Ms`, `processMemoryMb`, `inboundKbps`, and `outboundKbps`.

- [ ] **Step 2: Implement metrics container**

Implement thread-safe counters for admissions, invalid input, disconnects, active connections, spawned players, and rolling tick samples. Use `GC.GetTotalMemory(false)` or Unity process APIs for memory when available.

- [ ] **Step 3: Extend status JSON**

Modify `GameServerStatus` constructor and `ToJson()` to include metrics. Preserve existing fields for Phase 02/03 health checks.

- [ ] **Step 4: Wire metrics increments**

Increment accepted/rejected admission counters in `GameServerAuthenticator`, player counts in `ServerPlayerRegistry`, invalid input in `NetworkPlayerController`, and disconnects on connection cleanup.

- [ ] **Step 5: Configure FishNet observer management**

Use FishNet's built-in observer management for technical player objects. Prefer the simplest built-in policy that keeps the 64-client scenario stable; document the chosen policy in README and the load report.

- [ ] **Step 6: Verify task**

Run Unity edit-mode tests for status and metrics.

Start a local game-server and request `/status`; verify JSON includes old fields and new metric fields.

Run: `git diff --check`

Expected: tests pass, status endpoint returns extended JSON, and whitespace check is clean.

---

### Task 6: Client Connection And Headless Bot Harness

**Files:**
- Create: `Assets/Scripts/Client/MatchConnectionClient.cs`
- Create: `Assets/Scripts/Load/HeadlessMatchBot.cs`
- Create: `Assets/Scripts/Load/LoadScenarioReport.cs`
- Create: `Assets/Scripts/Editor/NetworkedCoreLoadRunner.cs`
- Modify: `README.md`
- Modify: `.env.example`
- Modify: `docker-compose.yml`

**Interfaces:**
- Consumes: `MatchAdmissionPayload.ToJson()`
- Produces: `MatchConnectionClient.ConnectAsync(MatchAssignment assignment, Guid playerId, CancellationToken cancellationToken) : Task<bool>`
- Produces: `HeadlessMatchBot.RunAsync(BotScenario scenario, CancellationToken cancellationToken) : Task<BotResult>`
- Produces: `LoadScenarioReport.WriteJson(string path)`

- [ ] **Step 1: Add client connection flow**

Implement the development client code that takes backend assignment values, builds `MatchAdmissionPayload`, configures FishNet/Tugboat to connect to `publicHost` and `publicPort`, and supplies the payload through the FishNet authentication path.

- [ ] **Step 2: Add deterministic bot behavior**

Implement `HeadlessMatchBot` with this route: wait for spawn, move forward for 10 seconds, right for 10 seconds, backward for 10 seconds, left for 10 seconds, repeat until scenario duration ends, then disconnect cleanly.

- [ ] **Step 3: Add load report model**

Write JSON fields for started time, duration, target clients, connected clients, spawned clients, completed clients, failed clients, server status snapshots, disconnect reasons, machine notes, and metric collection gaps.

- [ ] **Step 4: Add load runner entry point**

Implement an editor/headless entry point that can run `--clients 1 --durationSeconds 30` for smoke and `--clients 64 --durationSeconds 300` for Phase 04 validation. The runner must fail non-zero if any client cannot connect, spawn, move, or disconnect cleanly.

- [ ] **Step 5: Document environment and commands**

Update `.env.example` and `docker-compose.yml` with backend URL/shared-key variables for game-server containers. Update README with one-client smoke and 64-client load commands, including where the report is written.

- [ ] **Step 6: Verify task**

Run backend tests: `dotnet test server\LH.Main.Server.sln --configuration Release`

Run one-client load smoke for 30 seconds.

Run: `git diff --check`

Expected: backend tests pass, one-client smoke produces a report, and whitespace check is clean.

---

### Task 7: 64-Client Phase Verification

**Files:**
- Modify: `docs/07-development/phase-04-networked-core.md`
- Modify: `README.md`
- Add generated report only if it is small and intentionally reviewed; otherwise keep report as an untracked local artifact and summarize its path.

**Interfaces:**
- Consumes: load runner from Task 6
- Produces: documented Phase 04 verification result and metric summary

- [ ] **Step 1: Run full automated backend verification**

Run: `dotnet test server\LH.Main.Server.sln --configuration Release`

Expected: all backend tests pass.

- [ ] **Step 2: Build Unity dedicated server**

Run the existing Unity game-server build path that produces `Builds/**/LH.Main.GameServer.x86_64`.

Expected: Linux dedicated server artifact exists.

- [ ] **Step 3: Start local services**

Start PostgreSQL, backend, and one game-server through the documented local Compose/profile flow using `.env.example` values adjusted for local secrets.

Expected: backend health is OK and game-server `/health/ready` returns `200`.

- [ ] **Step 4: Run replay and wrong-server negative smokes**

Use one valid assignment ticket once and verify admission succeeds. Reuse the same ticket and verify admission fails. Change `serverId` in the payload and verify admission fails before player spawn.

Expected: only the first admission spawns a player.

- [ ] **Step 5: Run 64-client load scenario**

Run the load runner with `64` clients and `300` seconds duration.

Expected: 64 clients connect, spawn, follow deterministic movement, disconnect cleanly, and no server error is recorded.

- [ ] **Step 6: Capture results in docs**

Update `docs/07-development/phase-04-networked-core.md` with the report path, pass/fail summary, hardware/context notes, and metric highlights. Do not commit large generated logs or secret-bearing files.

- [ ] **Step 7: Final checks**

Run: `git diff --check`

Run: `git status --short --branch`

Expected: whitespace check is clean and only intentional files are modified.
