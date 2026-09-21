# Phase 06 Core Match Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a runtime-only, server-authoritative Unity/FishNet core match with slot inventory, scene-authored loot, baseline weapon combat, grenades, med items, effects, shrinking zone damage, and a 64-client technical load scenario.

**Architecture:** Keep the Unity dedicated server as the authoritative runtime. Put deterministic gameplay rules in small `Assets/Scripts/Gameplay/*` classes covered by EditMode tests, then expose them through FishNet intent RPC adapters and load-runner hooks. Keep backend changes out of scope except preserving existing Phase 05 match-result verification.

**Tech Stack:** Unity 6000.3.14f1, FishNet, Tugboat, C# EditMode tests with NUnit, Unity Physics raycasts/colliders/layers, ASP.NET Core backend tests for regression only, PowerShell-compatible verification commands.

**Spec:** `docs/superpowers/specs/2026-09-21-phase-06-core-match-design.md`

## Global Constraints

- Runtime-only persistence: match inventory, loot, ammo, effects, med-item use, and extracted loot do not persist after the match.
- Existing Phase 05 match result flow remains the only post-match backend integration.
- Clients send intent only; server owns inventory mutations, damage, projectile hits, grenade explosions, healing, and zone state.
- Inventory is slot-based only: no weight, grid layout, volume, nested containers, or persistent stash.
- Loot comes from server-authored Unity scene components: `LootSpawnPoint` and `LootContainer`.
- Item categories are weapon, ammo, grenade, med item, and quest/value item.
- Weapon scope is one baseline server-authoritative hitscan weapon with ammo and reload.
- Damage zones are head, torso, arms, and legs; armor and helmets are out of scope.
- Grenades are server-simulated entities with Unity Physics collision checks and fuse-triggered explosion.
- Med items apply instantly after server-side item, cooldown, life-state, and health validation.
- Shrinking zone uses server-owned Unity scene volumes; clients receive replicated state/events only.
- Phase 05 extraction remains valid without loot requirement, quest requirement, or zone/hazard blocker.
- Required load-readiness target is one 64-client technical match covering loot contention, inventory capacity, weapon fire, reload/ammo consumption, grenade explosion, med use, and zone damage.
- Do not add backend item tables, stash APIs, item economy, armor, attachments, final content balance, AI, client prediction, custom transport, or production art.
- Run `git status --short --branch` before editing and do not revert unrelated user changes.
- If executing this plan with user approval, commit after each completed task using the task commit message.

---

## File Structure

- Create `Assets/Scripts/Gameplay/Items/ItemCategory.cs`: item category enum.
- Create `Assets/Scripts/Gameplay/Items/ItemDefinition.cs`: immutable runtime item definition with stack/capacity rules.
- Create `Assets/Scripts/Gameplay/Items/ItemStack.cs`: item id and quantity value type.
- Create `Assets/Scripts/Gameplay/Items/InventorySlot.cs`: slot value type.
- Create `Assets/Scripts/Gameplay/Items/InventoryTransactionResult.cs`: accepted/rejected inventory operation result.
- Create `Assets/Scripts/Gameplay/Items/PlayerInventory.cs`: fixed-slot inventory operations and duplicate transaction protection.
- Create `Assets/Tests/EditMode/PlayerInventoryTests.cs`: inventory unit tests.
- Create `Assets/Scripts/Gameplay/Loot/LootEntry.cs`: unique runtime loot entry state.
- Create `Assets/Scripts/Gameplay/Loot/LootContainer.cs`: server-owned loot container with atomic pickup.
- Create `Assets/Scripts/Gameplay/Loot/LootSpawnPoint.cs`: scene-authored loot seed component.
- Create `Assets/Tests/EditMode/LootContainerTests.cs`: pickup contention and spawn tests.
- Modify `Assets/Scripts/Gameplay/TechnicalDamageEvent.cs`: keep compatibility while typed combat damage is added beside it.
- Create `Assets/Scripts/Gameplay/Combat/BodyZone.cs`: body zone enum.
- Create `Assets/Scripts/Gameplay/Combat/DamageEvent.cs`: typed damage correlation event.
- Create `Assets/Scripts/Gameplay/Combat/HealingEvent.cs`: typed heal correlation event.
- Create `Assets/Scripts/Gameplay/Combat/BodyZoneDamageTable.cs`: damage multiplier rules.
- Modify `Assets/Scripts/Gameplay/PlayerStateMachine.cs`: add typed damage/healing while preserving existing technical damage methods.
- Modify `Assets/Tests/EditMode/PlayerStateMachineTests.cs`: health/damage/healing regression coverage.
- Create `Assets/Scripts/Gameplay/Combat/WeaponRuntimeState.cs`: weapon magazine/reserve/reload state.
- Create `Assets/Scripts/Gameplay/Combat/WeaponDefinition.cs`: baseline weapon config.
- Create `Assets/Scripts/Gameplay/Combat/WeaponFireRequest.cs`: client intent value type.
- Create `Assets/Scripts/Gameplay/Combat/WeaponFireResult.cs`: accepted/rejected shot result.
- Create `Assets/Scripts/Gameplay/Combat/WeaponRuntime.cs`: ammo, cooldown, reload, duplicate-fire logic.
- Create `Assets/Scripts/Gameplay/Combat/BodyZoneHitbox.cs`: Unity collider adapter identifying player/body zone.
- Create `Assets/Scripts/Gameplay/Combat/ServerRaycastWeaponResolver.cs`: Unity Physics raycast hit resolution.
- Create `Assets/Tests/EditMode/WeaponRuntimeTests.cs`: pure weapon rules tests.
- Create `Assets/Tests/EditMode/ServerRaycastWeaponResolverTests.cs`: Unity Physics hit-zone tests.
- Create `Assets/Scripts/Gameplay/Combat/GrenadeDefinition.cs`: fuse/radius/falloff config.
- Create `Assets/Scripts/Gameplay/Combat/GrenadeThrowRequest.cs`: throw intent value type.
- Create `Assets/Scripts/Gameplay/Combat/GrenadeRuntime.cs`: server-simulated fuse/explosion logic.
- Create `Assets/Scripts/Gameplay/Combat/GrenadeExplosionResult.cs`: explosion target damage results.
- Create `Assets/Tests/EditMode/GrenadeRuntimeTests.cs`: fuse, single explosion, radius falloff tests.
- Create `Assets/Scripts/Gameplay/Effects/EffectKind.cs`: effect kind enum.
- Create `Assets/Scripts/Gameplay/Effects/TimedEffect.cs`: active runtime effect value type.
- Create `Assets/Scripts/Gameplay/Effects/EffectRuntime.cs`: cooldowns, med use, damage-over-time ticks.
- Create `Assets/Tests/EditMode/EffectRuntimeTests.cs`: cooldown, heal, damage-over-time tests.
- Create `Assets/Scripts/Gameplay/Zone/ZonePhase.cs`: phase timing and damage config.
- Create `Assets/Scripts/Gameplay/Zone/ZoneRuntime.cs`: phase advancement and damage tick scheduling.
- Create `Assets/Scripts/Gameplay/Zone/ZoneVolume.cs`: scene volume adapter for safe/hazard membership.
- Create `Assets/Tests/EditMode/ZoneRuntimeTests.cs`: phase and tick tests.
- Create `Assets/Tests/EditMode/ZoneVolumeTests.cs`: collider membership tests.
- Create `Assets/Scripts/Gameplay/CoreMatchRuntime.cs`: server-side composition root for inventories, loot, combat, effects, and zone.
- Modify `Assets/Scripts/Networking/NetworkPlayerController.cs`: add server RPC intent methods delegating to `CoreMatchRuntime`.
- Modify `Assets/Scripts/Networking/GameServerMetrics.cs`: add Phase 06 counters.
- Modify `Assets/Scripts/Server/GameServerStatus.cs`: include Phase 06 metrics in status JSON.
- Modify `Assets/Scripts/Server/GameServerBootstrap.cs`: initialize runtime, tick zone/grenades/effects, expose load-runner hooks.
- Modify `Assets/Tests/EditMode/NetworkPlayerControllerTests.cs`: verify intent APIs are server-only and terminal-state safe.
- Modify `Assets/Tests/EditMode/GameServerStatusTests.cs`: verify Phase 06 metric fields.
- Modify `Assets/Scripts/Load/HeadlessMatchBot.cs`: add Phase 06 scenario actions/results.
- Modify `Assets/Scripts/Load/LoadScenarioReport.cs`: add Phase 06 report counters.
- Modify `Assets/Scripts/Editor/NetworkedCoreLoadRunner.cs`: add Phase 06 flag, report path, success criteria.
- Modify `Assets/Tests/EditMode/HeadlessMatchBotTests.cs`: verify Phase 06 bot action scheduling.
- Modify `Assets/Tests/EditMode/LoadScenarioReportTests.cs`: verify Phase 06 JSON counters.
- Modify `Assets/Tests/EditMode/NetworkedCoreLoadRunnerTests.cs`: verify Phase 06 success/failure criteria.
- Create `.superpowers/sdd/2026-09-21-phase-06-core-match/`: execution notes and final report artifacts during implementation.

