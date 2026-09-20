using System;
using LH.Main.Unity.Gameplay;
using NUnit.Framework;
using UnityEngine;

public sealed class GrenadeRuntimeTests
{
    [Test]
    public void TickExplodesOnceWhenFuseExpires()
    {
        GrenadeDefinition definition = GrenadeDefinition.StandardFrag();
        var sourcePlayerId = Guid.NewGuid();
        var targetPlayerId = Guid.NewGuid();
        var grenadeId = Guid.NewGuid();
        var request = new GrenadeThrowRequest(grenadeId, sourcePlayerId, Vector3.zero, Vector3.forward);
        var runtime = new GrenadeRuntime(request, definition, 10d);

        GrenadeExplosionResult beforeFuse = runtime.Tick(10d + definition.FuseSeconds - 0.01d, new[]
        {
            new GrenadeTarget(targetPlayerId, Vector3.zero)
        });

        runtime.Tick(10d + definition.FuseSeconds, Array.Empty<GrenadeTarget>());
        Vector3 explosionPosition = runtime.CurrentPosition;
        var explodingRuntime = new GrenadeRuntime(request, definition, 10d);

        GrenadeExplosionResult atFuse = explodingRuntime.Tick(10d + definition.FuseSeconds, new[]
        {
            new GrenadeTarget(targetPlayerId, explosionPosition)
        });

        GrenadeExplosionResult duplicate = explodingRuntime.Tick(10d + definition.FuseSeconds + 0.1d, new[]
        {
            new GrenadeTarget(targetPlayerId, explosionPosition)
        });

        Assert.That(beforeFuse.Exploded, Is.False);
        Assert.That(beforeFuse.DamageEvents.Count, Is.EqualTo(0));
        Assert.That(explodingRuntime.HasExploded, Is.True);

        Assert.That(atFuse.Exploded, Is.True);
        Assert.That(atFuse.DamageEvents.Count, Is.EqualTo(1));
        DamageEvent damage = atFuse.DamageEvents[0];
        Assert.That(damage.CorrelationId, Is.EqualTo(grenadeId));
        Assert.That(damage.SourcePlayerId, Is.EqualTo(sourcePlayerId));
        Assert.That(damage.TargetPlayerId, Is.EqualTo(targetPlayerId));
        Assert.That(damage.BaseAmount, Is.EqualTo(definition.MaxDamage));
        Assert.That(damage.BodyZone, Is.EqualTo(BodyZone.Torso));
        Assert.That(damage.Kind, Is.EqualTo("grenade"));

        Assert.That(duplicate.Exploded, Is.False);
        Assert.That(duplicate.DamageEvents.Count, Is.EqualTo(0));
    }

    [Test]
    public void TickAppliesDistanceFalloffOnlyInsideRadius()
    {
        GrenadeDefinition definition = GrenadeDefinition.StandardFrag();
        var request = new GrenadeThrowRequest(Guid.NewGuid(), null, Vector3.zero, Vector3.forward);
        var runtime = new GrenadeRuntime(request, definition, 2d);
        var centerTarget = Guid.NewGuid();
        var halfRadiusTarget = Guid.NewGuid();
        var nearEdgeTarget = Guid.NewGuid();
        var outsideTarget = Guid.NewGuid();

        runtime.Tick(2d + definition.FuseSeconds, Array.Empty<GrenadeTarget>());
        Vector3 explosionPosition = runtime.CurrentPosition;
        var explodingRuntime = new GrenadeRuntime(request, definition, 2d);

        GrenadeExplosionResult result = explodingRuntime.Tick(2d + definition.FuseSeconds, new[]
        {
            new GrenadeTarget(centerTarget, explosionPosition),
            new GrenadeTarget(halfRadiusTarget, explosionPosition + new Vector3(definition.Radius * 0.5f, 0f, 0f)),
            new GrenadeTarget(nearEdgeTarget, explosionPosition + new Vector3(definition.Radius - 0.01f, 0f, 0f)),
            new GrenadeTarget(outsideTarget, explosionPosition + new Vector3(definition.Radius + 0.01f, 0f, 0f))
        });

        Assert.That(result.Exploded, Is.True);
        Assert.That(result.DamageEvents.Count, Is.EqualTo(3));
        AssertDamage(result, centerTarget, definition.MaxDamage);
        AssertDamage(result, halfRadiusTarget, (int)Math.Ceiling(definition.MaxDamage * 0.5d));
        AssertDamage(result, nearEdgeTarget, 1);
        Assert.That(Array.Exists(ToArray(result.DamageEvents), damage => damage.TargetPlayerId == outsideTarget), Is.False);
    }

