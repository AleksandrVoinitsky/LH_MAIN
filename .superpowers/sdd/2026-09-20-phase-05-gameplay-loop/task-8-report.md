# Task 8 Report: Final End-To-End Verification

## Summary

Task 8 final verification completed for Phase 05 against a clean local Compose database. Unity `-quit` runs returned before refreshing XML/JSON evidence in this environment, so the same Unity commands were rerun without `-quit` per the task brief.

## Fixes During Verification

- `NetworkedCoreLoadRunner` now allows result submission when the Editor load-runner disables `ServerBootstrap`, while preserving a machine note for the unavailable Phase 05 finalization hook.
- `NetworkedCoreLoadRunner` now assigns `phase05_test_reward` to every Phase 05 participant outcome so backend result verification accepts the 64-client payload and persists exactly one reward transaction per participant.

## Evidence

Backend tests:

```powershell
dotnet test server\LH.Main.Server.sln --configuration Release
```

Result: `LH.Main.Contracts.Tests` passed `1/1`; `LH.Main.Backend.Api.Tests` passed `66/66`.

Unity EditMode:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.14f1\Editor\Unity.exe" -batchmode -projectPath "D:\LH_MAIN-phase-02" -runTests -testPlatform EditMode -testResults ".superpowers/sdd/2026-09-20-phase-05-gameplay-loop/task-8-final-editmode.xml"
```

Result: `.superpowers/sdd/2026-09-20-phase-05-gameplay-loop/task-8-final-editmode.xml` reports `83/83` passed, `failed=0`.

Compose reset and rebuild:

```powershell
docker compose --env-file .env.example --profile game-servers down --volumes
docker compose --env-file .env.example --profile game-servers up --build --detach --wait backend-api game-server-1 game-server-2
```

Result: Postgres, backend-api, game-server-1, and game-server-2 became healthy.

One-client smoke:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.14f1\Editor\Unity.exe" -batchmode -projectPath "D:\LH_MAIN-phase-02" -executeMethod LH.Main.Unity.Editor.NetworkedCoreLoadRunner.Run -lhClients 1 -lhDurationSeconds 30 -lhPhase05GameplayLoop true -lhReportPath ".superpowers/sdd/2026-09-20-phase-05-gameplay-loop/task-8-final-load-smoke.json"
```

Result: `targetClients=1`, `connectedClients=1`, `spawnedClients=1`, `completedClients=1`, `failedClients=0`, `extractedClients=1`, `deadClients=0`, `disconnectedOutcomeClients=0`, `resultSubmitted=true`, `duplicateResultAccepted=true`, `rewardTransactions=1`, `disconnectReasons=[]`, `metricCollectionGaps=[]`.

