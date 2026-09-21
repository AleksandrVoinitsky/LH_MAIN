# Project State - 2026-09-21

## Summary

LH Main is a Unity/FishNet multiplayer extraction-match project with a .NET backend. The current branch `phase-06-core-match` contains the production-oriented Phase 06 core match runtime: server-authoritative inventory, loot, weapon fire, grenades, med use, shrinking zone damage, Dockerized headless game servers, backend admission/status APIs, and a 64-client load scenario.

The active development worktree is:

```text
D:\LH_MAIN\.worktrees\phase-06-core-match
```

The main project root remains:

```text
D:\LH_MAIN
```

## Architecture

The project is split into three major runtime areas:

- Unity client/game-server code under `Assets/`.
- .NET backend services under `server/`.
- Docker orchestration under `docker/` and `docker-compose.yml`.

The multiplayer runtime uses FishNet networking with server authority. Clients send intents, while the server owns authoritative state transitions for inventory, loot pickup, damage, healing, grenades, zone damage, match outcomes, and game-server metrics.

## Unity Runtime

The Unity side includes gameplay systems for:

- player runtime state and match lifecycle
- server-owned inventory slots
- item definitions and item stacks
- server-authored loot pickup transactions
- baseline hitscan/raycast weapon fire
- ammo and reload validation
- server-simulated grenade throws and explosions
- instant med use with validation and cooldown behavior
- shrinking-zone phase/runtime damage
- network player controller RPC intent handling
- server player registry integration
- game-server bootstrap and status metrics

Important Unity files include:

- `Assets/Scripts/Gameplay/CoreMatchRuntime.cs`
- `Assets/Scripts/Gameplay/Items/PlayerInventory.cs`
- `Assets/Scripts/Networking/NetworkPlayerController.cs`
- `Assets/Scripts/Networking/ServerPlayerRegistry.cs`
- `Assets/Scripts/Networking/GameServerMetrics.cs`
- `Assets/Scripts/Server/GameServerBootstrap.cs`
- `Assets/Scripts/Editor/NetworkedCoreLoadRunner.cs`
- `Assets/Scripts/Load/HeadlessMatchBot.cs`

## Backend Runtime

The backend contains the API and contracts used by game servers and load scenarios. It supports game-server registration/status, admission flow, match finalization paths, and supporting tests.

Important backend areas include:

- `server/src/LH.Main.Contracts`
- `server/src/LH.Main.Backend.Api`
- `server/tests/LH.Main.Contracts.Tests`
- `server/tests/LH.Main.Backend.Api.Tests`

## Docker And Headless Servers

Game servers run as Linux headless Unity builds inside Docker containers. The Docker entrypoint is:

- `docker/game-server/entrypoint.sh`

Shell scripts are pinned to LF line endings through:

- `.gitattributes`

This is required because Linux containers fail to execute CRLF shell scripts.

The latest verified path was:

1. Build Unity Linux headless game server.
2. Rebuild Docker images from `Builds/GameServer/LinuxHeadless/`.
3. Start backend and two game-server containers through Docker Compose.
4. Run the Unity load runner against the local backend/game servers.

## Implemented Phase 06 Behavior

Phase 06 is implemented as a runtime-only, server-authoritative core match slice.

Current behavior includes:

- clients connect through backend admission
- server registers authoritative player state
- each player receives initial med and grenade items
- loot pickup is transaction-based and duplicate-safe
- duplicate loot attempts are rejected and counted
- fire requests use authoritative server-side player position
- grenade throws use authoritative server-side player position
- grenade explosions are simulated and counted by server tick
- zone damage is applied by server tick
- missing scene zone volume is treated as hazardous for headless/default runtime coverage
- med use is validated by authoritative inventory and health state
- status endpoints expose authoritative Phase 06 metrics
- load-runner success gates check real game-server counters

## Load Scenario

The Phase 06 load scenario is run through:

```text
LH.Main.Unity.Editor.NetworkedCoreLoadRunner.Run
```

The planned command form is supported with single-dash options:

```text
-phase06CoreMatch true -clients 64 -durationSeconds 60 -reportPath <path>
```

The latest verified 64-client report is:

```text
.superpowers/sdd/2026-09-21-phase-06-core-match/task-9-final-load-64.json
```

Key results from that run:

- `targetClients: 64`
- `connectedClients: 64`
- `spawnedClients: 64`
- `completedClients: 64`
- `failedClients: 0`
- `duplicateLootSucceeded: false`
- `grenadesExploded: 1`
- `zoneDamageTicks: 1662`
- `medItemsUsed: 1`

## Verification Snapshot

Latest verification evidence:

- Unity EditMode tests: `149/149` passed, `0` failed.
- Backend .NET tests: contracts `1/1` passed, backend API `66/66` passed.
- `git diff --check`: no whitespace errors; only CRLF warnings on Unity/generated files.
- Docker backend and two game-server containers reached healthy state after rebuild.
- 64-client Phase 06 load scenario completed successfully with zero failed clients.

Unity XML result:

```text
.superpowers/sdd/2026-09-21-phase-06-core-match/final-editmode.xml
```

## Current Branch State

Current development branch:

```text
phase-06-core-match
```

The branch contains Phase 06 implementation commits and final hardening commits. The latest local changes before this document include:

- load-runner CLI parsing support for the exact planned single-dash command
- regression test for that command-line parsing
- LF enforcement for shell scripts
- LF-normalized Docker game-server entrypoint
- load/test evidence files under `.superpowers/sdd/2026-09-21-phase-06-core-match/`

Known working tree noise from Unity/editor generation may include ProjectSettings, package lock/manifest, settings assets, FishNet metadata deletes, and generated `.meta` files. These should not be blindly reverted without deciding whether they belong to the Unity project state.

## Practical Next Session Notes

If development resumes, start from the `phase-06-core-match` branch and inspect the working tree before editing. The project is currently at a point where Phase 06 runtime behavior has been implemented and verified locally, including the 64-client load scenario.

Useful commands from the active worktree:

```powershell
dotnet test "server\LH.Main.Server.sln" --configuration Release
```

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.14f1\Editor\Unity.exe" -batchmode -projectPath . -runTests -testPlatform EditMode -testResults .superpowers/sdd/2026-09-21-phase-06-core-match/final-editmode.xml
```

```powershell
docker compose --env-file .env.example --profile game-servers up --build --detach --wait backend-api game-server-1 game-server-2
```

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.14f1\Editor\Unity.exe" -batchmode -projectPath . -executeMethod LH.Main.Unity.Editor.NetworkedCoreLoadRunner.Run -phase06CoreMatch true -clients 64 -durationSeconds 60 -reportPath .superpowers/sdd/2026-09-21-phase-06-core-match/task-9-final-load-64.json
```
