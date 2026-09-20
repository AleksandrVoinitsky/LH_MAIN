using System;
using System.Collections.Generic;

namespace LH.Main.Unity.Gameplay
{
    public sealed class WeaponRuntime
    {
        private const double TimeEpsilon = 0.0000001d;

        private readonly WeaponDefinition _definition;
        private readonly HashSet<Guid> _fireRequestIds = new HashSet<Guid>();
        private readonly HashSet<Guid> _reloadRequestIds = new HashSet<Guid>();
        private int _magazineAmmo;
        private int _reserveAmmo;
        private double _nextFireTimeSeconds;
        private bool _isReloading;
        private double _reloadCompleteTimeSeconds;

        public WeaponRuntime(WeaponDefinition definition)
        {
            _definition = definition ?? throw new ArgumentNullException(nameof(definition));
            _magazineAmmo = definition.MagazineSize;
            _reserveAmmo = definition.ReserveAmmo;
        }

        public WeaponRuntimeState State => CurrentState();

        public WeaponFireResult TryFire(WeaponFireRequest request, double serverTimeSeconds)
        {
            CompleteReloadIfReady(serverTimeSeconds);

            if (!_fireRequestIds.Add(request.RequestId))
                return Rejected("fire_duplicate");

            if (serverTimeSeconds + TimeEpsilon < _nextFireTimeSeconds)
                return Rejected("weapon_cooldown");

            if (_isReloading)
                return Rejected("weapon_reloading");

            if (_magazineAmmo <= 0)
                return Rejected("ammo_empty");

            _magazineAmmo--;
            _nextFireTimeSeconds = serverTimeSeconds + _definition.FireCooldownSeconds;
            return Accepted();
        }

        public WeaponFireResult TryReload(Guid requestId, double serverTimeSeconds)
        {
            CompleteReloadIfReady(serverTimeSeconds);

            if (!_reloadRequestIds.Add(requestId))
                return Rejected("reload_duplicate");

            if (_magazineAmmo >= _definition.MagazineSize)
                return Rejected("reload_not_needed");

            if (_reserveAmmo <= 0)
                return Rejected("reserve_empty");

            if (_isReloading)
                return Rejected("weapon_reloading");

            _isReloading = true;
            _reloadCompleteTimeSeconds = serverTimeSeconds + _definition.ReloadSeconds;
            return Accepted();
        }

        private void CompleteReloadIfReady(double serverTimeSeconds)
        {
            if (!_isReloading || serverTimeSeconds + TimeEpsilon < _reloadCompleteTimeSeconds)
                return;

            int needed = _definition.MagazineSize - _magazineAmmo;
            int moved = Math.Min(needed, _reserveAmmo);
            _magazineAmmo += moved;
            _reserveAmmo -= moved;
            _isReloading = false;
            _reloadCompleteTimeSeconds = 0d;
        }

        private WeaponFireResult Accepted()
        {
            return new WeaponFireResult(true, string.Empty, CurrentState());
        }

        private WeaponFireResult Rejected(string reason)
        {
            return new WeaponFireResult(false, reason, CurrentState());
        }

        private WeaponRuntimeState CurrentState()
        {
            return new WeaponRuntimeState(_magazineAmmo, _reserveAmmo, _isReloading, _reloadCompleteTimeSeconds);
        }
    }
}
