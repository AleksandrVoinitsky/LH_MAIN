namespace LH.Main.Unity.Gameplay
{
    public sealed class GrenadeDefinition
    {
        private GrenadeDefinition(double fuseSeconds, float radius, int maxDamage)
        {
            FuseSeconds = fuseSeconds;
            Radius = radius;
            MaxDamage = maxDamage;
        }

        public double FuseSeconds { get; }
        public float Radius { get; }
        public int MaxDamage { get; }

        public static GrenadeDefinition StandardFrag()
        {
            return new GrenadeDefinition(3d, 5f, 120);
        }
    }
}
