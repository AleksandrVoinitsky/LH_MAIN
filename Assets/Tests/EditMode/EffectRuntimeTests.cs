using System;
using System.Collections.Generic;
using LH.Main.Unity.Gameplay;
using LH.Main.Unity.Gameplay.Effects;
using LH.Main.Unity.Gameplay.Items;
using NUnit.Framework;

public sealed class EffectRuntimeTests
{
    [Test]
    public void TryUseMedItemConsumesOneItemAndAppliesHealing()
    {
        var playerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var inventory = CreateInventoryWithMeds(2);
        var state = PlayerStateMachine.Create(playerId);
        state.ApplyDamage(new TechnicalDamageEvent(Guid.NewGuid(), null, playerId, 40, "technical"));
        var runtime = new EffectRuntime();

        InventoryTransactionResult result = runtime.TryUseMedItem(
            playerId,
            inventory,
            state,
            MedDefinition(),
            25,
            10d,
            Guid.Parse("22222222-2222-2222-2222-222222222222"));

        Assert.That(result.Accepted, Is.True);
        Assert.That(state.DamageTaken, Is.EqualTo(15));
        Assert.That(GetQuantity(inventory, "med_small"), Is.EqualTo(1));
    }

    [Test]
    public void TryUseMedItemStringApiUsesServerKnownMedDefinition()
    {
        var playerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var inventory = CreateInventoryWithMeds(2);
        var state = PlayerStateMachine.Create(playerId);
        state.ApplyDamage(new TechnicalDamageEvent(Guid.NewGuid(), null, playerId, 40, "technical"));
        var runtime = new EffectRuntime();
        runtime.RegisterItemDefinition(MedDefinition());

        InventoryTransactionResult result = runtime.TryUseMedItem(playerId, inventory, state, "med_small", 25, 11d, Guid.NewGuid());

        Assert.That(result.Accepted, Is.True);
        Assert.That(state.DamageTaken, Is.EqualTo(15));
        Assert.That(GetQuantity(inventory, "med_small"), Is.EqualTo(1));
    }

    [Test]
    public void TryUseMedItemRejectsCooldownBeforeConsumingInventory()
    {
        var playerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var inventory = CreateInventoryWithMeds(2);
        var state = PlayerStateMachine.Create(playerId);
        state.ApplyDamage(new TechnicalDamageEvent(Guid.NewGuid(), null, playerId, 80, "technical"));
        var runtime = new EffectRuntime();

        runtime.TryUseMedItem(playerId, inventory, state, MedDefinition(), 20, 20d, Guid.NewGuid());
        InventoryTransactionResult result = runtime.TryUseMedItem(playerId, inventory, state, MedDefinition(), 20, 20.5d, Guid.NewGuid());

        Assert.That(result.Accepted, Is.False);
        Assert.That(result.Reason, Is.EqualTo("med_cooldown"));
        Assert.That(state.DamageTaken, Is.EqualTo(60));
        Assert.That(GetQuantity(inventory, "med_small"), Is.EqualTo(1));
    }

    [Test]
    public void TryUseMedItemRestoresInventoryWhenHealingIsRejected()
    {
        var playerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var inventory = CreateInventoryWithMeds(1);
        var state = PlayerStateMachine.Create(playerId);
        var runtime = new EffectRuntime();

        InventoryTransactionResult result = runtime.TryUseMedItem(playerId, inventory, state, MedDefinition(), 20, 30d, Guid.NewGuid());

        Assert.That(result.Accepted, Is.False);
        Assert.That(result.Reason, Is.EqualTo("healing_not_needed"));
        Assert.That(GetQuantity(inventory, "med_small"), Is.EqualTo(1));
    }

    [Test]
    public void TryUseMedItemRejectsNonMedDefinitionBeforeConsumingInventory()
    {
        var playerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var inventory = CreateInventoryWithItem(new ItemDefinition("ammo_9mm", ItemCategory.Ammo, 30, false), 1);
        var state = PlayerStateMachine.Create(playerId);
        state.ApplyDamage(new TechnicalDamageEvent(Guid.NewGuid(), null, playerId, 40, "technical"));
        var runtime = new EffectRuntime();

        InventoryTransactionResult result = runtime.TryUseMedItem(playerId, inventory, state, new ItemDefinition("ammo_9mm", ItemCategory.Ammo, 30, false), 20, 40d, Guid.NewGuid());

        Assert.That(result.Accepted, Is.False);
        Assert.That(result.Reason, Is.EqualTo("med_item_invalid"));
        Assert.That(state.DamageTaken, Is.EqualTo(40));
        Assert.That(GetQuantity(inventory, "ammo_9mm"), Is.EqualTo(1));
    }

