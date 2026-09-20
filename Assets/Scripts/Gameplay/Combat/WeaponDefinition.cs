namespace LH.Main.Unity.Gameplay
{
    public sealed class WeaponDefinition
    {
        private WeaponDefinition(int magazineSize, int reserveAmmo, double fireCooldownSeconds, double reloadSeconds, float maxRange, int baseDamage)
        {
            MagazineSize = magazineSize;
            ReserveAmmo = reserveAmmo;
            FireCooldownSeconds = fireCooldownSeconds;
            ReloadSeconds = reloadSeconds;
            MaxRange = maxRange;
            BaseDamage = baseDamage;
        }

        public int MagazineSize { get; }
        public int ReserveAmmo { get; }
        public double FireCooldownSeconds { get; }
        public double ReloadSeconds { get; }
        public float MaxRange { get; }
        public int BaseDamage { get; }

        public static WeaponDefinition BaselineRifle()
        {
            return new WeaponDefinition(30, 90, 0.1d, 2.0d, 100f, 30);
        }
    }
}
