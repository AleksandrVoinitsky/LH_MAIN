# Task 5 Report: Unity Extraction, Result Models, And Submitter

## What I Implemented

- Added `ExtractionProgress` as a Unity-free continuous extraction hold timer that resets when outside the zone.
- Added Unity-side match result domain models:
  - `PlayerResultSnapshot`
  - `MatchParticipantResult`
  - `MatchResultPayload`
  - `MatchResultSubmissionOutcome`
  - `MatchResultSubmissionResponse`
- Added `MatchResultBuilder.Build(...)` to produce participant outcomes and `phase05_test_reward` for extracted players.
- Added `MatchResultPayload.ToJson()` with backend-facing camelCase JSON fields.
- Added `MatchResultSubmitter.SubmitAsync(...)` with injectable sender abstraction, timeout handling, response mapping, duplicate accepted handling, and `X-Game-Server-Key` transport support through the sender contract/HTTP sender.
- Extended `ServerPlayerRegistry` to retain `PlayerStateMachine` instances per accepted player and expose:
  - `TryGetPlayerState(Guid playerId, out PlayerStateMachine stateMachine)`
  - `SnapshotResults(DateTime utcNow)`
- `SnapshotResults(...)` reports disconnected players as `disconnected` when they have no other terminal outcome.
- Did not wire runtime result submission, metrics, backend changes, load runner changes, or report/runtime status flow.

## What I Tested And Test Results

- Added EditMode tests for extraction progress, match result building, match result submission mapping, and registry disconnected snapshots.
- Unity EditMode GREEN result: `task-5-green.xml` shows `total="69" passed="69" failed="0"`.
- Required whitespace check completed with no whitespace errors. Git emitted line-ending normalization warnings for two existing tracked files only.

## TDD Evidence

### RED Command

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.14f1\Editor\Unity.exe" -batchmode -quit -projectPath "D:\LH_MAIN-phase-02" -runTests -testPlatform EditMode -testResults "D:\LH_MAIN-phase-02\task-5-red.xml"
```

### RED Output

```text
Aborting batchmode due to failure:
Scripts have compiler errors.
```

Editor log compiler evidence:

```text
Assets\Tests\EditMode\MatchResultSubmitterTests.cs(24,55): error CS0246: The type or namespace name 'IMatchResultSender' could not be found (are you missing a using directive or an assembly reference?)
Assets\Tests\EditMode\MatchResultSubmitterTests.cs(38,21): error CS0246: The type or namespace name 'MatchResultSubmissionResponse' could not be found (are you missing a using directive or an assembly reference?)
```

`task-5-red.xml` was not generated because Unity aborted before test execution due to expected compile errors from missing Task 5 production types.

### GREEN Command

Initial command with `-quit` returned no console output and did not create XML:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.14f1\Editor\Unity.exe" -batchmode -quit -projectPath "D:\LH_MAIN-phase-02" -runTests -testPlatform EditMode -testResults "D:\LH_MAIN-phase-02\task-5-green.xml"
```

Per controller ruling, reran without `-quit`:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.14f1\Editor\Unity.exe" -batchmode -projectPath "D:\LH_MAIN-phase-02" -runTests -testPlatform EditMode -testResults "D:\LH_MAIN-phase-02\task-5-green.xml"
```

### GREEN Output

Console output was empty, but Unity wrote XML and Editor log evidence:

```text
Saving results to: D:\LH_MAIN-phase-02\task-5-green.xml
Test run completed. Exiting with code 0 (Ok). Run completed.
```

`task-5-green.xml` summary:

```xml
<test-run id="2" testcasecount="69" result="Passed" total="69" passed="69" failed="0" inconclusive="0" skipped="0" ...>
```

Required whitespace check:

```powershell
git diff --check -- Assets/Scripts/Gameplay Assets/Scripts/Networking Assets/Tests/EditMode
```

Output:

```text
warning: in the working copy of 'Assets/Scripts/Networking/ServerPlayerRegistry.cs', LF will be replaced by CRLF the next time Git touches it
warning: in the working copy of 'Assets/Tests/EditMode/ServerPlayerRegistryTests.cs', LF will be replaced by CRLF the next time Git touches it
```

No whitespace errors were reported.

## Files Changed

- `Assets/Scripts/Gameplay/ExtractionProgress.cs`
- `Assets/Scripts/Gameplay/ExtractionProgress.cs.meta`
- `Assets/Scripts/Gameplay/MatchResultBuilder.cs`
- `Assets/Scripts/Gameplay/MatchResultBuilder.cs.meta`
- `Assets/Scripts/Gameplay/MatchResultModels.cs`
- `Assets/Scripts/Gameplay/MatchResultModels.cs.meta`
- `Assets/Scripts/Gameplay/MatchResultSubmitter.cs`
- `Assets/Scripts/Gameplay/MatchResultSubmitter.cs.meta`
- `Assets/Scripts/Networking/ServerPlayerRegistry.cs`
- `Assets/Tests/EditMode/ExtractionProgressTests.cs`
- `Assets/Tests/EditMode/ExtractionProgressTests.cs.meta`
- `Assets/Tests/EditMode/MatchResultBuilderTests.cs`
- `Assets/Tests/EditMode/MatchResultBuilderTests.cs.meta`
- `Assets/Tests/EditMode/MatchResultSubmitterTests.cs`
- `Assets/Tests/EditMode/MatchResultSubmitterTests.cs.meta`
- `Assets/Tests/EditMode/ServerPlayerRegistryTests.cs`
- `.superpowers/sdd/2026-09-20-phase-05-gameplay-loop/task-5-report.md`

## Self-Review Findings

- Scope stayed within Task 5: no backend changes and no runtime submission/metrics/status wiring.
- Gameplay additions are Unity-free and remain in `LH.Main.Unity.Gameplay`, whose asmdef has `noEngineReferences=true`.
- `MatchResultSubmitter` diagnostic messages include backend host, match id, result id, and server id; they do not log `X-Game-Server-Key` or shared-key values.
- The submitter test verifies duplicate accepted mapping, endpoint path, and shared key propagation through the sender abstraction.
- Registry snapshot behavior is protected by an EditMode test for removed non-terminal players snapshotting as `disconnected`.

## Issues Or Concerns

- Unity's `-quit` GREEN run did not produce XML in this environment. The no-`-quit` rerun produced `task-5-green.xml` and Editor log exit-code evidence.
- `git diff --check` emitted CRLF normalization warnings for two modified tracked files, but no whitespace errors.