    [Test]
    public void ConstructorRejectsInvalidSpawnPositionAndThrowDirection()
    {
        GrenadeDefinition definition = GrenadeDefinition.StandardFrag();

        Assert.Throws<ArgumentException>(() => new GrenadeRuntime(
            new GrenadeThrowRequest(Guid.NewGuid(), null, new Vector3(float.NaN, 0f, 0f), Vector3.forward),
            definition,
            1d));

        Assert.Throws<ArgumentException>(() => new GrenadeRuntime(
            new GrenadeThrowRequest(Guid.NewGuid(), null, Vector3.zero, Vector3.zero),
            definition,
            1d));

        Assert.Throws<ArgumentException>(() => new GrenadeRuntime(
            new GrenadeThrowRequest(Guid.NewGuid(), null, Vector3.zero, new Vector3(0f, float.PositiveInfinity, 0f)),
            definition,
            1d));
    }

    [Test]
    public void TickMovesAuthoritativePositionAlongThrowDirectionBeforeFuse()
    {
        GrenadeDefinition definition = GrenadeDefinition.StandardFrag();
        var runtime = new GrenadeRuntime(
            new GrenadeThrowRequest(Guid.NewGuid(), null, Vector3.zero, Vector3.forward),
            definition,
            4d);

        runtime.Tick(4.5d, Array.Empty<GrenadeTarget>());

        Assert.That(runtime.CurrentPosition.z, Is.GreaterThan(0f));
        Assert.That(runtime.CurrentPosition.x, Is.EqualTo(0f).Within(0.001f));
    }

    [Test]
    public void TickStopsAuthoritativePositionAtPhysicsCollision()
    {
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            wall.name = "grenade-runtime-wall";
            wall.transform.position = new Vector3(0f, 0f, 1f);
            wall.transform.localScale = new Vector3(2f, 2f, 0.2f);
            Physics.SyncTransforms();

            GrenadeDefinition definition = GrenadeDefinition.StandardFrag();
            var runtime = new GrenadeRuntime(
                new GrenadeThrowRequest(Guid.NewGuid(), null, Vector3.zero, Vector3.forward),
                definition,
                7d);

            runtime.Tick(7.5d, Array.Empty<GrenadeTarget>());

            Assert.That(runtime.CurrentPosition.z, Is.GreaterThan(0f));
            Assert.That(runtime.CurrentPosition.z, Is.LessThan(1f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(wall);
        }
    }

    private static void AssertDamage(GrenadeExplosionResult result, Guid targetPlayerId, int baseAmount)
    {
        DamageEvent damage = Array.Find(ToArray(result.DamageEvents), candidate => candidate.TargetPlayerId == targetPlayerId);

        Assert.That(damage.TargetPlayerId, Is.EqualTo(targetPlayerId));
        Assert.That(damage.BaseAmount, Is.EqualTo(baseAmount));
        Assert.That(damage.BodyZone, Is.EqualTo(BodyZone.Torso));
    }

    private static DamageEvent[] ToArray(System.Collections.Generic.IReadOnlyList<DamageEvent> damageEvents)
    {
        var result = new DamageEvent[damageEvents.Count];
        for (int i = 0; i < damageEvents.Count; i++)
            result[i] = damageEvents[i];

        return result;
    }
}