64-client final smoke:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.14f1\Editor\Unity.exe" -batchmode -projectPath "D:\LH_MAIN-phase-02" -executeMethod LH.Main.Unity.Editor.NetworkedCoreLoadRunner.Run -lhClients 64 -lhDurationSeconds 300 -lhPhase05GameplayLoop true -lhReportPath ".superpowers/sdd/2026-09-20-phase-05-gameplay-loop/task-8-final-load-64.json"
```

Result: `targetClients=64`, `connectedClients=64`, `spawnedClients=64`, `completedClients=64`, `failedClients=0`, `extractedClients=22`, `deadClients=21`, `disconnectedOutcomeClients=21`, `resultSubmitted=true`, `duplicateResultAccepted=true`, `rewardTransactions=64`, `disconnectReasons=[]`, `metricCollectionGaps=[]`.

## Red-Green Regression Evidence

- `.superpowers/sdd/2026-09-20-phase-05-gameplay-loop/task-8-red-editmode.xml`: finalization hook unavailable regression failed before the runner change.
- `.superpowers/sdd/2026-09-20-phase-05-gameplay-loop/task-8-green-focused-editmode.xml`: finalization hook unavailable regression passed after the runner change.
- `.superpowers/sdd/2026-09-20-phase-05-gameplay-loop/task-8-red-reward-code-editmode.xml`: all-outcome reward-code regression failed before the payload change.
- `.superpowers/sdd/2026-09-20-phase-05-gameplay-loop/task-8-green-reward-code-editmode.xml`: all-outcome reward-code regression passed after the payload change.

## Notes

- `machineNotes` contains `Phase 05 finalization hook unavailable.` because the Editor load-runner disables `ServerBootstrap` when preparing the local `NetworkManager`. The backend result submission, duplicate submission check, and reward ledger evidence are still exercised by the load-runner payload.
- No metric collection gaps were reported by the one-client or 64-client final reports.

## Fix Round 1 Hygiene Evidence

Command:

```powershell
git status --short
```

Output summary: the working tree still contains pre-existing unrelated local/generated changes, including `task-1-report.md`, Unity/FishNet `.meta` deletions, Unity settings/package/project settings modifications, local `.vsconfig`, Task 5/6 logs, and untracked superpowers plan/spec files. No Task 8 code or doc files were dirty before this report-only fix was applied.

Command:

```powershell
git diff --check
```

Output summary: no whitespace-error lines were reported. Git printed CRLF normalization warnings for unrelated dirty files; no intended Task 8 files required whitespace fixes.

## Final-Review Fix Evidence

Changes:

- `MatchResultBuilder` now emits `phase05_test_reward` for every Phase 05 participant outcome, including extracted, dead, and disconnected runtime snapshots.
- Added an EditMode mixed-outcome builder regression proving all participants receive the supported reward code.
- Added an EditMode finalization regression that configures a `GameServerBootstrap` with a real `ServerPlayerRegistry`, mixed extracted/dead/disconnected player states, and a fake backend-rule sender; it invokes `FinalizeMatchForLoadRunner()` and verifies the submitted payload is accepted only when all reward codes are supported.

Red evidence:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.14f1\Editor\Unity.exe" -batchmode -projectPath "D:\LH_MAIN-phase-02" -runTests -testPlatform EditMode -testFilter "MatchResultBuilderTests.BuildAssignsPhase05RewardCodeToEveryMixedOutcome;GameServerBootstrapAdmissionReflectionTests.FinalizeMatchForLoadRunnerSubmitsMixedRuntimeSnapshotWithSupportedRewards" -testResults ".superpowers/sdd/2026-09-20-phase-05-gameplay-loop/final-review-fix-red-editmode.xml"
```

Result: `.superpowers/sdd/2026-09-20-phase-05-gameplay-loop/final-review-fix-red-editmode.xml` reported `0/2` passed before the production fix. The builder regression failed on an empty dead/disconnected reward code, and the bootstrap finalization regression failed because the fake backend rule rejected the mixed runtime payload.

Green evidence:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.14f1\Editor\Unity.exe" -batchmode -projectPath "D:\LH_MAIN-phase-02" -runTests -testPlatform EditMode -testFilter "MatchResultBuilderTests.BuildAssignsPhase05RewardCodeToEveryMixedOutcome;GameServerBootstrapAdmissionReflectionTests.FinalizeMatchForLoadRunnerSubmitsMixedRuntimeSnapshotWithSupportedRewards" -testResults ".superpowers/sdd/2026-09-20-phase-05-gameplay-loop/final-review-fix-green-editmode.xml"
```

Result: `.superpowers/sdd/2026-09-20-phase-05-gameplay-loop/final-review-fix-green-editmode.xml` reported `2/2` passed, `failed=0`.

Whitespace check:

```powershell
git diff --check -- Assets/Scripts/Gameplay Assets/Scripts/Server Assets/Tests/EditMode README.md docs/07-development/phase-05-gameplay-loop.md .superpowers/sdd/2026-09-20-phase-05-gameplay-loop/task-8-report.md
```

Result: no whitespace-error lines were reported. Git printed CRLF normalization warnings for intended edited files.

Docs: no docs beyond this `task-8-report.md` needed changes.

Concern: the existing load-runner smoke report can still contain `Phase 05 finalization hook unavailable.` because the Editor load-runner disables `ServerBootstrap` while preparing the local `NetworkManager`; this is now covered by the explicit EditMode finalization regression that exercises `GameServerBootstrap.FinalizeMatchForLoadRunner()` directly.
