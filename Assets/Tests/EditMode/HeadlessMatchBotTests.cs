using System;
using LH.Main.Unity.Load;
using NUnit.Framework;

public sealed class HeadlessMatchBotTests
{
    [Test]
    public void GetMoveForElapsedSecondsFollowsDeterministicRoute()
    {
        AssertMove(0d, 0f, 1f);
        AssertMove(9.99d, 0f, 1f);
        AssertMove(10d, 1f, 0f);
        AssertMove(20d, 0f, -1f);
        AssertMove(30d, -1f, 0f);
        AssertMove(40d, 0f, 1f);
    }

    [Test]
    public void GetPhase05OutcomeForClientIndexSplitsClientsIntoThreeDeterministicPaths()
    {
        Assert.That(HeadlessMatchBot.GetPhase05OutcomeForClientIndex(0), Is.EqualTo(BotPhase05Outcome.Extracted));
        Assert.That(HeadlessMatchBot.GetPhase05OutcomeForClientIndex(1), Is.EqualTo(BotPhase05Outcome.Dead));
        Assert.That(HeadlessMatchBot.GetPhase05OutcomeForClientIndex(2), Is.EqualTo(BotPhase05Outcome.Disconnected));
        Assert.That(HeadlessMatchBot.GetPhase05OutcomeForClientIndex(3), Is.EqualTo(BotPhase05Outcome.Extracted));
    }

    [Test]
    public void GetPhase06ActionForElapsedSecondsFollowsDeterministicActionWindows()
    {
        Assert.That(HeadlessMatchBot.GetPhase06ActionForElapsedSeconds(1d), Is.EqualTo(BotPhase06Action.PickupLoot));
        Assert.That(HeadlessMatchBot.GetPhase06ActionForElapsedSeconds(3d), Is.EqualTo(BotPhase06Action.FireWeapon));
        Assert.That(HeadlessMatchBot.GetPhase06ActionForElapsedSeconds(5d), Is.EqualTo(BotPhase06Action.ReloadWeapon));
        Assert.That(HeadlessMatchBot.GetPhase06ActionForElapsedSeconds(7d), Is.EqualTo(BotPhase06Action.ThrowGrenade));
        Assert.That(HeadlessMatchBot.GetPhase06ActionForElapsedSeconds(9d), Is.EqualTo(BotPhase06Action.UseMedItem));
    }

    private static void AssertMove(double elapsedSeconds, float expectedX, float expectedY)
    {
        BotMove move = HeadlessMatchBot.GetMoveForElapsedSeconds(elapsedSeconds);

        Assert.That(move.MoveX, Is.EqualTo(expectedX));
        Assert.That(move.MoveY, Is.EqualTo(expectedY));
    }
}