---

### Task 1: Runtime Item Definitions And Inventory Rules

**Files:**
- Create: `Assets/Scripts/Gameplay/Items/ItemCategory.cs`
- Create: `Assets/Scripts/Gameplay/Items/ItemDefinition.cs`
- Create: `Assets/Scripts/Gameplay/Items/ItemStack.cs`
- Create: `Assets/Scripts/Gameplay/Items/InventorySlot.cs`
- Create: `Assets/Scripts/Gameplay/Items/InventoryTransactionResult.cs`
- Create: `Assets/Scripts/Gameplay/Items/PlayerInventory.cs`
- Test: `Assets/Tests/EditMode/PlayerInventoryTests.cs`

**Interfaces:**
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

- [ ] **Step 1: Inspect worktree**

Run: `git status --short --branch`

Expected: only approved plan/spec changes or a clean worktree. Do not revert unrelated files.

- [ ] **Step 2: Write failing inventory tests**

Create `Assets/Tests/EditMode/PlayerInventoryTests.cs`:

```csharp
using System;
using LH.Main.Unity.Gameplay.Items;
using NUnit.Framework;

public sealed class PlayerInventoryTests
{
    [Test]
    public void TryAddStacksItemsUpToDefinitionLimit()
    {
        var inventory = new PlayerInventory(2);
        var ammo = new ItemDefinition("ammo_9mm", ItemCategory.Ammo, 30, false);

        InventoryTransactionResult first = inventory.TryAdd(ammo, 20, Guid.Parse("11111111-1111-1111-1111-111111111111"));
        InventoryTransactionResult second = inventory.TryAdd(ammo, 15, Guid.Parse("22222222-2222-2222-2222-222222222222"));

        Assert.That(first.Accepted, Is.True);
        Assert.That(second.Accepted, Is.True);
        Assert.That(inventory.GetSlots()[0].Stack.ItemId, Is.EqualTo("ammo_9mm"));
        Assert.That(inventory.GetSlots()[0].Stack.Quantity, Is.EqualTo(30));
        Assert.That(inventory.GetSlots()[1].Stack.Quantity, Is.EqualTo(5));
    }

    [Test]
    public void TryAddRejectsWhenInventoryIsFull()
    {
        var inventory = new PlayerInventory(1);
        var med = new ItemDefinition("med_small", ItemCategory.MedItem, 1, true);
        inventory.TryAdd(med, 1, Guid.Parse("11111111-1111-1111-1111-111111111111"));

        InventoryTransactionResult result = inventory.TryAdd(med, 1, Guid.Parse("22222222-2222-2222-2222-222222222222"));

        Assert.That(result.Accepted, Is.False);
        Assert.That(result.Reason, Is.EqualTo("inventory_full"));
    }

    [Test]
    public void DuplicateTransactionDoesNotApplyTwice()
    {
        var inventory = new PlayerInventory(2);
        var grenade = new ItemDefinition("grenade_frag", ItemCategory.Grenade, 1, true);
        var transactionId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        InventoryTransactionResult first = inventory.TryAdd(grenade, 1, transactionId);
        InventoryTransactionResult duplicate = inventory.TryAdd(grenade, 1, transactionId);

        Assert.That(first.Accepted, Is.True);
        Assert.That(duplicate.Accepted, Is.False);
        Assert.That(duplicate.Reason, Is.EqualTo("transaction_duplicate"));
        Assert.That(inventory.GetSlots()[1].IsEmpty, Is.True);
    }

    [Test]
    public void TryRemoveConsumesRequestedQuantity()
    {
        var inventory = new PlayerInventory(2);
        var ammo = new ItemDefinition("ammo_9mm", ItemCategory.Ammo, 30, false);
        inventory.TryAdd(ammo, 25, Guid.Parse("11111111-1111-1111-1111-111111111111"));

        InventoryTransactionResult removed = inventory.TryRemove("ammo_9mm", 10, Guid.Parse("44444444-4444-4444-4444-444444444444"));

        Assert.That(removed.Accepted, Is.True);
        Assert.That(inventory.GetSlots()[0].Stack.Quantity, Is.EqualTo(15));
    }
}
```

- [ ] **Step 3: Run tests and verify failure**

Run: `& "C:\Program Files\Unity\Hub\Editor\6000.3.14f1\Editor\Unity.exe" -batchmode -projectPath . -runTests -testPlatform EditMode -testResults .superpowers/sdd/2026-09-21-phase-06-core-match/task-1-editmode.xml -quit`

Expected: FAIL because `LH.Main.Unity.Gameplay.Items` types do not exist.

- [ ] **Step 4: Implement item and inventory types**

Create the files listed above. Use immutable structs/classes for definitions and stacks. In `PlayerInventory`, reject invalid quantities with `quantity_invalid`, empty item ids with `item_id_required`, duplicate transaction ids with `transaction_duplicate`, missing removals with `item_not_found`, and capacity failures with `inventory_full`.

```csharp
public InventoryTransactionResult TryAdd(ItemDefinition definition, int quantity, Guid transactionId)
{
    if (!TryStartTransaction(transactionId, out InventoryTransactionResult duplicate))
        return duplicate;
    if (quantity <= 0)
        return InventoryTransactionResult.Rejected("quantity_invalid");
    if (definition.MaxStack <= 0 || string.IsNullOrWhiteSpace(definition.ItemId))
        return InventoryTransactionResult.Rejected("item_invalid");

    int remaining = quantity;
    for (int i = 0; i < _slots.Length && remaining > 0; i++)
        remaining = TryFillExistingStack(i, definition, remaining);
    for (int i = 0; i < _slots.Length && remaining > 0; i++)
        remaining = TryFillEmptySlot(i, definition, remaining);

    return remaining == 0
        ? InventoryTransactionResult.AcceptedResult(FindLastChangedSlot(definition.ItemId), new ItemStack(definition.ItemId, quantity))
        : InventoryTransactionResult.Rejected("inventory_full");
}
```

