namespace LH.Main.Unity.Gameplay.Items
{
    public readonly struct ItemDefinition
    {
        public ItemDefinition(string itemId, ItemCategory category, int maxStack, bool usable)
        {
            ItemId = itemId;
            Category = category;
            MaxStack = maxStack;
            Usable = usable;
        }

        public string ItemId { get; }
        public ItemCategory Category { get; }
        public int MaxStack { get; }
        public bool Usable { get; }
    }
}