    [Test]
    public void TryUseMedItemRejectsUnusableMedDefinitionBeforeConsumingInventory()
    {
        var playerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var unusableMed = new ItemDefinition("med_small", ItemCategory.MedItem, 5, false);
        var inventory = CreateInventoryWithItem(unusableMed, 1);
        var state = PlayerStateMachine.Create(playerId);
        state.ApplyDamage(new TechnicalDamageEvent(Guid.NewGuid(), null, playerId, 40, "technical"));
        var runtime = new EffectRuntime();

        InventoryTransactionResult result = runtime.TryUseMedItem(playerId, inventory, state, unusableMed, 20, 50d, Guid.NewGuid());

        Assert.That(result.Accepted, Is.False);
        Assert.That(result.Reason, Is.EqualTo("med_item_invalid"));
        Assert.That(state.DamageTaken, Is.EqualTo(40));
        Assert.That(GetQuantity(inventory, "med_small"), Is.EqualTo(1));
    }

    [Test]
    public void TryUseMedItemStringApiRejectsInvalidOrUnknownItemsBeforeConsumingInventory()
    {
        var playerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var ammo = new ItemDefinition("ammo_9mm", ItemCategory.Ammo, 30, false);
        var unusableMed = new ItemDefinition("med_broken", ItemCategory.MedItem, 5, false);
        var inventory = new PlayerInventory(3);
        inventory.TryAdd(ammo, 1, Guid.NewGuid());
        inventory.TryAdd(unusableMed, 1, Guid.NewGuid());
        var state = PlayerStateMachine.Create(playerId);
        state.ApplyDamage(new TechnicalDamageEvent(Guid.NewGuid(), null, playerId, 40, "technical"));
        var runtime = new EffectRuntime();
        runtime.RegisterItemDefinition(ammo);
        runtime.RegisterItemDefinition(unusableMed);

        InventoryTransactionResult nonMed = runtime.TryUseMedItem(playerId, inventory, state, "ammo_9mm", 20, 60d, Guid.NewGuid());
        InventoryTransactionResult unusable = runtime.TryUseMedItem(playerId, inventory, state, "med_broken", 20, 61d, Guid.NewGuid());
        InventoryTransactionResult unknown = runtime.TryUseMedItem(playerId, inventory, state, "med_unknown", 20, 62d, Guid.NewGuid());

        Assert.That(nonMed.Accepted, Is.False);
        Assert.That(nonMed.Reason, Is.EqualTo("med_item_invalid"));
        Assert.That(unusable.Accepted, Is.False);
        Assert.That(unusable.Reason, Is.EqualTo("med_item_invalid"));
        Assert.That(unknown.Accepted, Is.False);
        Assert.That(unknown.Reason, Is.EqualTo("med_item_unknown"));
        Assert.That(state.DamageTaken, Is.EqualTo(40));
        Assert.That(GetQuantity(inventory, "ammo_9mm"), Is.EqualTo(1));
        Assert.That(GetQuantity(inventory, "med_broken"), Is.EqualTo(1));
    }

    [Test]
    public void TickAppliesDueDamageOverTimeTicksUntilEffectExpires()
    {
        var playerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var runtime = new EffectRuntime();
        var applied = new List<int>();

        runtime.AddDamageOverTime(playerId, 3, 1d, 12d);

        int first = runtime.Tick(10d, (targetPlayerId, amount, kind, correlationId) =>
        {
            Assert.That(targetPlayerId, Is.EqualTo(playerId));
            Assert.That(kind, Is.EqualTo("damage_over_time"));
            Assert.That(correlationId, Is.Not.EqualTo(Guid.Empty));
            applied.Add(amount);
            return true;
        });
        int second = runtime.Tick(10.5d, (_, _, _, _) => throw new InvalidOperationException("not due"));
        int third = runtime.Tick(11d, (_, amount, _, _) =>
        {
            applied.Add(amount);
            return true;
        });
        int expired = runtime.Tick(12.1d, (_, _, _, _) => throw new InvalidOperationException("expired"));

        Assert.That(first, Is.EqualTo(1));
        Assert.That(second, Is.EqualTo(0));
        Assert.That(third, Is.EqualTo(1));
        Assert.That(expired, Is.EqualTo(0));
        Assert.That(applied, Is.EqualTo(new[] { 3, 3 }));
    }

    private static PlayerInventory CreateInventoryWithMeds(int quantity)
    {
        return CreateInventoryWithItem(MedDefinition(), quantity);
    }

    private static PlayerInventory CreateInventoryWithItem(ItemDefinition definition, int quantity)
    {
        var inventory = new PlayerInventory(2);
        inventory.TryAdd(definition, quantity, Guid.NewGuid());
        return inventory;
    }

    private static ItemDefinition MedDefinition()
    {
        return new ItemDefinition("med_small", ItemCategory.MedItem, 5, true);
    }

    private static int GetQuantity(PlayerInventory inventory, string itemId)
    {
        int quantity = 0;
        foreach (InventorySlot slot in inventory.GetSlots())
        {
            if (!slot.IsEmpty && slot.Stack.ItemId == itemId)
                quantity += slot.Stack.Quantity;
        }

        return quantity;
    }
}