If the implementation records a transaction before discovering `inventory_full`, keep that behavior: repeated identical failed network deliveries should not mutate state later.

- [ ] **Step 5: Verify task**

Run the Unity EditMode command from Step 3.

Expected: `PlayerInventoryTests` pass.

Run: `git diff --check`

Expected: no whitespace errors.

- [ ] **Step 6: Commit task**

```bash
git add Assets/Scripts/Gameplay/Items Assets/Tests/EditMode/PlayerInventoryTests.cs
git commit -m "feat: add runtime inventory rules"
```

---

### Task 2: Server-Authored Loot Containers

**Files:**
- Create: `Assets/Scripts/Gameplay/Loot/LootEntry.cs`
- Create: `Assets/Scripts/Gameplay/Loot/LootContainer.cs`
- Create: `Assets/Scripts/Gameplay/Loot/LootSpawnPoint.cs`
- Test: `Assets/Tests/EditMode/LootContainerTests.cs`

**Interfaces:**
- Consumes: `ItemDefinition`, `ItemStack`, `PlayerInventory.TryAdd(...)`
- Produces: `LootEntry(Guid lootId, ItemStack stack, Vector3 position)`
- Produces: `LootContainer.AddLoot(LootEntry entry) : bool`
- Produces: `LootContainer.TryPickup(Guid lootId, PlayerInventory inventory, ItemDefinition definition, Guid transactionId, Vector3 playerPosition, float interactionRange) : InventoryTransactionResult`
- Produces: `LootContainer.GetAvailableLoot() : IReadOnlyList<LootEntry>`
- Produces: `LootSpawnPoint.BuildEntry(Guid lootId) : LootEntry`

- [ ] **Step 1: Write failing loot tests**

Create `Assets/Tests/EditMode/LootContainerTests.cs`:

```csharp
using System;
using LH.Main.Unity.Gameplay.Items;
using LH.Main.Unity.Gameplay.Loot;
using NUnit.Framework;
using UnityEngine;

public sealed class LootContainerTests
{
    [Test]
    public void PickupConsumesLootOnce()
    {
        var definition = new ItemDefinition("med_small", ItemCategory.MedItem, 1, true);
        var inventoryA = new PlayerInventory(2);
        var inventoryB = new PlayerInventory(2);
        var lootId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var container = new LootContainer();
        container.AddLoot(new LootEntry(lootId, new ItemStack("med_small", 1), Vector3.zero));

        InventoryTransactionResult first = container.TryPickup(lootId, inventoryA, definition, Guid.Parse("22222222-2222-2222-2222-222222222222"), Vector3.zero, 2f);
        InventoryTransactionResult second = container.TryPickup(lootId, inventoryB, definition, Guid.Parse("33333333-3333-3333-3333-333333333333"), Vector3.zero, 2f);

        Assert.That(first.Accepted, Is.True);
        Assert.That(second.Accepted, Is.False);
        Assert.That(second.Reason, Is.EqualTo("loot_unavailable"));
        Assert.That(container.GetAvailableLoot(), Is.Empty);
    }

    [Test]
    public void PickupRejectsOutOfRangePlayer()
    {
        var definition = new ItemDefinition("value_relic", ItemCategory.QuestValue, 1, false);
        var inventory = new PlayerInventory(2);
        var lootId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var container = new LootContainer();
        container.AddLoot(new LootEntry(lootId, new ItemStack("value_relic", 1), Vector3.zero));

        InventoryTransactionResult result = container.TryPickup(lootId, inventory, definition, Guid.Parse("22222222-2222-2222-2222-222222222222"), new Vector3(10f, 0f, 0f), 2f);

        Assert.That(result.Accepted, Is.False);
        Assert.That(result.Reason, Is.EqualTo("interaction_out_of_range"));
        Assert.That(container.GetAvailableLoot().Count, Is.EqualTo(1));
    }
}
```

- [ ] **Step 2: Run tests and verify failure**

Run the Unity EditMode command with results path `.superpowers/sdd/2026-09-21-phase-06-core-match/task-2-editmode.xml`.

Expected: FAIL because loot classes do not exist.

- [ ] **Step 3: Implement loot classes**

Create `LootEntry` as an immutable value with `LootId`, `Stack`, and `Position`. Create `LootContainer` with a private lock and dictionary keyed by `Guid`. `TryPickup` must check range before inventory mutation, call `PlayerInventory.TryAdd`, and remove loot only after the inventory accepts. Return `loot_id_required`, `loot_unavailable`, `interaction_out_of_range`, or the inventory rejection reason.

Create `LootSpawnPoint : MonoBehaviour` with serialized `itemId`, `quantity`, and optional local offset. `BuildEntry(Guid lootId)` returns a `LootEntry` at `transform.position + offset`.

- [ ] **Step 4: Verify task**

Run Unity EditMode tests.

Expected: `LootContainerTests` and `PlayerInventoryTests` pass.

Run: `git diff --check`

Expected: clean.

- [ ] **Step 5: Commit task**

```bash
git add Assets/Scripts/Gameplay/Loot Assets/Tests/EditMode/LootContainerTests.cs
git commit -m "feat: add server-owned loot containers"
```

---

### Task 3: Player Health, Body Zones, Damage, And Healing

**Files:**
- Create: `Assets/Scripts/Gameplay/Combat/BodyZone.cs`
- Create: `Assets/Scripts/Gameplay/Combat/DamageEvent.cs`
- Create: `Assets/Scripts/Gameplay/Combat/HealingEvent.cs`
- Create: `Assets/Scripts/Gameplay/Combat/BodyZoneDamageTable.cs`
- Modify: `Assets/Scripts/Gameplay/PlayerStateMachine.cs`
- Test: `Assets/Tests/EditMode/PlayerStateMachineTests.cs`

**Interfaces:**
- Produces: `enum BodyZone { Head, Torso, Arms, Legs }`
- Produces: `DamageEvent(Guid correlationId, Guid? sourcePlayerId, Guid targetPlayerId, int baseAmount, BodyZone bodyZone, string kind)`
- Produces: `HealingEvent(Guid correlationId, Guid targetPlayerId, int amount, string kind)`
- Produces: `BodyZoneDamageTable.Standard() : BodyZoneDamageTable`
- Produces: `BodyZoneDamageTable.CalculateDamage(int baseAmount, BodyZone bodyZone) : int`
- Modifies: `PlayerStateMachine.ApplyDamage(DamageEvent damage, BodyZoneDamageTable table) : PlayerStateChange`
- Modifies: `PlayerStateMachine.ApplyHealing(HealingEvent healing) : PlayerStateChange`

- [ ] **Step 1: Add failing typed damage tests**

Extend `PlayerStateMachineTests.cs`:

