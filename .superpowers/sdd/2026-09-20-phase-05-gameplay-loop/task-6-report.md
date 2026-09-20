### Task 6 Report: Unity Runtime Wiring And Status Metrics

## What I implemented

- Added Phase 05 gameplay loop metrics to `GameServerMetrics` snapshots:
  - `acceptedDamageEvents`
  - `rejectedDamageEvents`
  - `extractedPlayers`
  - `deadPlayers`
  - `submittedMatchResults`
  - `duplicateMatchResults`
  - `failedMatchResults`
- Added matching `GameServerStatus` fields and JSON output while preserving existing Phase 04 status fields.
- Added a status constructor compatible with the Task 6 brief's string state test input.
- Extended `ServerPlayerRegistry.SnapshotResults` to preserve terminal extracted/dead outcomes and report terminal player counts to metrics.
- Added `ServerPlayerRegistry.TryGetMatchId` for controlled runtime finalization.
- Wired `GameServerBootstrap` to initialize `MatchResultSubmitter` from the existing backend URL/shared key config.
- Added explicit `FinalizeMatchForLoadRunner` path that builds a result payload from the registry snapshot, submits it, and records submitted/duplicate/failed result metrics.
- Updated `NetworkPlayerController.ApplyServerLifeState` to record extracted/dead player metrics when server-side life state transitions are applied.
- Added the `LH.Main.Unity.Gameplay` assembly reference to the server assembly so runtime finalization can use match result models/submitter.

## What I tested and test results

- Unity EditMode tests with `task-6-green.xml`:
  - Result: Passed
  - Total: 73
  - Passed: 73
  - Failed: 0
- Whitespace check:
  - Command: `git diff --check -- Assets/Scripts/Networking Assets/Scripts/Server Assets/Tests/EditMode`
  - Result: no whitespace errors
  - Note: Git reported LF-to-CRLF working-copy warnings only.

## TDD Evidence

### RED command/output

Command:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.14f1\Editor\Unity.exe" -batchmode -quit -projectPath "D:\LH_MAIN-phase-02" -runTests -testPlatform EditMode -testResults "D:\LH_MAIN-phase-02\task-6-red.xml" -logFile "D:\LH_MAIN-phase-02\Unity-Task6-Red.log"
```

Output:

```text
Aborting batchmode due to failure:
Scripts have compiler errors.
```

Expected failure evidence from `Unity-Task6-Red.log`:

```text
Assets\Tests\EditMode\GameServerStatusTests.cs(54,60): error CS1503: Argument 2: cannot convert from 'string' to 'LH.Main.Unity.Server.GameServerState'
Assets\Tests\EditMode\GameServerStatusTests.cs(56,13): error CS0117: 'GameServerStatus' does not contain a definition for 'AcceptedDamageEvents'
Assets\Tests\EditMode\GameServerStatusTests.cs(57,13): error CS0117: 'GameServerStatus' does not contain a definition for 'RejectedDamageEvents'
Assets\Tests\EditMode\GameServerStatusTests.cs(58,13): error CS0117: 'GameServerStatus' does not contain a definition for 'ExtractedPlayers'
Assets\Tests\EditMode\GameServerStatusTests.cs(59,13): error CS0117: 'GameServerStatus' does not contain a definition for 'DeadPlayers'
Assets\Tests\EditMode\GameServerStatusTests.cs(60,13): error CS0117: 'GameServerStatus' does not contain a definition for 'SubmittedMatchResults'
Assets\Tests\EditMode\GameServerStatusTests.cs(61,13): error CS0117: 'GameServerStatus' does not contain a definition for 'DuplicateMatchResults'
Assets\Tests\EditMode\GameServerStatusTests.cs(62,13): error CS0117: 'GameServerStatus' does not contain a definition for 'FailedMatchResults'
Scripts have compiler errors.
```

### GREEN command/output

Initial `-quit` GREEN command returned exit code 0 but did not write `task-6-green.xml`, so I reran the same test args without `-quit` per controller ruling.

Command:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.14f1\Editor\Unity.exe" -batchmode -projectPath "D:\LH_MAIN-phase-02" -runTests -testPlatform EditMode -testResults "D:\LH_MAIN-phase-02\task-6-green.xml" -logFile "D:\LH_MAIN-phase-02\Unity-Task6-Green-NoQuit.log"
```

Output evidence from `Unity-Task6-Green-NoQuit.log`:

```text
Test run completed. Exiting with code 0 (Ok). Run completed.
```

XML evidence from `task-6-green.xml`:

```xml
<test-run id="2" testcasecount="73" result="Passed" total="73" passed="73" failed="0" inconclusive="0" skipped="0" asserts="0">
```

