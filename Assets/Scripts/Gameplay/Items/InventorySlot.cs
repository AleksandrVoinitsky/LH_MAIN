using System;

namespace LH.Main.Unity.Gameplay.Items
{
    public readonly struct InventorySlot
    {
        public static readonly InventorySlot Empty = new InventorySlot(new ItemStack(string.Empty, 0));

        public InventorySlot(ItemStack stack)
        {
            Stack = stack;
        }

        public ItemStack Stack { get; }
        public bool IsEmpty => Stack.Quantity <= 0 || string.IsNullOrEmpty(Stack.ItemId);

        public InventorySlot WithQuantity(int quantity)
        {
            if (quantity <= 0)
                return Empty;

            return new InventorySlot(new ItemStack(Stack.ItemId, quantity));
        }

        public InventorySlot AddQuantity(int quantity)
        {
            if (quantity < 0)
                throw new ArgumentOutOfRangeException(nameof(quantity));

            return new InventorySlot(new ItemStack(Stack.ItemId, Stack.Quantity + quantity));
        }
    }
}