```csharp
[Test]
public void BodyZoneDamageAppliesMultiplier()
{
    var playerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    var state = PlayerStateMachine.Create(playerId);
    var table = BodyZoneDamageTable.Standard();

    PlayerStateChange result = state.ApplyDamage(new DamageEvent(Guid.Parse("22222222-2222-2222-2222-222222222222"), null, playerId, 40, BodyZone.Head, "hitscan"), table);

    Assert.That(result.Accepted, Is.True);
    Assert.That(state.DamageTaken, Is.EqualTo(80));
    Assert.That(state.Health, Is.EqualTo(20));
}

[Test]
public void HealingReducesDamageButDoesNotReviveDeadPlayer()
{
    var playerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    var state = PlayerStateMachine.Create(playerId);
    state.ApplyDamage(new TechnicalDamageEvent(Guid.Parse("22222222-2222-2222-2222-222222222222"), null, playerId, 60, "technical"));

    PlayerStateChange healed = state.ApplyHealing(new HealingEvent(Guid.Parse("33333333-3333-3333-3333-333333333333"), playerId, 25, "med_small"));

    Assert.That(healed.Accepted, Is.True);
    Assert.That(state.DamageTaken, Is.EqualTo(35));
    Assert.That(state.LifeState, Is.EqualTo(PlayerLifeState.Alive));
}
```

- [ ] **Step 2: Run tests and verify failure**

Run Unity EditMode tests with results path `.superpowers/sdd/2026-09-21-phase-06-core-match/task-3-editmode.xml`.

Expected: FAIL because typed combat classes and healing methods do not exist.

- [ ] **Step 3: Implement combat health model**

Add body-zone multiplier table values: head `2.0`, torso `1.0`, arms `0.75`, legs `0.75`. Round damage with `Math.Ceiling` and clamp minimum accepted damage to `1`. Add `_appliedHealingCorrelations` beside existing damage correlations. Preserve `ApplyDamage(TechnicalDamageEvent)` exactly for Phase 05 callers by converting technical damage to torso-equivalent raw damage or keeping existing code path.

`ApplyHealing` rejects terminal extracted/dead players with `state_terminal`, mismatched target with `healing_target_mismatch`, non-positive amount with `healing_invalid`, duplicate correlation with `healing_duplicate`, and no missing health with `healing_not_needed`.

- [ ] **Step 4: Verify task**

Run Unity EditMode tests.

Expected: all `PlayerStateMachineTests` pass and existing Phase 05 damage tests still pass.

Run: `git diff --check`

Expected: clean.

- [ ] **Step 5: Commit task**

```bash
git add Assets/Scripts/Gameplay/Combat/BodyZone.cs Assets/Scripts/Gameplay/Combat/DamageEvent.cs Assets/Scripts/Gameplay/Combat/HealingEvent.cs Assets/Scripts/Gameplay/Combat/BodyZoneDamageTable.cs Assets/Scripts/Gameplay/PlayerStateMachine.cs Assets/Tests/EditMode/PlayerStateMachineTests.cs
git commit -m "feat: add typed damage and healing"
```

---

### Task 4: Baseline Hitscan Weapon And Server Raycast Resolution

**Files:**
- Create: `Assets/Scripts/Gameplay/Combat/WeaponDefinition.cs`
- Create: `Assets/Scripts/Gameplay/Combat/WeaponRuntimeState.cs`
- Create: `Assets/Scripts/Gameplay/Combat/WeaponFireRequest.cs`
- Create: `Assets/Scripts/Gameplay/Combat/WeaponFireResult.cs`
- Create: `Assets/Scripts/Gameplay/Combat/WeaponRuntime.cs`
- Create: `Assets/Scripts/Gameplay/Combat/BodyZoneHitbox.cs`
- Create: `Assets/Scripts/Gameplay/Combat/ServerRaycastWeaponResolver.cs`
- Test: `Assets/Tests/EditMode/WeaponRuntimeTests.cs`
- Test: `Assets/Tests/EditMode/ServerRaycastWeaponResolverTests.cs`

**Interfaces:**
- Consumes: `DamageEvent`, `BodyZone`, `BodyZoneDamageTable`
- Produces: `WeaponDefinition.BaselineRifle() : WeaponDefinition`
- Produces: `WeaponRuntime.TryFire(WeaponFireRequest request, double serverTimeSeconds) : WeaponFireResult`
- Produces: `WeaponRuntime.TryReload(Guid requestId, double serverTimeSeconds) : WeaponFireResult`
- Produces: `BodyZoneHitbox.PlayerId : string`
- Produces: `BodyZoneHitbox.Zone : BodyZone`
- Produces: `ServerRaycastWeaponResolver.TryResolve(Vector3 origin, Vector3 direction, float range, LayerMask mask, out Guid targetPlayerId, out BodyZone bodyZone) : bool`

- [ ] **Step 1: Write failing weapon runtime tests**

Create `WeaponRuntimeTests.cs`:

```csharp
using System;
using LH.Main.Unity.Gameplay.Combat;
using NUnit.Framework;

public sealed class WeaponRuntimeTests
{
    [Test]
    public void TryFireConsumesAmmoAndRejectsCooldown()
    {
        var weapon = new WeaponRuntime(WeaponDefinition.BaselineRifle());

        WeaponFireResult first = weapon.TryFire(new WeaponFireRequest(Guid.Parse("11111111-1111-1111-1111-111111111111")), 1.0d);
        WeaponFireResult second = weapon.TryFire(new WeaponFireRequest(Guid.Parse("22222222-2222-2222-2222-222222222222")), 1.01d);

        Assert.That(first.Accepted, Is.True);
        Assert.That(first.AmmoInMagazine, Is.EqualTo(29));
        Assert.That(second.Accepted, Is.False);
        Assert.That(second.Reason, Is.EqualTo("weapon_cooldown"));
    }

    [Test]
    public void TryReloadMovesReserveAmmoIntoMagazine()
    {
        var definition = new WeaponDefinition("baseline_rifle", 30, 90, 0.1d, 2.0d, 100f, 30);
        var weapon = new WeaponRuntime(definition);
        weapon.TryFire(new WeaponFireRequest(Guid.Parse("11111111-1111-1111-1111-111111111111")), 1.0d);

        WeaponFireResult reload = weapon.TryReload(Guid.Parse("22222222-2222-2222-2222-222222222222"), 3.0d);

        Assert.That(reload.Accepted, Is.True);
        Assert.That(reload.AmmoInMagazine, Is.EqualTo(30));
        Assert.That(reload.ReserveAmmo, Is.EqualTo(89));
    }

    [Test]
    public void DuplicateFireRequestDoesNotConsumeAmmoTwice()
    {
        var weapon = new WeaponRuntime(WeaponDefinition.BaselineRifle());
        var request = new WeaponFireRequest(Guid.Parse("11111111-1111-1111-1111-111111111111"));

        WeaponFireResult first = weapon.TryFire(request, 1.0d);
        WeaponFireResult duplicate = weapon.TryFire(request, 2.0d);

        Assert.That(first.Accepted, Is.True);
        Assert.That(duplicate.Accepted, Is.False);
        Assert.That(duplicate.Reason, Is.EqualTo("fire_duplicate"));
        Assert.That(weapon.State.AmmoInMagazine, Is.EqualTo(29));
    }
}
```

- [ ] **Step 2: Write failing raycast resolver tests**

Create `ServerRaycastWeaponResolverTests.cs` with a temporary cube collider and `BodyZoneHitbox` component. The test should fire a ray through the collider and assert returned player id/body zone.

