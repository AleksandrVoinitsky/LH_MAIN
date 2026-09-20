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

    [Test]
    public void HeadDamageUsesStandardMultiplierAndRoundsUp()
    {
        var playerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var state = PlayerStateMachine.Create(playerId);
        var damage = new DamageEvent(Guid.NewGuid(), null, playerId, 13, BodyZone.Head, "bullet");

        var change = state.ApplyDamage(damage, BodyZoneDamageTable.Standard());

        Assert.That(change.Accepted, Is.True);
        Assert.That(change.OldState, Is.EqualTo(PlayerLifeState.Alive));
        Assert.That(change.NewState, Is.EqualTo(PlayerLifeState.Alive));
        Assert.That(state.DamageTaken, Is.EqualTo(26));
        Assert.That(state.Health, Is.EqualTo(74));
    }

    [Test]
    public void LimbDamageUsesStandardMultiplierAndRoundsUpToAtLeastOne()
    {
        var playerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var state = PlayerStateMachine.Create(playerId);

        state.ApplyDamage(new DamageEvent(Guid.NewGuid(), null, playerId, 3, BodyZone.Arms, "bullet"), BodyZoneDamageTable.Standard());
        state.ApplyDamage(new DamageEvent(Guid.NewGuid(), null, playerId, 1, BodyZone.Legs, "bullet"), BodyZoneDamageTable.Standard());

        Assert.That(state.DamageTaken, Is.EqualTo(4));
        Assert.That(state.Health, Is.EqualTo(96));
    }

    [Test]
    public void HealingReducesDamageAndCanReturnWoundedPlayerToAlive()
    {
        var playerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var state = PlayerStateMachine.Create(playerId);
        state.ApplyDamage(new TechnicalDamageEvent(Guid.NewGuid(), null, playerId, 110, "technical"));

        var change = state.ApplyHealing(new HealingEvent(Guid.NewGuid(), playerId, 20, "medkit"));

        Assert.That(change.Accepted, Is.True);
        Assert.That(change.OldState, Is.EqualTo(PlayerLifeState.Wounded));
        Assert.That(change.NewState, Is.EqualTo(PlayerLifeState.Alive));
        Assert.That(state.DamageTaken, Is.EqualTo(90));
        Assert.That(state.Health, Is.EqualTo(10));
    }

    [Test]
    public void HealingDoesNotReviveDeadPlayers()
    {
        var playerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var state = PlayerStateMachine.Create(playerId);
        state.ApplyDamage(new TechnicalDamageEvent(Guid.NewGuid(), null, playerId, 130, "technical"));

        var change = state.ApplyHealing(new HealingEvent(Guid.NewGuid(), playerId, 10, "medkit"));

        Assert.That(change.Accepted, Is.False);
        Assert.That(change.Reason, Is.EqualTo("state_terminal"));
        Assert.That(state.LifeState, Is.EqualTo(PlayerLifeState.Dead));
        Assert.That(state.DamageTaken, Is.EqualTo(130));
    }

    [Test]
    public void HealingRejectsInvalidDuplicateMismatchedAndUnneededHealing()
    {
        var playerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var otherPlayerId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var correlationId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var state = PlayerStateMachine.Create(playerId);

        var mismatch = state.ApplyHealing(new HealingEvent(Guid.NewGuid(), otherPlayerId, 5, "medkit"));
        var invalid = state.ApplyHealing(new HealingEvent(Guid.NewGuid(), playerId, 0, "medkit"));
        var notNeeded = state.ApplyHealing(new HealingEvent(Guid.NewGuid(), playerId, 5, "medkit"));
        state.ApplyDamage(new TechnicalDamageEvent(Guid.NewGuid(), null, playerId, 10, "technical"));
        var accepted = state.ApplyHealing(new HealingEvent(correlationId, playerId, 5, "medkit"));
        var duplicate = state.ApplyHealing(new HealingEvent(correlationId, playerId, 5, "medkit"));

        Assert.That(mismatch.Accepted, Is.False);
        Assert.That(mismatch.Reason, Is.EqualTo("healing_target_mismatch"));
        Assert.That(invalid.Accepted, Is.False);
        Assert.That(invalid.Reason, Is.EqualTo("healing_invalid"));
        Assert.That(notNeeded.Accepted, Is.False);
        Assert.That(notNeeded.Reason, Is.EqualTo("healing_not_needed"));
        Assert.That(accepted.Accepted, Is.True);
        Assert.That(duplicate.Accepted, Is.False);
        Assert.That(duplicate.Reason, Is.EqualTo("healing_duplicate"));
        Assert.That(state.DamageTaken, Is.EqualTo(5));
    }
}
