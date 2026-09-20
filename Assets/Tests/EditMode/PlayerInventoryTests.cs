using System;
using LH.Main.Unity.Gameplay.Items;
using NUnit.Framework;

public sealed class PlayerInventoryTests
{
    [Test]
    public void TryAddStacksIntoExistingSlotsBeforeUsingEmptySlots()
    {
        var inventory = new PlayerInventory(2);
        var ammo = new ItemDefinition("ammo_9mm", ItemCategory.Ammo, 30, false);

        var first = inventory.TryAdd(ammo, 20, Guid.NewGuid());
        var second = inventory.TryAdd(ammo, 15, Guid.NewGuid());

        Assert.That(first.Accepted, Is.True);
        Assert.That(second.Accepted, Is.True);
        Assert.That(inventory.GetSlots()[0].Stack.ItemId, Is.EqualTo("ammo_9mm"));
        Assert.That(inventory.GetSlots()[0].Stack.Quantity, Is.EqualTo(30));
        Assert.That(inventory.GetSlots()[1].Stack.ItemId, Is.EqualTo("ammo_9mm"));
        Assert.That(inventory.GetSlots()[1].Stack.Quantity, Is.EqualTo(5));
    }

    [Test]
    public void TryAddRejectsFullInventoryWithoutPartialMutation()
    {
        var inventory = new PlayerInventory(1);
        var ammo = new ItemDefinition("ammo_9mm", ItemCategory.Ammo, 10, false);

        inventory.TryAdd(ammo, 8, Guid.NewGuid());

        var result = inventory.TryAdd(ammo, 5, Guid.NewGuid());

        Assert.That(result.Accepted, Is.False);
        Assert.That(result.Reason, Is.EqualTo("inventory_full"));
        Assert.That(inventory.GetSlots()[0].Stack.ItemId, Is.EqualTo("ammo_9mm"));
        Assert.That(inventory.GetSlots()[0].Stack.Quantity, Is.EqualTo(8));
    }

    [Test]
    public void TryAddRejectsDuplicateTransaction()
    {
        var inventory = new PlayerInventory(2);
        var ammo = new ItemDefinition("ammo_9mm", ItemCategory.Ammo, 30, false);
        var transactionId = Guid.NewGuid();

        inventory.TryAdd(ammo, 5, transactionId);
        var duplicate = inventory.TryAdd(ammo, 5, transactionId);

        Assert.That(duplicate.Accepted, Is.False);
        Assert.That(duplicate.Reason, Is.EqualTo("transaction_duplicate"));
        Assert.That(inventory.GetSlots()[0].Stack.Quantity, Is.EqualTo(5));
    }

    [Test]
    public void TryRemoveConsumesQuantityAcrossSlots()
    {
        var inventory = new PlayerInventory(2);
        var ammo = new ItemDefinition("ammo_9mm", ItemCategory.Ammo, 10, false);

        inventory.TryAdd(ammo, 15, Guid.NewGuid());

        var result = inventory.TryRemove("ammo_9mm", 12, Guid.NewGuid());

        Assert.That(result.Accepted, Is.True);
        Assert.That(result.Stack.ItemId, Is.EqualTo("ammo_9mm"));
        Assert.That(result.Stack.Quantity, Is.EqualTo(12));
        Assert.That(inventory.GetSlots()[0].Stack.ItemId, Is.EqualTo("ammo_9mm"));
        Assert.That(inventory.GetSlots()[0].Stack.Quantity, Is.EqualTo(3));
        Assert.That(inventory.GetSlots()[1].IsEmpty, Is.True);
    }
}
