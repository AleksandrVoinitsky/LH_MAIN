# Task 2 Report: Server-Authored Loot Containers

## Summary

- Added `LootEntry`, `LootContainer`, and `LootSpawnPoint` under `LH.Main.Unity.Gameplay.Loot`.
- Added EditMode coverage for successful one-time pickup and out-of-range rejection.
- Enabled UnityEngine references for the Gameplay assembly because Task 2 requires `Vector3` and `MonoBehaviour`.

## TDD Evidence

- RED command: `Unity.exe -batchmode -quit -projectPath "D:\LH_MAIN\.worktrees\phase-06-core-match" -runTests -testPlatform EditMode -testResults "D:\LH_MAIN\.worktrees\phase-06-core-match\.superpowers\sdd\2026-09-21-phase-06-core-match\task-2-editmode.xml"`
- RED result: Unity aborted with `Scripts have compiler errors` because `LH.Main.Unity.Gameplay.Loot` and loot types did not exist. No XML was written.
- RED rerun without `-quit`: same compiler-error abort, still before XML creation.
- GREEN command: same command with `-quit`; Unity returned without XML.
- GREEN rerun without `-quit`: XML written to `.superpowers/sdd/2026-09-21-phase-06-core-match/task-2-editmode.xml`.
- GREEN result: `Passed`, total `92`, passed `92`, failed `0`.
- Required fixtures in XML: `LootContainerTests` passed `2/2`; `PlayerInventoryTests` passed `5/5`.

## Verification

- `git diff --check`: clean; PowerShell output included line-ending warnings only.

## Notes

- `LootContainer` stores runtime loot in a private dictionary protected by a private lock.
- `TryPickup` rejects empty loot ids with `loot_id_required`, missing entries with `loot_unavailable`, and out-of-range pickups with `interaction_out_of_range` before inventory mutation.
- Loot is removed only after `PlayerInventory.TryAdd` accepts; inventory rejection reasons are returned unchanged.

## Review Fix: Definition Validation and Scene Component

- Added `PickupRejectsMismatchedItemDefinitionWithoutMutation` to cover the caller-supplied `ItemDefinition` mismatch case.
- Added `LootContainerCanExistAsSceneComponent` to cover Unity component authoring.
- RED command with `-quit`: Unity aborted before XML after adding the review tests.
- RED rerun without `-quit`: same compiler-error abort before XML.
- Implemented `LootContainer : MonoBehaviour` while preserving `AddLoot`, `TryPickup`, and `GetAvailableLoot` public APIs.
- Added pre-mutation definition validation in `TryPickup`; mismatched `definition.ItemId` and `LootEntry.Stack.ItemId` returns `loot_item_mismatch` with inventory and loot unchanged.
- Corrected the mismatch test setup to use existing `ItemCategory.MedItem` after Unity reported invalid test data for `ItemCategory.Consumable` during verification.
- GREEN command with `-quit`: Unity returned without refreshing XML.
- GREEN rerun without `-quit`: XML written to `.superpowers/sdd/2026-09-21-phase-06-core-match/task-2-editmode.xml`.
- GREEN result: `Passed`, total `94`, passed `94`, failed `0`; `LootContainerTests` passed `4/4`.
- `git diff --check`: clean; PowerShell output included line-ending warnings only.