```csharp
[Test]
public void TryResolveReturnsBodyZoneFromHitCollider()
{
    var target = GameObject.CreatePrimitive(PrimitiveType.Cube);
    try
    {
        var hitbox = target.AddComponent<BodyZoneHitbox>();
        hitbox.ConfigureForTest(Guid.Parse("11111111-1111-1111-1111-111111111111"), BodyZone.Head);

        bool hit = ServerRaycastWeaponResolver.TryResolve(new Vector3(0f, 0f, -5f), Vector3.forward, 20f, Physics.DefaultRaycastLayers, out Guid playerId, out BodyZone zone);

        Assert.That(hit, Is.True);
        Assert.That(playerId, Is.EqualTo(Guid.Parse("11111111-1111-1111-1111-111111111111")));
        Assert.That(zone, Is.EqualTo(BodyZone.Head));
    }
    finally
    {
        UnityEngine.Object.DestroyImmediate(target);
    }
}
```

- [ ] **Step 3: Run tests and verify failure**

Run Unity EditMode tests with results path `.superpowers/sdd/2026-09-21-phase-06-core-match/task-4-editmode.xml`.

Expected: FAIL because weapon and raycast types do not exist.

- [ ] **Step 4: Implement weapon runtime and raycast resolver**

Implement baseline rifle defaults: magazine `30`, reserve `90`, fire cooldown `0.1`, reload seconds `2.0`, max range `100`, base damage `30`. `TryFire` rejects `fire_duplicate`, `weapon_cooldown`, `weapon_reloading`, and `ammo_empty`. `TryReload` rejects `reload_duplicate`, `reload_not_needed`, and `reserve_empty`.

Implement `BodyZoneHitbox : MonoBehaviour` with serialized player id string and body zone. Add `ConfigureForTest(Guid, BodyZone)` for tests. `ServerRaycastWeaponResolver.TryResolve` normalizes direction, rejects zero direction, uses `Physics.Raycast`, and returns the first hitbox found on the hit collider or parent.

- [ ] **Step 5: Verify task**

Run Unity EditMode tests.

Expected: weapon runtime and raycast resolver tests pass.

Run: `git diff --check`

Expected: clean.

- [ ] **Step 6: Commit task**

```bash
git add Assets/Scripts/Gameplay/Combat Assets/Tests/EditMode/WeaponRuntimeTests.cs Assets/Tests/EditMode/ServerRaycastWeaponResolverTests.cs
git commit -m "feat: add baseline hitscan weapon rules"
```

---

### Task 5: Grenade Runtime And Explosion Damage

**Files:**
- Create: `Assets/Scripts/Gameplay/Combat/GrenadeDefinition.cs`
- Create: `Assets/Scripts/Gameplay/Combat/GrenadeThrowRequest.cs`
- Create: `Assets/Scripts/Gameplay/Combat/GrenadeExplosionResult.cs`
- Create: `Assets/Scripts/Gameplay/Combat/GrenadeRuntime.cs`
- Test: `Assets/Tests/EditMode/GrenadeRuntimeTests.cs`

**Interfaces:**
- Consumes: `DamageEvent`, `BodyZone.Torso`
- Produces: `GrenadeDefinition.StandardFrag() : GrenadeDefinition`
- Produces: `GrenadeRuntime.Tick(double serverTimeSeconds, IReadOnlyList<GrenadeTarget> targets) : GrenadeExplosionResult`
- Produces: `GrenadeRuntime.HasExploded : bool`
- Produces: `GrenadeExplosionResult.DamageEvents : IReadOnlyList<DamageEvent>`

- [ ] **Step 1: Write failing grenade tests**

Create `GrenadeRuntimeTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using LH.Main.Unity.Gameplay.Combat;
using NUnit.Framework;
using UnityEngine;

public sealed class GrenadeRuntimeTests
{
    [Test]
    public void TickExplodesOnceAfterFuse()
    {
        var grenade = new GrenadeRuntime(Guid.Parse("11111111-1111-1111-1111-111111111111"), GrenadeDefinition.StandardFrag(), Vector3.zero, 0.0d);
        var targets = new[] { new GrenadeTarget(Guid.Parse("22222222-2222-2222-2222-222222222222"), new Vector3(1f, 0f, 0f)) };

        GrenadeExplosionResult before = grenade.Tick(1.0d, targets);
        GrenadeExplosionResult first = grenade.Tick(3.1d, targets);
        GrenadeExplosionResult duplicate = grenade.Tick(4.0d, targets);

        Assert.That(before.Exploded, Is.False);
        Assert.That(first.Exploded, Is.True);
        Assert.That(first.DamageEvents.Count, Is.EqualTo(1));
        Assert.That(duplicate.Exploded, Is.False);
    }

    [Test]
    public void ExplosionDamageFallsOffWithDistance()
    {
        var grenade = new GrenadeRuntime(Guid.Parse("11111111-1111-1111-1111-111111111111"), new GrenadeDefinition(3.0d, 10f, 100), Vector3.zero, 0.0d);
        var near = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var far = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var targets = new[] { new GrenadeTarget(near, new Vector3(1f, 0f, 0f)), new GrenadeTarget(far, new Vector3(9f, 0f, 0f)) };

        GrenadeExplosionResult result = grenade.Tick(3.1d, targets);

        Assert.That(result.DamageEvents[0].BaseAmount, Is.GreaterThan(result.DamageEvents[1].BaseAmount));
    }
}
```

- [ ] **Step 2: Run tests and verify failure**

Run Unity EditMode tests with results path `.superpowers/sdd/2026-09-21-phase-06-core-match/task-5-editmode.xml`.

Expected: FAIL because grenade runtime types do not exist.

- [ ] **Step 3: Implement grenade runtime**

Create `GrenadeTarget(Guid playerId, Vector3 position)` in the same file or a focused file. `GrenadeRuntime` stores grenade id, spawn position, spawn time, definition, and exploded flag. `Tick` returns no mutation before fuse time, then creates torso `DamageEvent`s for targets within radius. Damage formula: `ceil(maxDamage * (1 - distance / radius))`, clamped to at least `1` for targets inside radius. After explosion, later ticks return `Exploded = false` with an empty damage list.

- [ ] **Step 4: Verify task**

Run Unity EditMode tests.

Expected: grenade tests pass.

Run: `git diff --check`

Expected: clean.

- [ ] **Step 5: Commit task**

```bash
git add Assets/Scripts/Gameplay/Combat Assets/Tests/EditMode/GrenadeRuntimeTests.cs
git commit -m "feat: add server grenade runtime"
```

---

### Task 6: Effects Runtime And Instant Med Items

**Files:**
- Create: `Assets/Scripts/Gameplay/Effects/EffectKind.cs`
- Create: `Assets/Scripts/Gameplay/Effects/TimedEffect.cs`
- Create: `Assets/Scripts/Gameplay/Effects/EffectRuntime.cs`
- Test: `Assets/Tests/EditMode/EffectRuntimeTests.cs`

**Interfaces:**
- Consumes: `PlayerInventory.TryRemove(...)`, `PlayerStateMachine.ApplyHealing(...)`, `HealingEvent`
- Produces: `EffectRuntime.TryUseMedItem(Guid playerId, PlayerInventory inventory, PlayerStateMachine state, string itemId, int healAmount, double serverTimeSeconds, Guid transactionId) : InventoryTransactionResult`
- Produces: `EffectRuntime.AddDamageOverTime(Guid playerId, int damagePerTick, double tickIntervalSeconds, double expiresAtSeconds) : void`
- Produces: `EffectRuntime.Tick(double serverTimeSeconds, Func<Guid, int, string, Guid, bool> applyDamage) : int`

- [ ] **Step 1: Write failing effects tests**

Create `EffectRuntimeTests.cs`:

