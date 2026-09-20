# Task 9 Report

status: DONE_WITH_CONCERNS

summary of changes:
- Added deterministic Phase 06 bot action windows and `BotPhase06Action`.
- Added Phase 06 bot result flags and Phase 06 intent invocation from `HeadlessMatchBot.RunAsync` while preserving movement.
- Added Phase 06 counters to `LoadScenarioReport.ToJson`.
- Added `--phase06CoreMatch true`/`-lhPhase06CoreMatch true` parsing and default report path `.superpowers/sdd/2026-09-21-phase-06-core-match/task-9-final-load-64.json`.
- Added Phase 06 load-runner completion/success gating for completed clients, duplicate-loot prevention evidence, non-zero pickup/fire/grenade/med/zone coverage, and no duplicate-loot success.

RED verification:
- Command: `& "C:\Program Files\Unity\Hub\Editor\6000.3.14f1\Editor\Unity.exe" -batchmode -quit -projectPath "D:\LH_MAIN\.worktrees\phase-06-core-match" -runTests -testPlatform EditMode -testResults "D:\LH_MAIN\.worktrees\phase-06-core-match\.superpowers\sdd\2026-09-21-phase-06-core-match\task-9-editmode.xml"`
- Result: expected RED compile failure before production code. Unity did not produce XML with `-quit`.
- Rerun without `-quit`: `& "C:\Program Files\Unity\Hub\Editor\6000.3.14f1\Editor\Unity.exe" -batchmode -projectPath "D:\LH_MAIN\.worktrees\phase-06-core-match" -runTests -testPlatform EditMode -testResults "D:\LH_MAIN\.worktrees\phase-06-core-match\.superpowers\sdd\2026-09-21-phase-06-core-match\task-9-editmode.xml"`
- Observed failure evidence from `C:\Users\Maany\AppData\Local\Unity\Editor\Editor.log`: missing `HeadlessMatchBot.GetPhase06ActionForElapsedSeconds`, missing `BotPhase06Action`, and missing `LoadScenarioReport` Phase 06 fields including `LootPickups`, `DuplicateLootPrevented`, `FireRequests`, `GrenadesExploded`, `ZoneDamageTicks`, `MedItemsUsed`, and `DuplicateLootSucceeded`.

final verification:
- Command: `& "C:\Program Files\Unity\Hub\Editor\6000.3.14f1\Editor\Unity.exe" -batchmode -quit -projectPath "D:\LH_MAIN\.worktrees\phase-06-core-match" -runTests -testPlatform EditMode -testResults "D:\LH_MAIN\.worktrees\phase-06-core-match\.superpowers\sdd\2026-09-21-phase-06-core-match\task-9-editmode.xml"`
- Result: Unity exited without writing XML.
- Rerun without `-quit`: `& "C:\Program Files\Unity\Hub\Editor\6000.3.14f1\Editor\Unity.exe" -batchmode -projectPath "D:\LH_MAIN\.worktrees\phase-06-core-match" -runTests -testPlatform EditMode -testResults "D:\LH_MAIN\.worktrees\phase-06-core-match\.superpowers\sdd\2026-09-21-phase-06-core-match\task-9-editmode.xml"`
- Result path: `.superpowers/sdd/2026-09-21-phase-06-core-match/task-9-editmode.xml`
- Result: Passed, total 139, passed 139, failed 0, skipped 0.
- Command: `dotnet test "server\LH.Main.Server.sln" --configuration Release`
- Result: Passed, total 67, passed 67, failed 0, skipped 0.
- Command: `git diff --check`
- Result: no whitespace errors; Git emitted line-ending warnings for existing/modified Unity files.

64-client load scenario:
- Compose down command: `docker compose --env-file .env.example --profile game-servers down --volumes`
- Compose up command: `docker compose --env-file .env.example --profile game-servers up --build --detach --wait backend-api game-server-1 game-server-2`
- Initial compose blocker: missing `Builds/GameServer/LinuxHeadless` artifact required by `docker/game-server/Dockerfile`.
- Build command attempted and completed: `& "C:\Program Files\Unity\Hub\Editor\6000.3.14f1\Editor\Unity.exe" -batchmode -quit -projectPath "D:\LH_MAIN\.worktrees\phase-06-core-match" -executeMethod LH.Main.Unity.Editor.GameServerBuild.BuildLinuxHeadless`
- Retry compose blocker: `127.0.0.1:8080` was allocated by older `lh_main-phase-02-*` containers.
- Stopped conflicting containers: `docker stop lh_main-phase-02-game-server-1-1 lh_main-phase-02-game-server-2-1 lh_main-phase-02-backend-api-1 lh_main-phase-02-postgres-1`
- Final compose blocker: `phase-06-core-match-backend-api-1` exited with code 139 during `--wait`; logs show `Npgsql.NpgsqlException: Resource temporarily unavailable` with `SocketException (00000001, 11)` resolving/connecting to `postgres:5432` during EF migration.
- Required load command was not run because backend-api never became healthy: `& "C:\Program Files\Unity\Hub\Editor\6000.3.14f1\Editor\Unity.exe" -batchmode -projectPath . -executeMethod LH.Main.Unity.Editor.NetworkedCoreLoadRunner.Run -phase06CoreMatch true -clients 64 -durationSeconds 60 -reportPath .superpowers/sdd/2026-09-21-phase-06-core-match/task-9-final-load-64.json`
- Load report path: `.superpowers/sdd/2026-09-21-phase-06-core-match/task-9-final-load-64.json`
- Load report result: not produced due compose backend startup blocker.

