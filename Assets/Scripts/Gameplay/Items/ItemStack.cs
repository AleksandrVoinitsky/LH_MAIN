namespace LH.Main.Unity.Gameplay.Items
{
    public readonly struct ItemStack
    {
        public ItemStack(string itemId, int quantity)
        {
            ItemId = itemId;
            Quantity = quantity;
        }

        public string ItemId { get; }
        public int Quantity { get; }
    }
}
