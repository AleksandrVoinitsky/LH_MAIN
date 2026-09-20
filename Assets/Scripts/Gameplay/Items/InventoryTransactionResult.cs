namespace LH.Main.Unity.Gameplay.Items
{
    public readonly struct InventoryTransactionResult
    {
        private InventoryTransactionResult(bool accepted, int slotIndex, ItemStack stack, string reason)
        {
            Accepted = accepted;
            SlotIndex = slotIndex;
            Stack = stack;
            Reason = reason;
        }

        public bool Accepted { get; }
        public int SlotIndex { get; }
        public ItemStack Stack { get; }
        public string Reason { get; }

        public static InventoryTransactionResult AcceptedResult(int slotIndex, ItemStack stack)
        {
            return new InventoryTransactionResult(true, slotIndex, stack, string.Empty);
        }

        public static InventoryTransactionResult Rejected(string reason)
        {
            return new InventoryTransactionResult(false, -1, default, reason);
        }
    }
}
