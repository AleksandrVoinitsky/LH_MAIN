# Task 3: Player Health, Body Zones, Damage, And Healing

## Files

- Create: `Assets/Scripts/Gameplay/Combat/BodyZone.cs`
- Create: `Assets/Scripts/Gameplay/Combat/DamageEvent.cs`
- Create: `Assets/Scripts/Gameplay/Combat/HealingEvent.cs`
- Create: `Assets/Scripts/Gameplay/Combat/BodyZoneDamageTable.cs`
- Modify: `Assets/Scripts/Gameplay/PlayerStateMachine.cs`
- Test: `Assets/Tests/EditMode/PlayerStateMachineTests.cs`

## Interfaces

- Produces: `enum BodyZone { Head, Torso, Arms, Legs }`
- Produces: `DamageEvent(Guid correlationId, Guid? sourcePlayerId, Guid targetPlayerId, int baseAmount, BodyZone bodyZone, string kind)`
- Produces: `HealingEvent(Guid correlationId, Guid targetPlayerId, int amount, string kind)`
- Produces: `BodyZoneDamageTable.Standard() : BodyZoneDamageTable`
- Produces: `BodyZoneDamageTable.CalculateDamage(int baseAmount, BodyZone bodyZone) : int`
- Modifies: `PlayerStateMachine.ApplyDamage(DamageEvent damage, BodyZoneDamageTable table) : PlayerStateChange`
- Modifies: `PlayerStateMachine.ApplyHealing(HealingEvent healing) : PlayerStateChange`

## Steps

1. Extend `Assets/Tests/EditMode/PlayerStateMachineTests.cs` with tests covering head multiplier damage and healing reducing damage without reviving dead players, using the exact behaviors from the implementation plan.
2. Run Unity EditMode tests with results path `.superpowers/sdd/2026-09-21-phase-06-core-match/task-3-editmode.xml`. Expected: FAIL because typed combat classes and healing methods do not exist.
3. Implement combat health model.
4. Add body-zone multiplier table values: head `2.0`, torso `1.0`, arms `0.75`, legs `0.75`.
5. Round damage with `Math.Ceiling` and clamp minimum accepted damage to `1`.
6. Add `_appliedHealingCorrelations` beside existing damage correlations.
7. Preserve `ApplyDamage(TechnicalDamageEvent)` exactly for Phase 05 callers by converting technical damage to torso-equivalent raw damage or keeping existing code path.
8. `ApplyHealing` rejects terminal extracted/dead players with `state_terminal`, mismatched target with `healing_target_mismatch`, non-positive amount with `healing_invalid`, duplicate correlation with `healing_duplicate`, and no missing health with `healing_not_needed`.
9. Verify with Unity EditMode tests. Expected: all `PlayerStateMachineTests` pass and existing Phase 05 damage tests still pass.
10. Run `git diff --check`. Expected: clean.
11. Commit with `git add Assets/Scripts/Gameplay/Combat/BodyZone.cs Assets/Scripts/Gameplay/Combat/DamageEvent.cs Assets/Scripts/Gameplay/Combat/HealingEvent.cs Assets/Scripts/Gameplay/Combat/BodyZoneDamageTable.cs Assets/Scripts/Gameplay/PlayerStateMachine.cs Assets/Tests/EditMode/PlayerStateMachineTests.cs` and `git commit -m "feat: add typed damage and healing"`.

## Global Constraints

- Damage zones are head, torso, arms, and legs; armor and helmets are out of scope.
- Server owns damage and healing; clients send intent only.
- Damage and healing correlation ids prevent duplicate application where repeated network delivery is possible.
- Preserve terminal-state safety.
- Existing Phase 05 technical damage behavior remains compatible.
