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

        GrenadeExplosionResult atFuse = runtime.Tick(10d + definition.FuseSeconds, new[]
        {
            new GrenadeTarget(targetPlayerId, Vector3.zero)
        });

        GrenadeExplosionResult duplicate = runtime.Tick(10d + definition.FuseSeconds + 0.1d, new[]
        {
            new GrenadeTarget(targetPlayerId, Vector3.zero)
        });

        Assert.That(beforeFuse.Exploded, Is.False);
        Assert.That(beforeFuse.DamageEvents.Count, Is.EqualTo(0));
        Assert.That(runtime.HasExploded, Is.True);

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

        GrenadeExplosionResult result = runtime.Tick(2d + definition.FuseSeconds, new[]
        {
            new GrenadeTarget(centerTarget, Vector3.zero),
            new GrenadeTarget(halfRadiusTarget, new Vector3(definition.Radius * 0.5f, 0f, 0f)),
            new GrenadeTarget(nearEdgeTarget, new Vector3(definition.Radius - 0.01f, 0f, 0f)),
            new GrenadeTarget(outsideTarget, new Vector3(definition.Radius + 0.01f, 0f, 0f))
        });

        Assert.That(result.Exploded, Is.True);
        Assert.That(result.DamageEvents.Count, Is.EqualTo(3));
        AssertDamage(result, centerTarget, definition.MaxDamage);
        AssertDamage(result, halfRadiusTarget, (int)Math.Ceiling(definition.MaxDamage * 0.5d));
        AssertDamage(result, nearEdgeTarget, 1);
        Assert.That(Array.Exists(ToArray(result.DamageEvents), damage => damage.TargetPlayerId == outsideTarget), Is.False);
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
