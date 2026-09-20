# Task 5 Report: Grenade Runtime And Explosion Damage

## Summary

- Added server-side grenade definition, throw request, explosion result, target value type, and runtime under `LH.Main.Unity.Gameplay`.
- `GrenadeRuntime.Tick` waits for fuse expiry, emits one authoritative explosion, prevents duplicate explosion damage, and produces torso `DamageEvent`s for targets inside the blast radius.
- Explosion falloff uses `ceil(maxDamage * (1 - distance / radius))` and clamps in-radius targets to at least `1` base damage.
- Added `GrenadeRuntimeTests` covering fuse timing, duplicate prevention, emitted damage metadata, and distance falloff.

## Verification

- RED command: `& "C:\Program Files\Unity\Hub\Editor\6000.3.14f1\Editor\Unity.exe" -batchmode -quit -projectPath "D:\LH_MAIN\.worktrees\phase-06-core-match" -runTests -testPlatform EditMode -testResults "D:\LH_MAIN\.worktrees\phase-06-core-match\.superpowers\sdd\2026-09-21-phase-06-core-match\task-5-editmode.xml"`
- RED result: Unity aborted with `Scripts have compiler errors.` because grenade runtime production types did not exist.
- GREEN command with `-quit`: Unity exited without writing `task-5-editmode.xml`.
- GREEN fallback command without `-quit`: `& "C:\Program Files\Unity\Hub\Editor\6000.3.14f1\Editor\Unity.exe" -batchmode -projectPath "D:\LH_MAIN\.worktrees\phase-06-core-match" -runTests -testPlatform EditMode -testResults "D:\LH_MAIN\.worktrees\phase-06-core-match\.superpowers\sdd\2026-09-21-phase-06-core-match\task-5-editmode.xml"`
- GREEN result: XML `total="108" passed="108" failed="0"`.
- Whitespace: `git diff --check` completed with no whitespace errors; PowerShell output contained line-ending warnings for existing Unity project files.

## Files

- `Assets/Scripts/Gameplay/Combat/GrenadeDefinition.cs`
- `Assets/Scripts/Gameplay/Combat/GrenadeThrowRequest.cs`
- `Assets/Scripts/Gameplay/Combat/GrenadeExplosionResult.cs`
- `Assets/Scripts/Gameplay/Combat/GrenadeRuntime.cs`
- `Assets/Tests/EditMode/GrenadeRuntimeTests.cs`
- Included Unity `.meta` files for all new Task 5 scripts and tests.

## Review Fixes

- Added constructor validation that rejects non-finite spawn positions, zero throw directions, and non-finite throw directions with reason-coded `ArgumentException` messages.
- Extended `GrenadeDefinition.StandardFrag()` with a minimal `InitialSpeed` value and added `GrenadeRuntime.CurrentPosition` so server-authoritative movement can be observed by integration code and tests.
- `GrenadeRuntime.Tick` now advances authoritative grenade position along normalized throw direction before fuse expiry and uses a Unity `Physics.Raycast` sweep to stop at the first collision.
- Explosion damage now uses the authoritative `CurrentPosition` after simulation, not the original spawn position.
- Added tests for validation rejection, pre-fuse movement, and physics collision stopping.

## Review Fix Verification

- RED command: `& "C:\Program Files\Unity\Hub\Editor\6000.3.14f1\Editor\Unity.exe" -batchmode -quit -projectPath "D:\LH_MAIN\.worktrees\phase-06-core-match" -runTests -testPlatform EditMode -testResults "D:\LH_MAIN\.worktrees\phase-06-core-match\.superpowers\sdd\2026-09-21-phase-06-core-match\task-5-editmode.xml"`
- RED result: Unity aborted with `Scripts have compiler errors.` because the new tests referenced missing `GrenadeRuntime.CurrentPosition` and validation behavior.
- GREEN command with `-quit`: Unity exited without refreshing `task-5-editmode.xml`.
- GREEN fallback command without `-quit`: `& "C:\Program Files\Unity\Hub\Editor\6000.3.14f1\Editor\Unity.exe" -batchmode -projectPath "D:\LH_MAIN\.worktrees\phase-06-core-match" -runTests -testPlatform EditMode -testResults "D:\LH_MAIN\.worktrees\phase-06-core-match\.superpowers\sdd\2026-09-21-phase-06-core-match\task-5-editmode.xml"`
- GREEN result: XML `total="111" passed="111" failed="0"`.
- Whitespace: `git diff --check` completed with no whitespace errors; PowerShell output contained line-ending warnings for existing Unity/project files and modified Task 5 files.

## Scoped Re-Review Fix

- Fixed `GrenadeRuntime.Tick` so `HasExploded` is checked before movement simulation. Duplicate/post-explosion ticks now return `Exploded = false` with no damage and leave `CurrentPosition` unchanged.
- Added `TickDoesNotMoveAuthoritativePositionAfterExplosion` to prove post-explosion ticks do not mutate authoritative position.

## Scoped Re-Review Verification

- RED command with `-quit`: `& "C:\Program Files\Unity\Hub\Editor\6000.3.14f1\Editor\Unity.exe" -batchmode -quit -projectPath "D:\LH_MAIN\.worktrees\phase-06-core-match" -runTests -testPlatform EditMode -testResults "D:\LH_MAIN\.worktrees\phase-06-core-match\.superpowers\sdd\2026-09-21-phase-06-core-match\task-5-editmode.xml"`
- RED fallback command without `-quit`: `& "C:\Program Files\Unity\Hub\Editor\6000.3.14f1\Editor\Unity.exe" -batchmode -projectPath "D:\LH_MAIN\.worktrees\phase-06-core-match" -runTests -testPlatform EditMode -testResults "D:\LH_MAIN\.worktrees\phase-06-core-match\.superpowers\sdd\2026-09-21-phase-06-core-match\task-5-editmode.xml"`
- RED result: XML `total="112" passed="111" failed="1"`; failing test `GrenadeRuntimeTests.TickDoesNotMoveAuthoritativePositionAfterExplosion` showed `CurrentPosition` changed from `(0.00, 0.00, 6.00)` to `(0.00, 0.00, 10.00)` after duplicate tick.
- GREEN command with `-quit`: Unity exited without refreshing the RED XML.
- GREEN fallback command without `-quit`: `& "C:\Program Files\Unity\Hub\Editor\6000.3.14f1\Editor\Unity.exe" -batchmode -projectPath "D:\LH_MAIN\.worktrees\phase-06-core-match" -runTests -testPlatform EditMode -testResults "D:\LH_MAIN\.worktrees\phase-06-core-match\.superpowers\sdd\2026-09-21-phase-06-core-match\task-5-editmode.xml"`
- GREEN result: XML `total="112" passed="112" failed="0"`.
- Whitespace: `git diff --check` completed with no whitespace errors; PowerShell output contained line-ending warnings for existing Unity/project files and modified Task 5 files.
