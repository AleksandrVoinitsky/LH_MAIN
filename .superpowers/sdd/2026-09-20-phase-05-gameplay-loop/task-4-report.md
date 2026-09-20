# Task 4 Report: Unity State Machine And Technical Damage

## What Implemented

- Added Unity-free gameplay assembly `LH.Main.Unity.Gameplay` under `Assets/Scripts/Gameplay`.
- Added `PlayerLifeState` and `PlayerLifeStateExtensions.IsTerminal()` with terminal states `Dead`, `Extracted`, and `Disconnected`.
- Added `TechnicalDamageEvent` with the required `Guid`/amount/kind fields and null-kind normalization.
- Added `PlayerStateMachine` with `MaxHealth = 100`, `MaxWoundedPoints = 30`, duplicate damage correlation tracking, target validation, terminal rejection, extraction transition, and `PlayerStateChange` result struct.
- Added EditMode tests for damage transitions, duplicate damage rejection, terminal rejection, and terminal movement blocking.
- Updated `NetworkPlayerController` so terminal `LifeState` records `state_terminal` and returns before movement validation.
- Split controller input body into `ApplyAuthoritativeInput()` so the server-authoritative movement path can be tested without FishNet ServerRpc runtime initialization; `ServerApplyInput()` delegates to it.

## What Tested And Results

- Unity EditMode tests:
  - Command used for actual test execution: `& "C:\Program Files\Unity\Hub\Editor\6000.3.14f1\Editor\Unity.exe" -batchmode -projectPath "D:\LH_MAIN-phase-02" -runTests -testPlatform EditMode -testResults ".superpowers/sdd/2026-09-20-phase-05-gameplay-loop/task-4-green.xml"`
  - Result: `testcasecount="63" result="Passed" total="63" passed="63" failed="0"`.
- Whitespace check:
  - Command: `git diff --check -- Assets/Scripts/Gameplay Assets/Scripts/Networking Assets/Tests/EditMode`
  - Result: no whitespace errors; only LF-to-CRLF warnings for touched tracked files.

## TDD Evidence

### RED

Command:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.14f1\Editor\Unity.exe" -batchmode -projectPath "D:\LH_MAIN-phase-02" -runTests -testPlatform EditMode -testResults ".superpowers/sdd/2026-09-20-phase-05-gameplay-loop/task-4-red.xml" -quit
```

Output:

```text
Aborting batchmode due to failure:
Scripts have compiler errors.
```

Relevant compiler errors from `Editor.log`:

```text
Assets\Tests\EditMode\NetworkPlayerControllerTests.cs(1,21): error CS0234: The type or namespace name 'Gameplay' does not exist in the namespace 'LH.Main.Unity' (are you missing an assembly reference?)
Assets\Tests\EditMode\PlayerStateMachineTests.cs(2,21): error CS0234: The type or namespace name 'Gameplay' does not exist in the namespace 'LH.Main.Unity' (are you missing an assembly reference?)
```

### GREEN

The exact brief command with `-quit` was run after implementation, but Unity exited successfully after refresh without invoking the test runner or producing the requested XML. Existing successful Unity test logs in this workspace also omit `-quit`, so actual GREEN evidence used the same full Unity Editor path and test arguments without `-quit`.

Command:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.14f1\Editor\Unity.exe" -batchmode -projectPath "D:\LH_MAIN-phase-02" -runTests -testPlatform EditMode -testResults ".superpowers/sdd/2026-09-20-phase-05-gameplay-loop/task-4-green.xml"
```

Output evidence from `task-4-green.xml`:

```xml
<test-run id="2" testcasecount="63" result="Passed" total="63" passed="63" failed="0" inconclusive="0" skipped="0" asserts="0" ...>
```

Task 4 tests in the same XML:

```text
NetworkPlayerControllerTests.ApplyAuthoritativeInputDoesNotMoveTerminalPlayer: Passed
PlayerStateMachineTests.DamageTransitionsAliveToWoundedToDead: Passed
PlayerStateMachineTests.DuplicateDamageCorrelationDoesNotApplyTwice: Passed
PlayerStateMachineTests.TerminalStateRejectsDamageAndExtraction: Passed
```

## Files Changed

- `Assets/Scripts/Gameplay.meta`
- `Assets/Scripts/Gameplay/LH.Main.Unity.Gameplay.asmdef`
- `Assets/Scripts/Gameplay/LH.Main.Unity.Gameplay.asmdef.meta`
- `Assets/Scripts/Gameplay/PlayerLifeState.cs`
- `Assets/Scripts/Gameplay/PlayerLifeState.cs.meta`
- `Assets/Scripts/Gameplay/TechnicalDamageEvent.cs`
- `Assets/Scripts/Gameplay/TechnicalDamageEvent.cs.meta`
- `Assets/Scripts/Gameplay/PlayerStateMachine.cs`
- `Assets/Scripts/Gameplay/PlayerStateMachine.cs.meta`
- `Assets/Scripts/Networking/LH.Main.Unity.Networking.asmdef`
- `Assets/Scripts/Networking/NetworkPlayerController.cs`
- `Assets/Tests/EditMode/LH.Main.Unity.EditMode.Tests.asmdef`
- `Assets/Tests/EditMode/NetworkPlayerControllerTests.cs`
- `Assets/Tests/EditMode/NetworkPlayerControllerTests.cs.meta`
- `Assets/Tests/EditMode/PlayerStateMachineTests.cs`
- `Assets/Tests/EditMode/PlayerStateMachineTests.cs.meta`
- `.superpowers/sdd/2026-09-20-phase-05-gameplay-loop/task-4-report.md`

## Self-Review Findings

- No backend files, Task 5 result models/submitter, wallet/profile balance fields, or extraction progress/result-building features were added.
- Gameplay DTO/domain classes are Unity-free and isolated in `LH.Main.Unity.Gameplay` with `noEngineReferences: true`.
- `NetworkPlayerController.ServerApplyInput()` still routes through the guarded authoritative input path.
- Directly invoking a FishNet `[ServerRpc]` in EditMode hits generated FishNet writer code before method logic and throws without runtime initialization, so the test covers the extracted authoritative method instead of directly invoking `ServerApplyInput()`.

## Issues Or Concerns

- The brief's exact Unity command including `-quit` exits successfully without running tests or writing result XML in this environment. Actual passing GREEN evidence was generated by omitting `-quit`, matching existing successful test logs in the workspace.
- `git diff --check` prints LF-to-CRLF warnings for touched tracked files, but no whitespace errors.
