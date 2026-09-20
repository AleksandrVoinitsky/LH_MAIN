# Task 6 Report: Effects Runtime And Instant Med Items

## Summary

- Added runtime-only `EffectRuntime` for instant med item use, med cooldowns, and damage-over-time ticking.
- Added `EffectKind` and `TimedEffect` runtime models under `Assets/Scripts/Gameplay/Effects`.
- Added EditMode coverage for med consume/heal, cooldown rejection before consume, atomic inventory restore on healing rejection, and DOT ticking/removal.

## TDD Evidence

- RED: Added `Assets/Tests/EditMode/EffectRuntimeTests.cs` first.
- RED command with `-quit`: `Unity.exe -batchmode -projectPath D:\LH_MAIN\.worktrees\phase-06-core-match -runTests -testPlatform EditMode -testResults .superpowers/sdd/2026-09-21-phase-06-core-match/task-6-editmode.xml -quit`
- RED result: Unity aborted with compiler errors because `EffectRuntime`/effects types did not exist; XML was not written.
- RED rerun without `-quit`: same compiler-error abort before XML generation.

## Verification

- GREEN command with `-quit`: Unity exited without updating XML.
- GREEN rerun without `-quit`: wrote `.superpowers/sdd/2026-09-21-phase-06-core-match/task-6-editmode.xml`.
- GREEN result: `testcasecount="116" result="Passed" total="116" passed="116" failed="0"`.
- `git diff --check`: no whitespace errors; warnings only for unrelated existing line-ending-normalization on `Assets/DefaultPrefabObjects.asset`, `ProjectSettings/Physics2DSettings.asset`, and `ProjectSettings/ProjectSettings.asset`.

## Notes

- Healing rejection restores the consumed item using a new internal transaction id so failed med use has no player-visible item loss.
- Med cooldown is checked before inventory mutation and applies only after successful healing.
