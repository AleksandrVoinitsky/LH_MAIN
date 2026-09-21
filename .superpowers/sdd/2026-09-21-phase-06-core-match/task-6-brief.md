# Task 6: Effects Runtime And Instant Med Items

## Files

- Create: `Assets/Scripts/Gameplay/Effects/EffectKind.cs`
- Create: `Assets/Scripts/Gameplay/Effects/TimedEffect.cs`
- Create: `Assets/Scripts/Gameplay/Effects/EffectRuntime.cs`
- Test: `Assets/Tests/EditMode/EffectRuntimeTests.cs`

## Interfaces

- Consumes: `PlayerInventory.TryRemove(...)`, `PlayerStateMachine.ApplyHealing(...)`, `HealingEvent`
- Produces: `EffectRuntime.TryUseMedItem(Guid playerId, PlayerInventory inventory, PlayerStateMachine state, string itemId, int healAmount, double serverTimeSeconds, Guid transactionId) : InventoryTransactionResult`
- Produces: `EffectRuntime.AddDamageOverTime(Guid playerId, int damagePerTick, double tickIntervalSeconds, double expiresAtSeconds) : void`
- Produces: `EffectRuntime.Tick(double serverTimeSeconds, Func<Guid, int, string, Guid, bool> applyDamage) : int`

## Steps

1. Create `Assets/Tests/EditMode/EffectRuntimeTests.cs` with tests from the plan covering med item consume/heal and cooldown rejection.
2. Run Unity EditMode tests with results path `.superpowers/sdd/2026-09-21-phase-06-core-match/task-6-editmode.xml`. Expected: FAIL because effects runtime does not exist.
3. Implement effects runtime.
4. `TryUseMedItem` rejects `med_cooldown` before consuming inventory.
5. `TryUseMedItem` removes one med item with `PlayerInventory.TryRemove`, applies `HealingEvent`, and returns the healing rejection reason if healing fails.
6. If healing fails after removal, restore the item with a new internal transaction id so med use remains atomic from the player perspective.
7. `Tick` applies active damage-over-time effects when due and returns the count of applied ticks.
8. Use stable effect ids internally so expired effects are removed after `expiresAtSeconds`.
9. Verify with Unity EditMode tests. Expected: effects, inventory, and state machine tests pass.
10. Run `git diff --check`. Expected: clean.
11. Commit with `git add Assets/Scripts/Gameplay/Effects Assets/Tests/EditMode/EffectRuntimeTests.cs` and `git commit -m "feat: add runtime effects and med use"`.

## Global Constraints

- Effects are runtime-only and server-owned.
- Med items apply instantly after server-side item, cooldown, life-state, and health validation.
- The client sends only use intent; server validates ownership, cooldown, health missing, and life state.
- Use duration and channel interruption are deferred.
- Failed med use returns no player-visible inventory mutation.
