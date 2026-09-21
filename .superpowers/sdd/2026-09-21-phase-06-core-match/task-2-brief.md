# Task 2: Server-Authored Loot Containers

## Files

- Create: `Assets/Scripts/Gameplay/Loot/LootEntry.cs`
- Create: `Assets/Scripts/Gameplay/Loot/LootContainer.cs`
- Create: `Assets/Scripts/Gameplay/Loot/LootSpawnPoint.cs`
- Test: `Assets/Tests/EditMode/LootContainerTests.cs`

## Interfaces

- Consumes: `ItemDefinition`, `ItemStack`, `PlayerInventory.TryAdd(...)`
- Produces: `LootEntry(Guid lootId, ItemStack stack, Vector3 position)`
- Produces: `LootContainer.AddLoot(LootEntry entry) : bool`
- Produces: `LootContainer.TryPickup(Guid lootId, PlayerInventory inventory, ItemDefinition definition, Guid transactionId, Vector3 playerPosition, float interactionRange) : InventoryTransactionResult`
- Produces: `LootContainer.GetAvailableLoot() : IReadOnlyList<LootEntry>`
- Produces: `LootSpawnPoint.BuildEntry(Guid lootId) : LootEntry`

## Steps

1. Create `Assets/Tests/EditMode/LootContainerTests.cs` with tests covering `PickupConsumesLootOnce` and `PickupRejectsOutOfRangePlayer` from the implementation plan.
2. Run the Unity EditMode command with results path `.superpowers/sdd/2026-09-21-phase-06-core-match/task-2-editmode.xml`. Expected: FAIL because loot classes do not exist.
3. Implement loot classes.
4. Create `LootEntry` as an immutable value with `LootId`, `Stack`, and `Position`.
5. Create `LootContainer` with a private lock and dictionary keyed by `Guid`.
6. `TryPickup` must check range before inventory mutation, call `PlayerInventory.TryAdd`, and remove loot only after the inventory accepts.
7. Return `loot_id_required`, `loot_unavailable`, `interaction_out_of_range`, or the inventory rejection reason.
8. Create `LootSpawnPoint : MonoBehaviour` with serialized `itemId`, `quantity`, and optional local offset. `BuildEntry(Guid lootId)` returns a `LootEntry` at `transform.position + offset`.
9. Verify with Unity EditMode tests. Expected: `LootContainerTests` and `PlayerInventoryTests` pass.
10. Run `git diff --check`. Expected: clean.
11. Commit with `git add Assets/Scripts/Gameplay/Loot Assets/Tests/EditMode/LootContainerTests.cs` and `git commit -m "feat: add server-owned loot containers"`.

## Global Constraints

- Loot comes from server-authored Unity scene components: `LootSpawnPoint` and `LootContainer`.
- Each spawned loot entry has a unique runtime id consumed atomically during pickup so concurrent clients cannot duplicate loot.
- Clients send intent only; server validates item existence, interaction range, slot capacity, stack limits, and duplicate transaction ids.
- Failed attempts should return no mutation and preserve loot unless pickup fully succeeds.
- Runtime-only persistence: loot and inventory state do not persist after the match.
