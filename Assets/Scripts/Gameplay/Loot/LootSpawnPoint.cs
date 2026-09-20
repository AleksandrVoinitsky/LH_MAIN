using System;
using LH.Main.Unity.Gameplay.Items;
using UnityEngine;

namespace LH.Main.Unity.Gameplay.Loot
{
    public sealed class LootSpawnPoint : MonoBehaviour
    {
        [SerializeField] private string itemId;
        [SerializeField] private int quantity;
        [SerializeField] private Vector3 localOffset;

        public LootEntry BuildEntry(Guid lootId)
        {
            return new LootEntry(lootId, new ItemStack(itemId, quantity), transform.position + localOffset);
        }
    }
}
