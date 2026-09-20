using System;
using LH.Main.Unity.Gameplay;
using NUnit.Framework;

public sealed class WeaponRuntimeTests
{
    [Test]
    public void TryFireConsumesAmmoAndRejectsUntilCooldownExpires()
    {
        var weapon = new WeaponRuntime(WeaponDefinition.BaselineRifle());

        WeaponFireResult first = weapon.TryFire(new WeaponFireRequest(Guid.NewGuid()), 10d);
        WeaponFireResult cooldown = weapon.TryFire(new WeaponFireRequest(Guid.NewGuid()), 10.05d);
        WeaponFireResult afterCooldown = weapon.TryFire(new WeaponFireRequest(Guid.NewGuid()), 10.1d);

        Assert.That(first.Accepted, Is.True);
        Assert.That(first.Reason, Is.EqualTo(string.Empty));
        Assert.That(first.MagazineAmmo, Is.EqualTo(29));
        Assert.That(cooldown.Accepted, Is.False);
        Assert.That(cooldown.Reason, Is.EqualTo("weapon_cooldown"));
        Assert.That(cooldown.MagazineAmmo, Is.EqualTo(29));
        Assert.That(afterCooldown.Accepted, Is.True);
        Assert.That(afterCooldown.MagazineAmmo, Is.EqualTo(28));
    }

    [Test]
    public void TryReloadMovesReserveAmmoIntoMagazineAfterReloadTime()
    {
        var weapon = new WeaponRuntime(WeaponDefinition.BaselineRifle());
        weapon.TryFire(new WeaponFireRequest(Guid.NewGuid()), 1d);
        weapon.TryFire(new WeaponFireRequest(Guid.NewGuid()), 1.1d);

        WeaponFireResult started = weapon.TryReload(Guid.NewGuid(), 2d);
        WeaponFireResult blockedWhileReloading = weapon.TryFire(new WeaponFireRequest(Guid.NewGuid()), 3d);
        WeaponFireResult completed = weapon.TryFire(new WeaponFireRequest(Guid.NewGuid()), 4d);

        Assert.That(started.Accepted, Is.True);
        Assert.That(started.MagazineAmmo, Is.EqualTo(28));
        Assert.That(started.ReserveAmmo, Is.EqualTo(90));
        Assert.That(blockedWhileReloading.Accepted, Is.False);
        Assert.That(blockedWhileReloading.Reason, Is.EqualTo("weapon_reloading"));
        Assert.That(completed.Accepted, Is.True);
        Assert.That(completed.MagazineAmmo, Is.EqualTo(29));
        Assert.That(completed.ReserveAmmo, Is.EqualTo(88));
    }

    [Test]
    public void TryFireRejectsDuplicateRequestWithoutConsumingAmmo()
    {
        var weapon = new WeaponRuntime(WeaponDefinition.BaselineRifle());
        var requestId = Guid.NewGuid();

        WeaponFireResult first = weapon.TryFire(new WeaponFireRequest(requestId), 1d);
        WeaponFireResult duplicate = weapon.TryFire(new WeaponFireRequest(requestId), 1.2d);

        Assert.That(first.Accepted, Is.True);
        Assert.That(duplicate.Accepted, Is.False);
        Assert.That(duplicate.Reason, Is.EqualTo("fire_duplicate"));
        Assert.That(duplicate.MagazineAmmo, Is.EqualTo(29));
    }

    [Test]
    public void TryFireRejectsEmptyMagazineAndTryReloadRejectsDuplicateNotNeededAndEmptyReserve()
    {
        var weapon = new WeaponRuntime(WeaponDefinition.BaselineRifle());
        for (int i = 0; i < 30; i++)
            weapon.TryFire(new WeaponFireRequest(Guid.NewGuid()), i * 0.1d);

        WeaponFireResult empty = weapon.TryFire(new WeaponFireRequest(Guid.NewGuid()), 3.1d);
        var reloadId = Guid.NewGuid();
        WeaponFireResult reload = weapon.TryReload(reloadId, 3.2d);
        WeaponFireResult duplicateReload = weapon.TryReload(reloadId, 3.3d);

        Assert.That(empty.Accepted, Is.False);
        Assert.That(empty.Reason, Is.EqualTo("ammo_empty"));
        Assert.That(reload.Accepted, Is.True);
        Assert.That(duplicateReload.Accepted, Is.False);
        Assert.That(duplicateReload.Reason, Is.EqualTo("reload_duplicate"));

        for (int cycle = 0; cycle < 3; cycle++)
        {
            double cycleStart = 6d + (cycle * 6d);
            for (int shot = 0; shot < 30; shot++)
                weapon.TryFire(new WeaponFireRequest(Guid.NewGuid()), cycleStart + (shot * 0.1d));

            weapon.TryReload(Guid.NewGuid(), cycleStart + 3.1d);
        }

        WeaponFireResult noReserve = weapon.TryReload(Guid.NewGuid(), 30d);
        Assert.That(noReserve.Accepted, Is.False);
        Assert.That(noReserve.Reason, Is.EqualTo("reserve_empty"));

        var fullWeapon = new WeaponRuntime(WeaponDefinition.BaselineRifle());
        WeaponFireResult notNeeded = fullWeapon.TryReload(Guid.NewGuid(), 1d);
        Assert.That(notNeeded.Accepted, Is.False);
        Assert.That(notNeeded.Reason, Is.EqualTo("reload_not_needed"));
    }
}