## Files changed

- `Assets/Scripts/Networking/GameServerMetrics.cs`
- `Assets/Scripts/Networking/NetworkPlayerController.cs`
- `Assets/Scripts/Networking/ServerPlayerRegistry.cs`
- `Assets/Scripts/Server/GameServerBootstrap.cs`
- `Assets/Scripts/Server/GameServerStatus.cs`
- `Assets/Scripts/Server/LH.Main.Unity.Server.asmdef`
- `Assets/Tests/EditMode/GameServerStatusTests.cs`
- `Assets/Tests/EditMode/ServerPlayerRegistryTests.cs`
- `.superpowers/sdd/2026-09-20-phase-05-gameplay-loop/task-6-report.md`

## Self-review findings

- No load-runner/report changes were added.
- No backend changes were made.
- Existing Phase 04 status JSON fields remain present with the same names.
- The load-runner finalization path is explicit/manual and does not add production timers.
- Dirty worktree noise outside Task 6 files was not modified or staged.

## Issues or concerns

- `NetworkPlayerController` only records metrics for life state transitions routed through `ApplyServerLifeState`; direct `PlayerStateMachine` mutations are summarized through registry snapshots instead.
- The initial GREEN command with `-quit` exited successfully before producing XML, so GREEN evidence uses the no-`-quit` rerun as instructed.

## Fix Round 1

### What changed

- Expanded `ToJsonIncludesGameplayLoopMetrics` to assert all seven required camelCase fields:
  - `acceptedDamageEvents`
  - `rejectedDamageEvents`
  - `extractedPlayers`
  - `deadPlayers`
  - `submittedMatchResults`
  - `duplicateMatchResults`
  - `failedMatchResults`
- Added `ServerPlayerRegistry.ApplyTechnicalDamage(TechnicalDamageEvent)` as a small server-owned technical damage entry point.
- Wired accepted damage results to `GameServerMetrics.RecordDamageAccepted()`.
- Wired rejected damage results, including missing targets, to `GameServerMetrics.RecordDamageRejected(...)`.
- Wrapped `GameServerBootstrap.FinalizeMatchForLoadRunnerAsync` in local `try/catch`, records `failedMatchResults`, and logs only the exception type.

### RED command/output

Command:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.14f1\Editor\Unity.exe" -batchmode -quit -projectPath "D:\LH_MAIN-phase-02" -runTests -testPlatform EditMode -testResults "D:\LH_MAIN-phase-02\task-6-fix1-red.xml" -logFile "D:\LH_MAIN-phase-02\Unity-Task6-Fix1-Red.log"
```

Output:

```text
Aborting batchmode due to failure:
Scripts have compiler errors.
```

Expected failure evidence from `Unity-Task6-Fix1-Red.log`:

```text
Assets\Tests\EditMode\ServerPlayerRegistryTests.cs(132,47): error CS1061: 'ServerPlayerRegistry' does not contain a definition for 'ApplyTechnicalDamage' and no accessible extension method 'ApplyTechnicalDamage' accepting a first argument of type 'ServerPlayerRegistry' could be found (are you missing a using directive or an assembly reference?)
Assets\Tests\EditMode\ServerPlayerRegistryTests.cs(134,47): error CS1061: 'ServerPlayerRegistry' does not contain a definition for 'ApplyTechnicalDamage' and no accessible extension method 'ApplyTechnicalDamage' accepting a first argument of type 'ServerPlayerRegistry' could be found (are you missing a using directive or an assembly reference?)
Scripts have compiler errors.
```

### GREEN command/output

The first GREEN command with `-quit` returned without producing `task-6-fix1-green.xml`, so I reran the same test args without `-quit` per controller ruling.

Command:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.14f1\Editor\Unity.exe" -batchmode -projectPath "D:\LH_MAIN-phase-02" -runTests -testPlatform EditMode -testResults "D:\LH_MAIN-phase-02\task-6-fix1-green.xml" -logFile "D:\LH_MAIN-phase-02\Unity-Task6-Fix1-Green-NoQuit.log"
```

Output evidence from `Unity-Task6-Fix1-Green-NoQuit.log`:

```text
Test run completed. Exiting with code 0 (Ok). Run completed.
```

XML evidence from `task-6-fix1-green.xml`:

```xml
<test-run id="2" testcasecount="74" result="Passed" total="74" passed="74" failed="0" inconclusive="0" skipped="0" asserts="0">
```

### Diff check

Command:

```powershell
git diff --check -- Assets/Scripts/Networking Assets/Scripts/Server Assets/Tests/EditMode
```

