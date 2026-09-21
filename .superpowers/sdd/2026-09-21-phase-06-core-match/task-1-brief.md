# Task 1: Runtime Item Definitions And Inventory Rules

## Files

- Create: `Assets/Scripts/Gameplay/Items/ItemCategory.cs`
- Create: `Assets/Scripts/Gameplay/Items/ItemDefinition.cs`
- Create: `Assets/Scripts/Gameplay/Items/ItemStack.cs`
- Create: `Assets/Scripts/Gameplay/Items/InventorySlot.cs`
- Create: `Assets/Scripts/Gameplay/Items/InventoryTransactionResult.cs`
- Create: `Assets/Scripts/Gameplay/Items/PlayerInventory.cs`
- Test: `Assets/Tests/EditMode/PlayerInventoryTests.cs`

## Interfaces

- Produces: `enum ItemCategory { Weapon, Ammo, Grenade, MedItem, QuestValue }`
- Produces: `ItemDefinition(string itemId, ItemCategory category, int maxStack, bool usable)`
- Produces: `ItemStack(string itemId, int quantity)`
- Produces: `InventorySlot.Empty : InventorySlot`
- Produces: `InventoryTransactionResult.AcceptedResult(int slotIndex, ItemStack stack) : InventoryTransactionResult`
- Produces: `InventoryTransactionResult.Rejected(string reason) : InventoryTransactionResult`
- Produces: `PlayerInventory(int slotCount)`
- Produces: `PlayerInventory.TryAdd(ItemDefinition definition, int quantity, Guid transactionId) : InventoryTransactionResult`
- Produces: `PlayerInventory.TryRemove(string itemId, int quantity, Guid transactionId) : InventoryTransactionResult`
- Produces: `PlayerInventory.GetSlots() : IReadOnlyList<InventorySlot>`
- Produces: `PlayerInventory.ContainsTransaction(Guid transactionId) : bool`

## Steps

1. Run `git status --short --branch`. Expected: only approved plan/spec changes or a clean worktree. Do not revert unrelated files.
2. Create `Assets/Tests/EditMode/PlayerInventoryTests.cs` with the tests from the implementation plan covering stacking, full inventory rejection, duplicate add transaction rejection, and remove quantity consumption.
3. Run `& "C:\Program Files\Unity\Hub\Editor\6000.3.14f1\Editor\Unity.exe" -batchmode -projectPath . -runTests -testPlatform EditMode -testResults .superpowers/sdd/2026-09-21-phase-06-core-match/task-1-editmode.xml -quit`. Expected: FAIL because `LH.Main.Unity.Gameplay.Items` types do not exist.
4. Implement the item and inventory types. Use immutable structs/classes for definitions and stacks. In `PlayerInventory`, reject invalid quantities with `quantity_invalid`, empty item ids with `item_id_required`, duplicate transaction ids with `transaction_duplicate`, missing removals with `item_not_found`, and capacity failures with `inventory_full`.
5. Apply this controller ruling: `TryAdd` must preflight or rollback so capacity failures do not partially mutate inventory. This overrides the plan snippet if it would partially mutate before returning `inventory_full`.
6. Verify with the Unity EditMode command above. If Unity exits before writing XML due to `-quit`, rerun the same full Unity test command without `-quit` and use the XML evidence.
7. Run `git diff --check`.
8. Commit with `git add Assets/Scripts/Gameplay/Items Assets/Tests/EditMode/PlayerInventoryTests.cs` and `git commit -m "feat: add runtime inventory rules"`.

## Global Constraints

- Runtime-only persistence: match inventory, loot, ammo, effects, med-item use, and extracted loot do not persist after the match.
- Clients send intent only; server owns inventory mutations, damage, projectile hits, grenade explosions, healing, and zone state.
- Inventory is slot-based only: no weight, grid layout, volume, nested containers, or persistent stash.
- Item categories are weapon, ammo, grenade, med item, and quest/value item.
- Do not add backend item tables, stash APIs, item economy, armor, attachments, final content balance, AI, client prediction, custom transport, or production art.
