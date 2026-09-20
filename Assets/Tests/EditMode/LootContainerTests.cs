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
        LootContainer container = CreateContainer(out GameObject gameObject);
        try
        {
            var lootId = Guid.NewGuid();
            var ammo = new ItemDefinition("ammo_9mm", ItemCategory.Ammo, 30, false);
            var entry = new LootEntry(lootId, new ItemStack("ammo_9mm", 12), Vector3.zero);
            var firstInventory = new PlayerInventory(1);
            var secondInventory = new PlayerInventory(1);

            Assert.That(container.AddLoot(entry), Is.True);

            var first = container.TryPickup(lootId, firstInventory, ammo, Guid.NewGuid(), Vector3.zero, 2f);
            var second = container.TryPickup(lootId, secondInventory, ammo, Guid.NewGuid(), Vector3.zero, 2f);

            Assert.That(first.Accepted, Is.True);
            Assert.That(firstInventory.GetSlots()[0].Stack.ItemId, Is.EqualTo("ammo_9mm"));
            Assert.That(firstInventory.GetSlots()[0].Stack.Quantity, Is.EqualTo(12));
            Assert.That(container.GetAvailableLoot(), Is.Empty);
            Assert.That(second.Accepted, Is.False);
            Assert.That(second.Reason, Is.EqualTo("loot_unavailable"));
            Assert.That(secondInventory.GetSlots()[0].IsEmpty, Is.True);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    [Test]
    public void PickupRejectsOutOfRangePlayer()
    {
        LootContainer container = CreateContainer(out GameObject gameObject);
        try
        {
            var lootId = Guid.NewGuid();
            var ammo = new ItemDefinition("ammo_9mm", ItemCategory.Ammo, 30, false);
            var entry = new LootEntry(lootId, new ItemStack("ammo_9mm", 12), new Vector3(10f, 0f, 0f));
            var inventory = new PlayerInventory(1);

            container.AddLoot(entry);

            var result = container.TryPickup(lootId, inventory, ammo, Guid.NewGuid(), Vector3.zero, 2f);

            Assert.That(result.Accepted, Is.False);
            Assert.That(result.Reason, Is.EqualTo("interaction_out_of_range"));
            Assert.That(inventory.GetSlots()[0].IsEmpty, Is.True);
            Assert.That(container.GetAvailableLoot(), Has.Count.EqualTo(1));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    [Test]
    public void PickupRejectsMismatchedItemDefinitionWithoutMutation()
    {
        LootContainer container = CreateContainer(out GameObject gameObject);
        try
        {
            var lootId = Guid.NewGuid();
            var medkit = new ItemDefinition("medkit_small", ItemCategory.MedItem, 5, true);
            var entry = new LootEntry(lootId, new ItemStack("ammo_9mm", 12), Vector3.zero);
            var inventory = new PlayerInventory(1);

            container.AddLoot(entry);

            var result = container.TryPickup(lootId, inventory, medkit, Guid.NewGuid(), Vector3.zero, 2f);

            Assert.That(result.Accepted, Is.False);
            Assert.That(result.Reason, Is.EqualTo("loot_item_mismatch"));
            Assert.That(inventory.GetSlots()[0].IsEmpty, Is.True);
            Assert.That(container.GetAvailableLoot(), Has.Count.EqualTo(1));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    [Test]
    public void LootContainerCanExistAsSceneComponent()
    {
        var gameObject = new GameObject("Loot Container");
        try
        {
            var container = gameObject.AddComponent<LootContainer>();

            Assert.That(container, Is.Not.Null);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    private static LootContainer CreateContainer(out GameObject gameObject)
    {
        gameObject = new GameObject("Loot Container");
        return gameObject.AddComponent<LootContainer>();
    }
}
