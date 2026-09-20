using System;
using LH.Main.Unity.Gameplay;
using NUnit.Framework;

public sealed class ZoneRuntimeTests
{
    [Test]
    public void GetActivePhaseSelectsPhaseByElapsedSecondsAfterSorting()
    {
        var latePhase = new ZonePhase(20d, 40d, 8, 2d);
        var earlyPhase = new ZonePhase(5d, 20d, 4, 1d);
        var runtime = new ZoneRuntime(new[] { latePhase, earlyPhase });

        Assert.That(runtime.GetActivePhase(4.99d), Is.Null);
        Assert.That(runtime.GetActivePhase(5d), Is.EqualTo(earlyPhase));
        Assert.That(runtime.GetActivePhase(25d), Is.EqualTo(latePhase));
        Assert.That(runtime.GetActivePhase(40d), Is.Null);
    }

    [Test]
    public void ConstructorRejectsEmptyPhaseList()
    {
        Assert.Throws<ArgumentException>(() => new ZoneRuntime(Array.Empty<ZonePhase>()));
    }

    [Test]
    public void ShouldApplyDamageTicksPerPlayerOnlyOutsideSafeVolumeDuringActivePhase()
    {
        var playerA = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var playerB = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var runtime = new ZoneRuntime(new[]
        {
            new ZonePhase(10d, 20d, 5, 2d)
        });

        Assert.That(runtime.ShouldApplyDamage(playerA, isInsideSafeVolume: true, elapsedSeconds: 10d), Is.False);
        Assert.That(runtime.ShouldApplyDamage(playerA, isInsideSafeVolume: false, elapsedSeconds: 9.99d), Is.False);
        Assert.That(runtime.ShouldApplyDamage(playerA, isInsideSafeVolume: false, elapsedSeconds: 10d), Is.True);
        Assert.That(runtime.ShouldApplyDamage(playerA, isInsideSafeVolume: false, elapsedSeconds: 11.99d), Is.False);
        Assert.That(runtime.ShouldApplyDamage(playerB, isInsideSafeVolume: false, elapsedSeconds: 11d), Is.True);
        Assert.That(runtime.ShouldApplyDamage(playerA, isInsideSafeVolume: false, elapsedSeconds: 12d), Is.True);
        Assert.That(runtime.ShouldApplyDamage(playerA, isInsideSafeVolume: false, elapsedSeconds: 20d), Is.False);
    }
}
