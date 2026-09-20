using System;
using System.Collections.Generic;
using System.Reflection;
using LH.Main.Unity.Gameplay;
using LH.Main.Unity.Gameplay.Items;
using NUnit.Framework;
using UnityEngine;

public sealed class CoreMatchRuntimeTests
{
    private readonly List<GameObject> _objects = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        for (int index = 0; index < _objects.Count; index++)
        {
            if (_objects[index] != null)
                UnityEngine.Object.DestroyImmediate(_objects[index]);
        }

        _objects.Clear();
    }

    [Test]
    public void TryFireAppliesHitscanDamageToRaycastTargetAndReportsHit()
    {
        var sourcePlayerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var targetPlayerId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var runtime = new CoreMatchRuntime();
        runtime.RegisterPlayer(sourcePlayerId);
        runtime.RegisterPlayer(targetPlayerId);
        CreateHitboxTarget(targetPlayerId, BodyZone.Head, new Vector3(0f, 0f, 5f));

        WeaponFireResult first = runtime.TryFire(sourcePlayerId, new WeaponFireRequest(Guid.NewGuid()), Vector3.zero, Vector3.forward, 100d);
        WeaponFireResult second = runtime.TryFire(sourcePlayerId, new WeaponFireRequest(Guid.NewGuid()), Vector3.zero, Vector3.forward, 100.2d);
        WeaponFireResult third = runtime.TryFire(sourcePlayerId, new WeaponFireRequest(Guid.NewGuid()), Vector3.zero, Vector3.forward, 100.4d);
        InventoryTransactionResult targetMed = runtime.TryUseMed(targetPlayerId, "med_basic", Guid.NewGuid(), 101d);

        Assert.That(first.Accepted, Is.True);
        Assert.That(ReadHit(first), Is.True);
        Assert.That(second.Accepted, Is.True);
        Assert.That(ReadHit(second), Is.True);
        Assert.That(third.Accepted, Is.True);
        Assert.That(ReadHit(third), Is.True);
        Assert.That(targetMed.Accepted, Is.False);
        Assert.That(targetMed.Reason, Is.EqualTo("state_terminal"));
    }

    [Test]
    public void TickAppliesZoneDamageToPlayersOutsideConfiguredSafeVolume()
    {
        var playerId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var runtime = new CoreMatchRuntime();
        runtime.RegisterPlayer(playerId);
        ZoneVolume safeVolume = CreateSafeVolume(new Vector3(100f, 0f, 100f), new Vector3(10f, 10f, 10f));
        MethodInfo configureZoneVolume = typeof(CoreMatchRuntime).GetMethod("ConfigureZoneVolume", new[] { typeof(ZoneVolume) });
        Assert.That(configureZoneVolume, Is.Not.Null, "CoreMatchRuntime should allow bootstrap to provide the scene safe-zone volume.");
        configureZoneVolume.Invoke(runtime, new object[] { safeVolume });

        runtime.Tick(Time.realtimeSinceStartupAsDouble + 1d);

        Assert.That(runtime.LastTickZoneDamageTicks, Is.EqualTo(1));
    }

    [Test]
    public void TryFireUsesAuthoritativePlayerPositionInsteadOfClientOrigin()
    {
        var sourcePlayerId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var targetPlayerId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        var runtime = new CoreMatchRuntime();
        runtime.RegisterPlayer(sourcePlayerId);
        runtime.RegisterPlayer(targetPlayerId);
        runtime.UpdatePlayerPosition(sourcePlayerId, Vector3.zero);
        runtime.UpdatePlayerPosition(targetPlayerId, new Vector3(1000f, 0f, 1000f));
        CreateHitboxTarget(targetPlayerId, BodyZone.Head, new Vector3(1000f, 0f, 1000f));

        WeaponFireResult fire = runtime.TryFire(
            sourcePlayerId,
            new WeaponFireRequest(Guid.NewGuid()),
            new Vector3(1000f, 0f, 995f),
            Vector3.forward,
            100d);

        Assert.That(fire.Accepted, Is.True);
        Assert.That(fire.Hit, Is.False);
    }

    [Test]
    public void TryThrowGrenadeUsesAuthoritativePlayerPositionInsteadOfClientOrigin()
    {
        var sourcePlayerId = Guid.Parse("66666666-6666-6666-6666-666666666666");
        var targetPlayerId = Guid.Parse("77777777-7777-7777-7777-777777777777");
        var runtime = new CoreMatchRuntime();
        runtime.RegisterPlayer(sourcePlayerId);
        runtime.RegisterPlayer(targetPlayerId);
        runtime.ConfigureZoneVolume(CreateSafeVolume(new Vector3(500f, 0f, 500f), new Vector3(2000f, 10f, 2000f)));
        runtime.UpdatePlayerPosition(sourcePlayerId, Vector3.zero);
        runtime.UpdatePlayerPosition(targetPlayerId, new Vector3(1000f, 0f, 1000f));

        InventoryTransactionResult thrown = runtime.TryThrowGrenade(
            sourcePlayerId,
            Guid.NewGuid(),
            new Vector3(996f, 0f, 1000f),
            Vector3.right,
            100d);
        runtime.Tick(103.2d);
        InventoryTransactionResult med = runtime.TryUseMed(targetPlayerId, "med_basic", Guid.NewGuid(), 104d);

        Assert.That(thrown.Accepted, Is.True);
        Assert.That(med.Accepted, Is.False);
        Assert.That(med.Reason, Is.EqualTo("healing_not_needed"));
    }

    [Test]
    public void DefaultRuntimeZoneDamagesPlayersAndEnablesMedCoverageWithoutSceneVolume()
    {
        var playerId = Guid.Parse("88888888-8888-8888-8888-888888888888");
        var runtime = new CoreMatchRuntime();
        runtime.RegisterPlayer(playerId);

        runtime.Tick(Time.realtimeSinceStartupAsDouble + 1d);
        InventoryTransactionResult med = runtime.TryUseMed(playerId, "med_basic", Guid.NewGuid(), Time.realtimeSinceStartupAsDouble + 2d);

        Assert.That(runtime.LastTickZoneDamageTicks, Is.EqualTo(1));
        Assert.That(med.Accepted, Is.True);
    }

    private void CreateHitboxTarget(Guid playerId, BodyZone bodyZone, Vector3 position)
    {
        GameObject target = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _objects.Add(target);
        target.transform.position = position;
        target.AddComponent<BodyZoneHitbox>().ConfigureForTest(playerId, bodyZone);
        Physics.SyncTransforms();
    }

    private ZoneVolume CreateSafeVolume(Vector3 position, Vector3 size)
    {
        var gameObject = new GameObject("core-match-runtime-safe-zone-test");
        _objects.Add(gameObject);
        var collider = gameObject.AddComponent<BoxCollider>();
        collider.isTrigger = true;
        collider.size = size;
        gameObject.transform.position = position;
        var volume = gameObject.AddComponent<ZoneVolume>();
        Physics.SyncTransforms();
        return volume;
    }

    private static bool ReadHit(WeaponFireResult result)
    {
        PropertyInfo hitProperty = typeof(WeaponFireResult).GetProperty("Hit", BindingFlags.Public | BindingFlags.Instance);
        Assert.That(hitProperty, Is.Not.Null, "WeaponFireResult should expose whether an accepted fire resolved a hitscan target.");
        return (bool)hitProperty.GetValue(result);
    }
}
