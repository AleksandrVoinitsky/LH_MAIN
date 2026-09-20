using System;
using System.Collections.Generic;

namespace LH.Main.Unity.Gameplay.Items
{
    public sealed class PlayerInventory
    {
        private readonly HashSet<Guid> _transactions = new HashSet<Guid>();
        private readonly InventorySlot[] _slots;

        public PlayerInventory(int slotCount)
        {
            if (slotCount < 0)
                throw new ArgumentOutOfRangeException(nameof(slotCount));

            _slots = new InventorySlot[slotCount];
            for (int index = 0; index < _slots.Length; index++)
                _slots[index] = InventorySlot.Empty;
        }

        public InventoryTransactionResult TryAdd(ItemDefinition definition, int quantity, Guid transactionId)
        {
            if (quantity <= 0)
                return InventoryTransactionResult.Rejected("quantity_invalid");

            if (string.IsNullOrEmpty(definition.ItemId))
                return InventoryTransactionResult.Rejected("item_id_required");

            if (_transactions.Contains(transactionId))
                return InventoryTransactionResult.Rejected("transaction_duplicate");

            if (definition.MaxStack <= 0)
                return InventoryTransactionResult.Rejected("quantity_invalid");

            if (GetAddCapacity(definition) < quantity)
                return InventoryTransactionResult.Rejected("inventory_full");

            int remaining = quantity;
            int firstSlotIndex = -1;

            for (int index = 0; index < _slots.Length && remaining > 0; index++)
            {
                InventorySlot slot = _slots[index];
                if (slot.IsEmpty || slot.Stack.ItemId != definition.ItemId)
                    continue;

                int addQuantity = Math.Min(definition.MaxStack - slot.Stack.Quantity, remaining);
                if (addQuantity <= 0)
                    continue;

                _slots[index] = slot.AddQuantity(addQuantity);
                remaining -= addQuantity;
                if (firstSlotIndex < 0)
                    firstSlotIndex = index;
            }

            for (int index = 0; index < _slots.Length && remaining > 0; index++)
            {
                if (!_slots[index].IsEmpty)
                    continue;

                int addQuantity = Math.Min(definition.MaxStack, remaining);
                _slots[index] = new InventorySlot(new ItemStack(definition.ItemId, addQuantity));
                remaining -= addQuantity;
                if (firstSlotIndex < 0)
                    firstSlotIndex = index;
            }

            _transactions.Add(transactionId);
            return InventoryTransactionResult.AcceptedResult(firstSlotIndex, new ItemStack(definition.ItemId, quantity));
        }

        public InventoryTransactionResult TryRemove(string itemId, int quantity, Guid transactionId)
        {
            if (quantity <= 0)
                return InventoryTransactionResult.Rejected("quantity_invalid");

            if (string.IsNullOrEmpty(itemId))
                return InventoryTransactionResult.Rejected("item_id_required");

            if (_transactions.Contains(transactionId))
                return InventoryTransactionResult.Rejected("transaction_duplicate");

            if (GetQuantity(itemId) < quantity)
                return InventoryTransactionResult.Rejected("item_not_found");

            int remaining = quantity;
            int firstSlotIndex = -1;

            for (int index = _slots.Length - 1; index >= 0 && remaining > 0; index--)
            {
                InventorySlot slot = _slots[index];
                if (slot.IsEmpty || slot.Stack.ItemId != itemId)
                    continue;

                int removeQuantity = Math.Min(slot.Stack.Quantity, remaining);
                _slots[index] = slot.WithQuantity(slot.Stack.Quantity - removeQuantity);
                remaining -= removeQuantity;
                firstSlotIndex = index;
            }

            _transactions.Add(transactionId);
            return InventoryTransactionResult.AcceptedResult(firstSlotIndex, new ItemStack(itemId, quantity));
        }

        public IReadOnlyList<InventorySlot> GetSlots()
        {
            return _slots;
        }

        public bool ContainsTransaction(Guid transactionId)
        {
            return _transactions.Contains(transactionId);
        }

        private int GetAddCapacity(ItemDefinition definition)
        {
            int capacity = 0;
            for (int index = 0; index < _slots.Length; index++)
            {
                InventorySlot slot = _slots[index];
                if (slot.IsEmpty)
                {
                    capacity += definition.MaxStack;
                }
                else if (slot.Stack.ItemId == definition.ItemId && slot.Stack.Quantity < definition.MaxStack)
                {
                    capacity += definition.MaxStack - slot.Stack.Quantity;
                }
            }

            return capacity;
        }

        private int GetQuantity(string itemId)
        {
            int quantity = 0;
            for (int index = 0; index < _slots.Length; index++)
            {
                InventorySlot slot = _slots[index];
                if (!slot.IsEmpty && slot.Stack.ItemId == itemId)
                    quantity += slot.Stack.Quantity;
            }

            return quantity;
        }
    }
}