Output:

```text
warning: in the working copy of 'Assets/Scripts/Networking/ServerPlayerRegistry.cs', LF will be replaced by CRLF the next time Git touches it
warning: in the working copy of 'Assets/Scripts/Server/GameServerBootstrap.cs', LF will be replaced by CRLF the next time Git touches it
warning: in the working copy of 'Assets/Tests/EditMode/GameServerStatusTests.cs', LF will be replaced by CRLF the next time Git touches it
warning: in the working copy of 'Assets/Tests/EditMode/ServerPlayerRegistryTests.cs', LF will be replaced by CRLF the next time Git touches it
```

Result: no whitespace errors; line-ending warnings only.

### Fix round 1 concerns

- The technical damage entry point remains intentionally narrow and server-owned; no weapons, ballistics, or hit detection were added.
- The no-`-quit` Unity rerun was required again to produce XML evidence.

## Fix Round 2

### What changed

- Added `GameServerBootstrap.ApplyTechnicalDamageForLoadRunner(Guid targetPlayerId, int amount, string kind)` as the controlled server-owned runtime call-site for Task 6 damage metrics.
- The bootstrap hook constructs a `TechnicalDamageEvent` and routes it through `ServerPlayerRegistry.ApplyTechnicalDamage(...)`; metric recording remains in the registry path.
- Added `ApplyTechnicalDamageForLoadRunnerRecordsMetricsThroughRegistry` to prove the bootstrap call-site increments accepted and rejected damage metrics through the real registry path.
- Preserved the previous status JSON test assertions for all seven exact camelCase gameplay-loop fields.

### RED command/output

Command:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.14f1\Editor\Unity.exe" -batchmode -quit -projectPath "D:\LH_MAIN-phase-02" -runTests -testPlatform EditMode -testResults "D:\LH_MAIN-phase-02\task-6-fix2-red.xml" -logFile "D:\LH_MAIN-phase-02\Unity-Task6-Fix2-Red.log"
```

Output:

```text
Aborting batchmode due to failure:
Scripts have compiler errors.
```

Expected failure evidence from `Unity-Task6-Fix2-Red.log`:

```text
Assets\Tests\EditMode\ServerPlayerRegistryTests.cs(254,23): error CS1061: 'GameServerBootstrap' does not contain a definition for 'ApplyTechnicalDamageForLoadRunner' and no accessible extension method 'ApplyTechnicalDamageForLoadRunner' accepting a first argument of type 'GameServerBootstrap' could be found (are you missing a using directive or an assembly reference?)
Assets\Tests\EditMode\ServerPlayerRegistryTests.cs(255,23): error CS1061: 'GameServerBootstrap' does not contain a definition for 'ApplyTechnicalDamageForLoadRunner' and no accessible extension method 'ApplyTechnicalDamageForLoadRunner' accepting a first argument of type 'GameServerBootstrap' could be found (are you missing a using directive or an assembly reference?)
Scripts have compiler errors.
```

### GREEN command/output

The first GREEN command with `-quit` returned without producing `task-6-fix2-green.xml`, so I reran the same test args without `-quit` per controller ruling.

Command:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.14f1\Editor\Unity.exe" -batchmode -projectPath "D:\LH_MAIN-phase-02" -runTests -testPlatform EditMode -testResults "D:\LH_MAIN-phase-02\task-6-fix2-green.xml" -logFile "D:\LH_MAIN-phase-02\Unity-Task6-Fix2-Green-NoQuit.log"
```

Output evidence from `Unity-Task6-Fix2-Green-NoQuit.log`:

```text
Test run completed. Exiting with code 0 (Ok). Run completed.
```

XML evidence from `task-6-fix2-green.xml`:

```xml
<test-run id="2" testcasecount="75" result="Passed" total="75" passed="75" failed="0" inconclusive="0" skipped="0" asserts="0">
```

### Diff check

Command:

```powershell
git diff --check -- Assets/Scripts/Networking Assets/Scripts/Server Assets/Tests/EditMode
```

Output:

```text
warning: in the working copy of 'Assets/Scripts/Server/GameServerBootstrap.cs', LF will be replaced by CRLF the next time Git touches it
warning: in the working copy of 'Assets/Tests/EditMode/ServerPlayerRegistryTests.cs', LF will be replaced by CRLF the next time Git touches it
```

Result: no whitespace errors; line-ending warnings only.

### Fix round 2 concerns

- No final weapons, ballistics, hit detection, client authority, production timers, backend changes, or load-runner/report docs changes were added.
- The no-`-quit` Unity rerun was required again to produce XML evidence.
