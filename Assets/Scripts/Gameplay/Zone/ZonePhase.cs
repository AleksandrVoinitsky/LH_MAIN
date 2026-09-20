namespace LH.Main.Unity.Gameplay
{
    public sealed class ZonePhase
    {
        public ZonePhase(double startsAtSeconds, double endsAtSeconds, int damagePerTick, double tickIntervalSeconds)
        {
            StartsAtSeconds = startsAtSeconds;
            EndsAtSeconds = endsAtSeconds;
            DamagePerTick = damagePerTick;
            TickIntervalSeconds = tickIntervalSeconds;
        }

        public double StartsAtSeconds { get; }

        public double EndsAtSeconds { get; }

        public int DamagePerTick { get; }

        public double TickIntervalSeconds { get; }
    }
}
