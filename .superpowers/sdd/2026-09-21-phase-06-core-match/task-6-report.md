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

## Review Fix: Med Item Validation

- Added a validated `TryUseMedItem` overload that accepts a server-owned `ItemDefinition`.
- The validated path rejects definitions that are not `ItemCategory.MedItem` or are not usable with `med_item_invalid` before cooldown checks or inventory mutation.
- Preserved the original brief signature but made it reject with `med_item_definition_required` before mutation because item id alone cannot prove category or usability.
- Added focused tests for non-med and unusable med definitions preserving inventory and damage state.
- RED: new tests/overload first caused Unity compiler errors because the overload did not exist.
- GREEN: fresh no-`-quit` Task 6 EditMode XML shows `testcasecount="118" result="Passed" total="118" passed="118" failed="0"`; `EffectRuntimeTests` shows `total="6" passed="6" failed="0"`.
- `git diff --check`: no whitespace errors; warnings only for line-ending normalization on touched C# files and unrelated asset/settings files.

## Scoped Re-Review Fix: Required String API Validation

- Restored the brief-required string `TryUseMedItem` API by adding a server-owned item definition registry on `EffectRuntime`.
- Added `RegisterItemDefinition(ItemDefinition)` so callers can register authoritative server definitions before accepting client item-use intent.
- The string overload now rejects unknown item ids with `med_item_unknown`, and registered non-med/unusable items with `med_item_invalid`, before inventory mutation.
- The string overload delegates successful known med use to the validated `ItemDefinition` path, preserving server-side category and usable validation.
- Added focused tests for successful string API med use and invalid/unknown string API rejection without inventory or health mutation.
- RED: new tests first failed at compile time because `RegisterItemDefinition` did not exist.
- GREEN: fresh no-`-quit` Task 6 EditMode XML shows `testcasecount="120" result="Passed" total="120" passed="120" failed="0"`; `EffectRuntimeTests` shows `total="8" passed="8" failed="0"`.
- `git diff --check`: no whitespace errors; warnings only for line-ending normalization on touched C# files and unrelated asset/settings files.
