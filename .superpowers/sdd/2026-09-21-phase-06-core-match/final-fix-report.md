# Phase 06 Final Fix Report

## Status

Fixed final whole-branch review blockers for Phase 06 Core Match Runtime.

## Summary

- Fire and grenade RPC paths now derive action position from server-owned controller/runtime state instead of trusting client-supplied origins.
- `CoreMatchRuntime` can register players with the `ServerPlayerRegistry` owned `PlayerStateMachine`, so runtime damage/death is reflected in registry snapshots and terminal counts.
- Default runtime behavior treats missing safe-zone volume as hazardous for technical/headless paths, enabling zone damage and med-use coverage without manually placed scene content.
- Reload rejections increment rejected fire metrics, and grenade throw rejections increment existing gameplay rejection metrics.

## Tests Added

- `CoreMatchRuntimeTests.TryFireUsesAuthoritativePlayerPositionInsteadOfClientOrigin`
- `CoreMatchRuntimeTests.TryThrowGrenadeUsesAuthoritativePlayerPositionInsteadOfClientOrigin`
- `CoreMatchRuntimeTests.DefaultRuntimeZoneDamagesPlayersAndEnablesMedCoverageWithoutSceneVolume`
- `ServerPlayerRegistryTests.RuntimeDamageIsReflectedInRegistryResultSnapshots`
- `NetworkPlayerControllerTests.FireHelperUsesServerTransformInsteadOfClientOrigin`
- `NetworkPlayerControllerTests.ReloadAndGrenadeRejectionsAreRecordedInMetrics`

## RED Evidence

- Command: `& "C:\Program Files\Unity\Hub\Editor\6000.3.14f1\Editor\Unity.exe" -batchmode -projectPath "D:\LH_MAIN\.worktrees\phase-06-core-match" -runTests -testPlatform EditMode -testResults "D:\LH_MAIN\.worktrees\phase-06-core-match\.superpowers\sdd\2026-09-21-phase-06-core-match\final-fix-editmode-red.xml" -quit`
- Result: failed before production changes with expected compile errors for missing `CoreMatchRuntime.UpdatePlayerPosition`, `NetworkPlayerController.ApplyServerReloadForTest`, and `NetworkPlayerController.ApplyServerThrowGrenadeForTest` APIs. Evidence in `C:\Users\Maany\AppData\Local\Unity\Editor\Editor.log` lines 744-783 from that run.

## Verification Commands And Results

- Command: `& "C:\Program Files\Unity\Hub\Editor\6000.3.14f1\Editor\Unity.exe" -batchmode -projectPath "D:\LH_MAIN\.worktrees\phase-06-core-match" -runTests -testPlatform EditMode -testResults "D:\LH_MAIN\.worktrees\phase-06-core-match\.superpowers\sdd\2026-09-21-phase-06-core-match\final-fix-editmode.xml"`
- Result: passed, `148/148`, result XML at `.superpowers/sdd/2026-09-21-phase-06-core-match/final-fix-editmode.xml`.
- Command: `dotnet test "server\LH.Main.Server.sln" --configuration Release`
- Result: passed, `LH.Main.Contracts.Tests` `1/1`, `LH.Main.Backend.Api.Tests` `66/66`.
- Command: `git diff --check`
- Result: no whitespace errors. Git emitted line-ending warnings for existing dirty/generated files and touched C# files.

## Load Scenario Result Or Blocker

- Command: `docker compose --env-file .env.example --profile game-servers down --volumes`
- Result: completed.
- Command: `docker compose --env-file .env.example --profile game-servers up --build --detach --wait backend-api game-server-1 game-server-2`
- Result: blocked before Unity load-runner execution because `game-server-2` exited with code `127`.
- Exact blocker from `docker compose --env-file .env.example --profile game-servers logs --no-color game-server-2`: `/usr/bin/env: 'sh\r': No such file or directory` and `/usr/bin/env: use -[v]S to pass options in shebang lines`.
- The 64-client Unity command was not run because required compose services did not become healthy.

## Commit Hashes

- Final response contains the commit hash for the commit that includes this report artifact.

## Concerns

- The committed Docker game-server entrypoint appears to have CRLF line endings in the container context, blocking the 64-client load retry independently of the Phase 06 runtime fixes.
- Existing unrelated dirty/untracked Unity/SDD files were left untouched except for the requested final result/report artifacts.
