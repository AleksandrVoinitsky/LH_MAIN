using System;
using System.Collections.Generic;
using LH.Main.Unity.Gameplay.Items;
using UnityEngine;

namespace LH.Main.Unity.Gameplay.Loot
{
    public sealed class LootContainer
    {
        private readonly object _lock = new object();
        private readonly Dictionary<Guid, LootEntry> _loot = new Dictionary<Guid, LootEntry>();

        public bool AddLoot(LootEntry entry)
        {
            if (entry.LootId == Guid.Empty)
                return false;

            lock (_lock)
            {
                if (_loot.ContainsKey(entry.LootId))
                    return false;

                _loot.Add(entry.LootId, entry);
                return true;
            }
        }

        public InventoryTransactionResult TryPickup(
            Guid lootId,
            PlayerInventory inventory,
            ItemDefinition definition,
            Guid transactionId,
            Vector3 playerPosition,
            float interactionRange)
        {
            if (lootId == Guid.Empty)
                return InventoryTransactionResult.Rejected("loot_id_required");

            lock (_lock)
            {
                if (!_loot.TryGetValue(lootId, out LootEntry entry))
                    return InventoryTransactionResult.Rejected("loot_unavailable");

                if (Vector3.Distance(playerPosition, entry.Position) > interactionRange)
                    return InventoryTransactionResult.Rejected("interaction_out_of_range");

                InventoryTransactionResult result = inventory.TryAdd(definition, entry.Stack.Quantity, transactionId);
                if (!result.Accepted)
                    return result;

                _loot.Remove(lootId);
                return result;
            }
        }

        public IReadOnlyList<LootEntry> GetAvailableLoot()
        {
            lock (_lock)
            {
                return Array.AsReadOnly(new List<LootEntry>(_loot.Values).ToArray());
            }
        }
    }
}