```csharp
using System;
using LH.Main.Unity.Gameplay;
using LH.Main.Unity.Gameplay.Effects;
using LH.Main.Unity.Gameplay.Items;
using NUnit.Framework;

public sealed class EffectRuntimeTests
{
    [Test]
    public void TryUseMedItemConsumesItemAndHeals()
    {
        var playerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var inventory = new PlayerInventory(2);
        inventory.TryAdd(new ItemDefinition("med_small", ItemCategory.MedItem, 1, true), 1, Guid.Parse("22222222-2222-2222-2222-222222222222"));
        var state = PlayerStateMachine.Create(playerId);
        state.ApplyDamage(new TechnicalDamageEvent(Guid.Parse("33333333-3333-3333-3333-333333333333"), null, playerId, 50, "test"));
        var effects = new EffectRuntime(5.0d);

        InventoryTransactionResult result = effects.TryUseMedItem(playerId, inventory, state, "med_small", 25, 10.0d, Guid.Parse("44444444-4444-4444-4444-444444444444"));

        Assert.That(result.Accepted, Is.True);
        Assert.That(state.Health, Is.EqualTo(75));
        Assert.That(inventory.GetSlots()[0].IsEmpty, Is.True);
    }

    [Test]
    public void TryUseMedItemRejectsCooldown()
    {
        var playerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var inventory = new PlayerInventory(2);
        var med = new ItemDefinition("med_small", ItemCategory.MedItem, 2, true);
        inventory.TryAdd(med, 2, Guid.Parse("22222222-2222-2222-2222-222222222222"));
        var state = PlayerStateMachine.Create(playerId);
        state.ApplyDamage(new TechnicalDamageEvent(Guid.Parse("33333333-3333-3333-3333-333333333333"), null, playerId, 80, "test"));
        var effects = new EffectRuntime(5.0d);
        effects.TryUseMedItem(playerId, inventory, state, "med_small", 10, 10.0d, Guid.Parse("44444444-4444-4444-4444-444444444444"));

        InventoryTransactionResult result = effects.TryUseMedItem(playerId, inventory, state, "med_small", 10, 11.0d, Guid.Parse("55555555-5555-5555-5555-555555555555"));

        Assert.That(result.Accepted, Is.False);
        Assert.That(result.Reason, Is.EqualTo("med_cooldown"));
    }
}
```

- [ ] **Step 2: Run tests and verify failure**

Run Unity EditMode tests with results path `.superpowers/sdd/2026-09-21-phase-06-core-match/task-6-editmode.xml`.

Expected: FAIL because effects runtime does not exist.

- [ ] **Step 3: Implement effects runtime**

`TryUseMedItem` rejects `med_cooldown` before consuming inventory, removes one med item with `PlayerInventory.TryRemove`, applies `HealingEvent`, and returns the healing rejection reason if healing fails. If healing fails after removal, restore the item with a new internal transaction id so med use remains atomic from the player perspective.

`Tick` applies active damage-over-time effects when due and returns the count of applied ticks. Use stable effect ids internally so expired effects are removed after `expiresAtSeconds`.

- [ ] **Step 4: Verify task**

Run Unity EditMode tests.

Expected: effects, inventory, and state machine tests pass.

Run: `git diff --check`

Expected: clean.

- [ ] **Step 5: Commit task**

```bash
git add Assets/Scripts/Gameplay/Effects Assets/Tests/EditMode/EffectRuntimeTests.cs
git commit -m "feat: add runtime effects and med use"
```

---

### Task 7: Scene-Authored Shrinking Zone

**Files:**
- Create: `Assets/Scripts/Gameplay/Zone/ZonePhase.cs`
- Create: `Assets/Scripts/Gameplay/Zone/ZoneRuntime.cs`
- Create: `Assets/Scripts/Gameplay/Zone/ZoneVolume.cs`
- Test: `Assets/Tests/EditMode/ZoneRuntimeTests.cs`
- Test: `Assets/Tests/EditMode/ZoneVolumeTests.cs`

**Interfaces:**
- Produces: `ZonePhase(double startsAtSeconds, double endsAtSeconds, int damagePerTick, double tickIntervalSeconds)`
- Produces: `ZoneRuntime(IReadOnlyList<ZonePhase> phases)`
- Produces: `ZoneRuntime.GetActivePhase(double elapsedSeconds) : ZonePhase`
- Produces: `ZoneRuntime.ShouldApplyDamage(Guid playerId, bool isInsideSafeVolume, double elapsedSeconds) : bool`
- Produces: `ZoneVolume.Contains(Vector3 worldPosition) : bool`

- [ ] **Step 1: Write failing zone runtime tests**

Create `ZoneRuntimeTests.cs`:

```csharp
using System;
using LH.Main.Unity.Gameplay.Zone;
using NUnit.Framework;

public sealed class ZoneRuntimeTests
{
    [Test]
    public void GetActivePhaseReturnsPhaseForElapsedTime()
    {
        var runtime = new ZoneRuntime(new[] { new ZonePhase(0d, 10d, 1, 2d), new ZonePhase(10d, 20d, 5, 1d) });

        ZonePhase phase = runtime.GetActivePhase(12d);

        Assert.That(phase.DamagePerTick, Is.EqualTo(5));
    }

    [Test]
    public void ShouldApplyDamageTicksOnlyOutsideSafeVolume()
    {
        var playerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var runtime = new ZoneRuntime(new[] { new ZonePhase(0d, 10d, 1, 2d) });

        bool first = runtime.ShouldApplyDamage(playerId, false, 2d);
        bool inside = runtime.ShouldApplyDamage(playerId, true, 4d);
        bool tooSoon = runtime.ShouldApplyDamage(playerId, false, 5d);
        bool next = runtime.ShouldApplyDamage(playerId, false, 6d);

        Assert.That(first, Is.True);
        Assert.That(inside, Is.False);
        Assert.That(tooSoon, Is.False);
        Assert.That(next, Is.True);
    }
}
```

- [ ] **Step 2: Write failing zone volume test**

Create `ZoneVolumeTests.cs` with a `BoxCollider` trigger and `ZoneVolume.Contains` assertions for inside/outside points.

- [ ] **Step 3: Run tests and verify failure**

Run Unity EditMode tests with results path `.superpowers/sdd/2026-09-21-phase-06-core-match/task-7-editmode.xml`.

Expected: FAIL because zone runtime types do not exist.

- [ ] **Step 4: Implement zone runtime and volume**

`ZoneRuntime` sorts phases by start time, rejects an empty phase list in the constructor, and stores last damage tick per player. `ShouldApplyDamage` returns false inside safe volume, outside active phases, or before the player's next tick time.

`ZoneVolume : MonoBehaviour` requires a collider reference or discovers one on the same object. `Contains` uses collider bounds for the first implementation. If the collider is missing, return false and log a warning once.

- [ ] **Step 5: Verify task**

Run Unity EditMode tests.

Expected: zone tests pass.

Run: `git diff --check`

Expected: clean.

- [ ] **Step 6: Commit task**

```bash
git add Assets/Scripts/Gameplay/Zone Assets/Tests/EditMode/ZoneRuntimeTests.cs Assets/Tests/EditMode/ZoneVolumeTests.cs
git commit -m "feat: add shrinking zone runtime"
```

---

### Task 8: Core Match Runtime, FishNet Intent Adapters, And Metrics

