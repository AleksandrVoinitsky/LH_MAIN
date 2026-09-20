namespace LH.Main.Unity.Networking
{
    public struct MovementCommand
    {
        public uint Sequence;
        public float MoveX;
        public float MoveY;
        public double SentAtClientTime;

        public MovementCommand(uint sequence, float moveX, float moveY, double sentAtClientTime)
        {
            Sequence = sequence;
            MoveX = moveX;
            MoveY = moveY;
            SentAtClientTime = sentAtClientTime;
        }
    }
}