commit hashes:
- 2c50d2b feat: add phase 06 load scenario

self-review notes/concerns:
- Phase 06 bot flags are set after local server-intent method invocation returns without exception, matching the accepted caveat in the brief where replicated accepted state is not directly exposed to the bot.
- The 64-client scenario could not be executed because Docker backend startup failed before the load runner could connect.
- Unrelated Unity/SDD worktree noise was left unstaged.

## Fix Round 1

review findings addressed:
- Replaced `Guid.Empty` pickup attempts with `HeadlessMatchBot.Phase06LootId`, matching `CoreMatchRuntime.InitialLootId`, so bots target server-seeded loot.
- Stopped counting Phase 06 pickup/fire/reload/grenade/med/zone coverage from bot intent flags when `phase06CoreMatch` is enabled; Phase 06 action coverage is now gated by authoritative server counters.
- Added authoritative Phase 06 coverage aggregation from game-server `/status` JSON via `acceptedPickupAttempts`, `duplicateLootPickups`, `acceptedFireRequests`, `grenadesThrown`, `grenadesExploded`, `zoneDamageTicks`, and `medItemsUsed`.
- Populated `DuplicateLootSucceeded` from server status when a single game-server reports more than one accepted pickup against its seeded loot.

RED verification:
- Command: `& "C:\Program Files\Unity\Hub\Editor\6000.3.14f1\Editor\Unity.exe" -batchmode -projectPath . -runTests -testPlatform EditMode -testResults ".superpowers/sdd/2026-09-21-phase-06-core-match/task-9-editmode-red-fix1.xml" -testFilter "HeadlessMatchBotTests;NetworkedCoreLoadRunnerTests" -quit`
- Result: expected compiler failure before production changes for missing `HeadlessMatchBot.Phase06LootId`, missing `CoreMatchRuntime.InitialLootId`, missing `ApplyBotResultToReport`, and missing `ApplyPhase06ServerMetricsFromStatusJson`.

fix verification:
- Targeted command: `& "C:\Program Files\Unity\Hub\Editor\6000.3.14f1\Editor\Unity.exe" -batchmode -projectPath "D:\LH_MAIN\.worktrees\phase-06-core-match" -runTests -testPlatform editmode -testResults "D:\LH_MAIN\.worktrees\phase-06-core-match\.superpowers\sdd\2026-09-21-phase-06-core-match\task-9-editmode-fix1-targeted.xml" -testFilter "HeadlessMatchBotTests"`
- Targeted result: 4 total, 4 passed, 0 failed.
- Targeted command: `& "C:\Program Files\Unity\Hub\Editor\6000.3.14f1\Editor\Unity.exe" -batchmode -projectPath "D:\LH_MAIN\.worktrees\phase-06-core-match" -runTests -testPlatform editmode -testResults "D:\LH_MAIN\.worktrees\phase-06-core-match\.superpowers\sdd\2026-09-21-phase-06-core-match\task-9-editmode-fix1-runner-targeted.xml" -testFilter "NetworkedCoreLoadRunnerTests"`
- Targeted result: 15 total, 15 passed, 0 failed.
- Full command: `& "C:\Program Files\Unity\Hub\Editor\6000.3.14f1\Editor\Unity.exe" -batchmode -projectPath "D:\LH_MAIN\.worktrees\phase-06-core-match" -runTests -testPlatform editmode -testResults "D:\LH_MAIN\.worktrees\phase-06-core-match\.superpowers\sdd\2026-09-21-phase-06-core-match\task-9-editmode.xml"`
- Full result: 142 total, 142 passed, 0 failed, 0 skipped.
- Backend command: `dotnet test "server\LH.Main.Server.sln" --configuration Release`
- Backend result: 67 total, 67 passed, 0 failed, 0 skipped.
- Whitespace command: `git diff --check`
- Whitespace result: no whitespace errors; line-ending warnings only.

64-client load retry:
- Build command: `& "C:\Program Files\Unity\Hub\Editor\6000.3.14f1\Editor\Unity.exe" -batchmode -projectPath "D:\LH_MAIN\.worktrees\phase-06-core-match" -executeMethod LH.Main.Unity.Editor.GameServerBuild.BuildLinuxHeadless -quit`
- Build result: `Build Finished, Result: Success.`
- Compose command: `docker compose --env-file .env.example --profile game-servers down --volumes; docker compose --env-file .env.example --profile game-servers up --build --detach --wait backend-api game-server-1 game-server-2`
- Compose result: backend and postgres became healthy, but `phase-06-core-match-game-server-2-1` exited with code 127.
- Exact blocker: game-server log shows `/usr/bin/env: 'sh\r': No such file or directory` and `/usr/bin/env: use -[v]S to pass options in shebang lines`.
- Required load command was not run because game servers did not become healthy.
