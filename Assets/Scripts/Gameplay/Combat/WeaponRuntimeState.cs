namespace LH.Main.Unity.Gameplay
{
    public readonly struct WeaponRuntimeState
    {
        public WeaponRuntimeState(int magazineAmmo, int reserveAmmo, bool isReloading, double reloadCompleteTimeSeconds)
        {
            MagazineAmmo = magazineAmmo;
            ReserveAmmo = reserveAmmo;
            IsReloading = isReloading;
            ReloadCompleteTimeSeconds = reloadCompleteTimeSeconds;
        }

        public int MagazineAmmo { get; }
        public int ReserveAmmo { get; }
        public bool IsReloading { get; }
        public double ReloadCompleteTimeSeconds { get; }
    }
}
