namespace LH.Main.Unity.Gameplay
{
    public readonly struct WeaponFireResult
    {
        public WeaponFireResult(bool accepted, string reason, WeaponRuntimeState state, bool hit = false)
        {
            Accepted = accepted;
            Reason = reason;
            State = state;
            Hit = hit;
        }

        public bool Accepted { get; }
        public string Reason { get; }
        public WeaponRuntimeState State { get; }
        public bool Hit { get; }
        public int MagazineAmmo => State.MagazineAmmo;
        public int ReserveAmmo => State.ReserveAmmo;
        public bool IsReloading => State.IsReloading;
    }
}