**Files:**
- Create: `Assets/Scripts/Gameplay/CoreMatchRuntime.cs`
- Modify: `Assets/Scripts/Networking/NetworkPlayerController.cs`
- Modify: `Assets/Scripts/Networking/GameServerMetrics.cs`
- Modify: `Assets/Scripts/Server/GameServerStatus.cs`
- Modify: `Assets/Scripts/Server/GameServerBootstrap.cs`
- Test: `Assets/Tests/EditMode/NetworkPlayerControllerTests.cs`
- Test: `Assets/Tests/EditMode/GameServerStatusTests.cs`

**Interfaces:**
- Consumes: inventory, loot, damage, weapon, grenade, effects, and zone interfaces from Tasks 1-7.
- Produces: `CoreMatchRuntime.RegisterPlayer(Guid playerId) : void`
- Produces: `CoreMatchRuntime.TryPickup(Guid playerId, Guid lootId, Guid transactionId, Vector3 playerPosition) : InventoryTransactionResult`
- Produces: `CoreMatchRuntime.TryUseMed(Guid playerId, string itemId, Guid transactionId, double serverTimeSeconds) : InventoryTransactionResult`
- Produces: `CoreMatchRuntime.TryFire(Guid playerId, WeaponFireRequest request, Vector3 origin, Vector3 direction, double serverTimeSeconds) : WeaponFireResult`
- Produces: `CoreMatchRuntime.TryThrowGrenade(Guid playerId, Guid transactionId, Vector3 origin, Vector3 direction, double serverTimeSeconds) : InventoryTransactionResult`
- Produces: `CoreMatchRuntime.Tick(double serverTimeSeconds) : void`
- Modifies: `NetworkPlayerController.ServerPickupLoot(Guid lootId, Guid transactionId)`
- Modifies: `NetworkPlayerController.ServerUseMed(string itemId, Guid transactionId)`
- Modifies: `NetworkPlayerController.ServerFireWeapon(Guid requestId, Vector3 origin, Vector3 direction)`
- Modifies: `NetworkPlayerController.ServerReloadWeapon(Guid requestId)`
- Modifies: `NetworkPlayerController.ServerThrowGrenade(Guid transactionId, Vector3 origin, Vector3 direction)`

- [ ] **Step 1: Write failing metrics/status tests**

Extend `GameServerStatusTests.cs` to assert JSON includes Phase 06 fields: `acceptedPickupAttempts`, `rejectedPickupAttempts`, `duplicateLootPickups`, `acceptedFireRequests`, `rejectedFireRequests`, `grenadesExploded`, `zoneDamageTicks`, and `medItemsUsed`.

```csharp
[Test]
public void StatusJsonIncludesPhase06Metrics()
{
    GameServerMetrics.RecordPickupAccepted();
    GameServerMetrics.RecordFireRejected("test");
    var status = new GameServerStatus("server-1", GameServerState.Idle, 8081, "localhost", 7771, DateTime.UtcNow, 1, 1, 0, 0, 0, 0, 60, 1.2d, 64, 1d, 1d, 0, 0, 0, 0, 0, 0, 0);

    string json = status.ToJson();

    Assert.That(json, Does.Contain("acceptedPickupAttempts"));
    Assert.That(json, Does.Contain("rejectedFireRequests"));
}
```

- [ ] **Step 2: Write failing NetworkPlayerController adapter tests**

Extend `NetworkPlayerControllerTests.cs` to assert new intent methods have `[ServerRpc]` and terminal players cannot fire/use/pickup through test-facing server helper methods. Add tests that require these exact helper signatures on `NetworkPlayerController`: `ApplyServerPickupForTest(CoreMatchRuntime runtime, Guid lootId, Guid transactionId)`, `ApplyServerUseMedForTest(CoreMatchRuntime runtime, string itemId, Guid transactionId)`, and `ApplyServerFireForTest(CoreMatchRuntime runtime, Guid requestId, Vector3 origin, Vector3 direction)`. Each helper must have `[Server]` and must return the same result type as the underlying runtime call.

- [ ] **Step 3: Run tests and verify failure**

Run Unity EditMode tests with results path `.superpowers/sdd/2026-09-21-phase-06-core-match/task-8-editmode.xml`.

Expected: FAIL because runtime, adapters, and metrics fields do not exist.

- [ ] **Step 4: Implement metrics and status fields**

Add counters to `GameServerMetrics`: accepted/rejected pickups, duplicate loot pickups, inventory-full rejections, accepted/rejected fire requests, hitscan hits, hitscan misses, grenades thrown, grenades exploded, zone damage ticks, med items used, med items rejected. Extend `Snapshot`, `GameServerBootstrap.CreateStatus`, `GameServerStatus`, and `GameServerStatus.ToJson` with these fields.

- [ ] **Step 5: Implement `CoreMatchRuntime`**

Compose per-player `PlayerInventory`, `PlayerStateMachine`, `WeaponRuntime`, active grenades, loot containers, effects, and zone runtime. Start with technical defaults so tests/load runner can operate without production content: 8 inventory slots, one baseline rifle, 30 ammo magazine, 90 reserve ammo, one med item, one grenade, and one value item spawn.

Every public method must check registered player and terminal life state before mutation. Return stable rejection reasons: `player_not_registered`, `state_terminal`, `loot_not_found`, `item_not_owned`, `fire_rejected`, `grenade_rejected`, or subsystem-specific reason.

- [ ] **Step 6: Implement FishNet intent methods**

Add `[ServerRpc]` methods to `NetworkPlayerController` that derive the authoritative player id from registry/runtime context rather than trusting a client-provided id. For tests, allow injecting a `CoreMatchRuntime` reference through a server-only configure method. Do not replicate final UI; only expose accepted result state enough for load runner and metrics.

- [ ] **Step 7: Wire runtime in bootstrap tick**

In `GameServerBootstrap`, initialize one `CoreMatchRuntime` during server startup, register players through existing registry integration, and call `CoreMatchRuntime.Tick(Time.realtimeSinceStartupAsDouble)` from `OnPostTick` after existing tick metrics logic.

- [ ] **Step 8: Verify task**

Run Unity EditMode tests.

Expected: network adapter, status, and all previous gameplay tests pass.

Run: `dotnet test server\LH.Main.Server.sln --configuration Release`

Expected: contracts and backend API tests pass; no backend persistence changes were added.

Run: `git diff --check`

Expected: clean.

- [ ] **Step 9: Commit task**

```bash
git add Assets/Scripts/Gameplay/CoreMatchRuntime.cs Assets/Scripts/Networking/NetworkPlayerController.cs Assets/Scripts/Networking/GameServerMetrics.cs Assets/Scripts/Server/GameServerStatus.cs Assets/Scripts/Server/GameServerBootstrap.cs Assets/Tests/EditMode/NetworkPlayerControllerTests.cs Assets/Tests/EditMode/GameServerStatusTests.cs
git commit -m "feat: wire core match runtime"
```

---

### Task 9: Phase 06 Load Runner And Final Verification

**Files:**
- Modify: `Assets/Scripts/Load/HeadlessMatchBot.cs`
- Modify: `Assets/Scripts/Load/LoadScenarioReport.cs`
- Modify: `Assets/Scripts/Editor/NetworkedCoreLoadRunner.cs`
- Test: `Assets/Tests/EditMode/HeadlessMatchBotTests.cs`
- Test: `Assets/Tests/EditMode/LoadScenarioReportTests.cs`
- Test: `Assets/Tests/EditMode/NetworkedCoreLoadRunnerTests.cs`
- Create: `.superpowers/sdd/2026-09-21-phase-06-core-match/task-9-final-report.md`

