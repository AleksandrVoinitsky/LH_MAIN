using System;
using LH.Main.Unity.Gameplay;
using NUnit.Framework;
using UnityEngine;

public sealed class ServerRaycastWeaponResolverTests
{
    private GameObject _target;

    [TearDown]
    public void TearDown()
    {
        if (_target != null)
            UnityEngine.Object.DestroyImmediate(_target);
    }

    [Test]
    public void TryResolveReturnsPlayerIdAndBodyZoneFromFirstHitbox()
    {
        var playerId = Guid.NewGuid();
        _target = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _target.transform.position = new Vector3(0f, 0f, 5f);
        _target.AddComponent<BodyZoneHitbox>().ConfigureForTest(playerId, BodyZone.Head);
        Physics.SyncTransforms();

        bool resolved = ServerRaycastWeaponResolver.TryResolve(
            Vector3.zero,
            Vector3.forward * 10f,
            100f,
            Physics.DefaultRaycastLayers,
            out Guid targetPlayerId,
            out BodyZone bodyZone);

        Assert.That(resolved, Is.True);
        Assert.That(targetPlayerId, Is.EqualTo(playerId));
        Assert.That(bodyZone, Is.EqualTo(BodyZone.Head));
    }

    [Test]
    public void TryResolveRejectsZeroDirection()
    {
        bool resolved = ServerRaycastWeaponResolver.TryResolve(
            Vector3.zero,
            Vector3.zero,
            100f,
            Physics.DefaultRaycastLayers,
            out Guid targetPlayerId,
            out BodyZone bodyZone);

        Assert.That(resolved, Is.False);
        Assert.That(targetPlayerId, Is.EqualTo(Guid.Empty));
        Assert.That(bodyZone, Is.EqualTo(default(BodyZone)));
    }
}
