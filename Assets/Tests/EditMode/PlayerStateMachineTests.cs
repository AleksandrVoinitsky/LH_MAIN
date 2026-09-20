using System;
using LH.Main.Unity.Gameplay;
using NUnit.Framework;

public sealed class PlayerStateMachineTests
{
    [Test]
    public void DamageTransitionsAliveToWoundedToDead()
    {
        var playerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var state = PlayerStateMachine.Create(playerId);

        var wounded = state.ApplyDamage(new TechnicalDamageEvent(Guid.NewGuid(), null, playerId, 100, "technical"));
        var dead = state.ApplyDamage(new TechnicalDamageEvent(Guid.NewGuid(), null, playerId, 30, "technical"));

        Assert.That(wounded.Accepted, Is.True);
        Assert.That(wounded.NewState, Is.EqualTo(PlayerLifeState.Wounded));
        Assert.That(dead.Accepted, Is.True);
        Assert.That(dead.NewState, Is.EqualTo(PlayerLifeState.Dead));
        Assert.That(state.DamageTaken, Is.EqualTo(130));
    }

    [Test]
    public void DuplicateDamageCorrelationDoesNotApplyTwice()
    {
        var playerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var correlationId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var state = PlayerStateMachine.Create(playerId);
        var damage = new TechnicalDamageEvent(correlationId, null, playerId, 25, "technical");

        state.ApplyDamage(damage);
        var duplicate = state.ApplyDamage(damage);

        Assert.That(duplicate.Accepted, Is.False);
        Assert.That(duplicate.Reason, Is.EqualTo("damage_duplicate"));
        Assert.That(state.Health, Is.EqualTo(75));
    }

    [Test]
    public void TerminalStateRejectsDamageAndExtraction()
    {
        var playerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var state = PlayerStateMachine.Create(playerId);
        state.TryExtract();

        var damage = state.ApplyDamage(new TechnicalDamageEvent(Guid.NewGuid(), null, playerId, 25, "technical"));
        var extract = state.TryExtract();

        Assert.That(damage.Accepted, Is.False);
        Assert.That(damage.Reason, Is.EqualTo("state_terminal"));
        Assert.That(extract.Accepted, Is.False);
        Assert.That(extract.Reason, Is.EqualTo("state_terminal"));
    }
}
