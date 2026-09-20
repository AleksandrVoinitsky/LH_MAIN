# Task 7 Report: Phase 05 Load Runner And Reports

## What Changed

- Added Phase 05 gameplay-loop fields to `LoadScenarioReport` JSON: `extractedClients`, `deadClients`, `disconnectedOutcomeClients`, `resultSubmitted`, `duplicateResultAccepted`, and `rewardTransactions`.
- Added deterministic Phase 05 bot grouping by client index: `index % 3 == 0` extraction report path, `index % 3 == 1` technical damage/death path, `index % 3 == 2` disconnected outcome path.
- Added load-runner parsing for `-lhClients`, `-lhDurationSeconds`, `-lhBackendUrl`, `-lhReportPath`, `-lhSharedKey`, and `-lhPhase05GameplayLoop true` while preserving existing `--...` options.
- Switched the default report path to `.superpowers/sdd/2026-09-20-phase-05-gameplay-loop/task-7-final-load-64.json` when Phase 05 mode is enabled and `LH_LOAD_REPORT_PATH`/`-lhReportPath` are not set.
- Added Phase 05 result submission from the load runner using the existing `/internal/v1/matches/results` submitter, including duplicate retry accounting from `NewRewardTransactions`.
- Updated README and `docs/07-development/phase-05-gameplay-loop.md` with Task 7 commands and report fields.
- Added EditMode coverage for report serialization, Phase 05 option/default path parsing, bot grouping, and submission outcome accounting.

## RED Test Command/Output

Command:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.14f1\Editor\Unity.exe" -batchmode -projectPath "D:\LH_MAIN-phase-02" -runTests -testPlatform EditMode -testResults ".superpowers/sdd/2026-09-20-phase-05-gameplay-loop/task-7-red.xml" -quit
```

Output:

```text
Aborting batchmode due to failure:
Scripts have compiler errors.
```

Expected RED cause: new tests referenced missing Task 7 report/options/bot members.

## GREEN Test Command/Output

Command with `-quit`:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.14f1\Editor\Unity.exe" -batchmode -projectPath "D:\LH_MAIN-phase-02" -runTests -testPlatform EditMode -testResults ".superpowers/sdd/2026-09-20-phase-05-gameplay-loop/task-7-green.xml" -quit
```

Output:

```text
(no output; Unity exited before writing XML)
```

Rerun without `-quit` using the same full test args:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.14f1\Editor\Unity.exe" -batchmode -projectPath "D:\LH_MAIN-phase-02" -runTests -testPlatform EditMode -testResults ".superpowers/sdd/2026-09-20-phase-05-gameplay-loop/task-7-green.xml"
```

XML evidence:

```xml
<test-run id="2" testcasecount="80" result="Passed" total="80" passed="80" failed="0" inconclusive="0" skipped="0" asserts="0" engine-version="3.5.0.0" clr-version="4.0.30319.42000" start-time="2026-09-20 15:52:44Z" end-time="2026-09-20 15:52:46Z" duration="2,1685047">
```

## Diff-Check Command/Output

Command:

```powershell
git diff --check -- Assets/Scripts/Load Assets/Scripts/Editor Assets/Tests/EditMode README.md docs/07-development/phase-05-gameplay-loop.md
```

Output:

```text
warning: in the working copy of 'Assets/Scripts/Editor/NetworkedCoreLoadRunner.cs', LF will be replaced by CRLF the next time Git touches it
warning: in the working copy of 'Assets/Scripts/Load/HeadlessMatchBot.cs', LF will be replaced by CRLF the next time Git touches it
warning: in the working copy of 'Assets/Scripts/Load/LoadScenarioReport.cs', LF will be replaced by CRLF the next time Git touches it
warning: in the working copy of 'Assets/Tests/EditMode/HeadlessMatchBotTests.cs', LF will be replaced by CRLF the next time Git touches it
warning: in the working copy of 'Assets/Tests/EditMode/LoadScenarioReportTests.cs', LF will be replaced by CRLF the next time Git touches it
warning: in the working copy of 'Assets/Tests/EditMode/NetworkedCoreLoadRunnerTests.cs', LF will be replaced by CRLF the next time Git touches it
warning: in the working copy of 'README.md', LF will be replaced by CRLF the next time Git touches it
warning: in the working copy of 'docs/07-development/phase-05-gameplay-loop.md', LF will be replaced by CRLF the next time Git touches it
```

No whitespace errors were reported.

## Concerns

- Unity EditMode with `-quit` repeatedly exited before writing XML; the required no-`-quit` rerun produced passing XML evidence.
- The Phase 05 extraction branch is represented in the load-runner report/submitted result payload. No new server extraction hook was added because Task 7 scope forbids backend/service changes and existing Task 6 hooks expose only technical damage and finalization.
