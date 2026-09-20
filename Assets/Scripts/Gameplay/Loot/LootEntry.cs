using System;
using LH.Main.Unity.Gameplay.Items;
using UnityEngine;

namespace LH.Main.Unity.Gameplay.Loot
{
    public readonly struct LootEntry
    {
        public LootEntry(Guid lootId, ItemStack stack, Vector3 position)
        {
            LootId = lootId;
            Stack = stack;
            Position = position;
        }

        public Guid LootId { get; }
        public ItemStack Stack { get; }
        public Vector3 Position { get; }
    }
}