**Interfaces:**
- Consumes: `NetworkPlayerController.ServerPickupLoot`, `ServerFireWeapon`, `ServerReloadWeapon`, `ServerThrowGrenade`, `ServerUseMed`
- Modifies: `BotScenario.Phase06CoreMatch : bool`
- Modifies: `BotResult.PickedUpLoot`, `FiredWeapon`, `ReloadedWeapon`, `ThrewGrenade`, `UsedMedItem`, `TookZoneDamage`
- Modifies: `LoadScenarioReport` Phase 06 counters and JSON output
- Modifies: `NetworkedCoreLoadRunner` command-line flag `--phase06CoreMatch true`

- [ ] **Step 1: Write failing bot scheduling tests**

Extend `HeadlessMatchBotTests.cs` to assert deterministic Phase 06 action windows. Use helper methods instead of waiting real time.

```csharp
[Test]
public void GetPhase06ActionForElapsedSecondsCoversCoreSystems()
{
    Assert.That(HeadlessMatchBot.GetPhase06ActionForElapsedSeconds(1d), Is.EqualTo(BotPhase06Action.PickupLoot));
    Assert.That(HeadlessMatchBot.GetPhase06ActionForElapsedSeconds(3d), Is.EqualTo(BotPhase06Action.FireWeapon));
    Assert.That(HeadlessMatchBot.GetPhase06ActionForElapsedSeconds(5d), Is.EqualTo(BotPhase06Action.ReloadWeapon));
    Assert.That(HeadlessMatchBot.GetPhase06ActionForElapsedSeconds(7d), Is.EqualTo(BotPhase06Action.ThrowGrenade));
    Assert.That(HeadlessMatchBot.GetPhase06ActionForElapsedSeconds(9d), Is.EqualTo(BotPhase06Action.UseMedItem));
}
```

- [ ] **Step 2: Write failing report JSON tests**

Extend `LoadScenarioReportTests.cs`:

```csharp
[Test]
public void ToJsonIncludesPhase06Counters()
{
    var report = new LoadScenarioReport(DateTime.UtcNow, 30, 64)
    {
        LootPickups = 64,
        DuplicateLootPrevented = 12,
        FireRequests = 64,
        GrenadesExploded = 64,
        ZoneDamageTicks = 10,
        MedItemsUsed = 32
    };

    string json = report.ToJson();

    Assert.That(json, Does.Contain("\"lootPickups\":64"));
    Assert.That(json, Does.Contain("\"duplicateLootPrevented\":12"));
    Assert.That(json, Does.Contain("\"grenadesExploded\":64"));
}
```

- [ ] **Step 3: Write failing load-runner criteria tests**

Extend `NetworkedCoreLoadRunnerTests.cs` to assert Phase 06 success requires all clients completed, at least one duplicate loot prevention event, at least one accepted fire request, at least one grenade explosion, at least one zone damage tick, and no duplicate loot success.

- [ ] **Step 4: Run tests and verify failure**

Run Unity EditMode tests with results path `.superpowers/sdd/2026-09-21-phase-06-core-match/task-9-editmode.xml`.

Expected: FAIL because Phase 06 bot/report/load-runner fields do not exist.

- [ ] **Step 5: Implement Phase 06 bot behavior**

Add `BotPhase06Action` enum with values `Move`, `PickupLoot`, `FireWeapon`, `ReloadWeapon`, `ThrowGrenade`, `UseMedItem`. In `HeadlessMatchBot.RunAsync`, when `Phase06CoreMatch` is true, keep movement running and invoke the matching `NetworkPlayerController` server intent methods in deterministic windows. Set `BotResult` flags only after local invocation succeeds or an accepted replicated state is observed.

- [ ] **Step 6: Implement report counters and success criteria**

Add Phase 06 counters to `LoadScenarioReport.ToJson`. Add command-line option parsing in `LoadRunnerOptions`: `--phase06CoreMatch true`, default report path `.superpowers/sdd/2026-09-21-phase-06-core-match/task-9-final-load-64.json`. Update `ShouldExitSuccessfully` so Phase 06 requires all clients completed, no duplicate-loot success, and non-zero coverage for pickup, fire, grenade, med, and zone counters.

- [ ] **Step 7: Run fast verification**

Run Unity EditMode tests with results path `.superpowers/sdd/2026-09-21-phase-06-core-match/task-9-editmode.xml`.

Expected: all EditMode tests pass.

Run: `dotnet test server\LH.Main.Server.sln --configuration Release`

Expected: contracts and backend API tests pass.

- [ ] **Step 8: Run 64-client Phase 06 load scenario**

Restart the local backend and game servers from the Phase 06 worktree:

```powershell
docker compose --env-file .env.example --profile game-servers down --volumes
docker compose --env-file .env.example --profile game-servers up --build --detach --wait backend-api game-server-1 game-server-2
```

Then run the Unity load runner with Phase 06 enabled:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.14f1\Editor\Unity.exe" -batchmode -projectPath . -executeMethod LH.Main.Unity.Editor.NetworkedCoreLoadRunner.Run -phase06CoreMatch true -clients 64 -durationSeconds 60 -reportPath .superpowers/sdd/2026-09-21-phase-06-core-match/task-9-final-load-64.json
```

Expected: Unity exits `0`, report has `targetClients:64`, `completedClients:64`, non-zero pickup/fire/grenade/med/zone counters, and duplicate loot prevention evidence without duplicate loot success.

- [ ] **Step 9: Write final report**

Create `.superpowers/sdd/2026-09-21-phase-06-core-match/task-9-final-report.md` with exact command lines, result XML/report paths, pass counts, and any accepted caveats. Include the backend test result and Unity EditMode result path.

- [ ] **Step 10: Commit task**

```bash
git add Assets/Scripts/Load/HeadlessMatchBot.cs Assets/Scripts/Load/LoadScenarioReport.cs Assets/Scripts/Editor/NetworkedCoreLoadRunner.cs Assets/Tests/EditMode/HeadlessMatchBotTests.cs Assets/Tests/EditMode/LoadScenarioReportTests.cs Assets/Tests/EditMode/NetworkedCoreLoadRunnerTests.cs .superpowers/sdd/2026-09-21-phase-06-core-match
git commit -m "feat: add phase 06 load scenario"
```

---

## Final Verification

After Task 9, run these commands before claiming Phase 06 complete:

```powershell
dotnet test server\LH.Main.Server.sln --configuration Release
```

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.14f1\Editor\Unity.exe" -batchmode -projectPath . -runTests -testPlatform EditMode -testResults .superpowers/sdd/2026-09-21-phase-06-core-match/final-editmode.xml -quit
```

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.14f1\Editor\Unity.exe" -batchmode -projectPath . -executeMethod LH.Main.Unity.Editor.NetworkedCoreLoadRunner.Run -phase06CoreMatch true -clients 64 -durationSeconds 60 -reportPath .superpowers/sdd/2026-09-21-phase-06-core-match/final-load-64.json
```

Expected final state:

- Backend tests pass.
- Unity EditMode tests pass.
- Phase 06 load report shows 64 completed clients.
- Load report shows no duplicate loot success and includes duplicate prevention evidence.
- Load report includes weapon fire, reload/ammo consumption, grenade explosion, med use, and zone damage coverage.
- `git status --short` shows only intended tracked artifacts.
