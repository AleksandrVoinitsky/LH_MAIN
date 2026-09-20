using System;
using LH.Main.Unity.Gameplay;
using NUnit.Framework;

public sealed class MatchResultBuilderTests
{
    [Test]
    public void BuildCreatesParticipantOutcomesAndRewardCodes()
    {
        var matchId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var playerId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var players = new[] { new PlayerResultSnapshot(playerId, PlayerLifeState.Extracted, 180, 25, 10) };

        MatchResultPayload payload = MatchResultBuilder.Build(matchId, "game-server-1", players, new DateTime(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc));

        Assert.That(payload.MatchId, Is.EqualTo(matchId));
        Assert.That(payload.ServerId, Is.EqualTo("game-server-1"));
        Assert.That(payload.Participants[0].Outcome, Is.EqualTo("extracted"));
        Assert.That(payload.Participants[0].RewardCode, Is.EqualTo("phase05_test_reward"));
        Assert.That(payload.ResultId, Is.Not.EqualTo(Guid.Empty));
    }

    [Test]
    public void BuildAssignsPhase05RewardCodeToEveryMixedOutcome()
    {
        var matchId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var players = new[]
        {
            new PlayerResultSnapshot(Guid.Parse("22222222-2222-2222-2222-222222222222"), PlayerLifeState.Extracted, 180, 25, 10),
            new PlayerResultSnapshot(Guid.Parse("33333333-3333-3333-3333-333333333333"), PlayerLifeState.Dead, 120, 5, 130),
            new PlayerResultSnapshot(Guid.Parse("44444444-4444-4444-4444-444444444444"), PlayerLifeState.Disconnected, 60, 0, 0)
        };

        MatchResultPayload payload = MatchResultBuilder.Build(matchId, "game-server-1", players, new DateTime(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc));

        Assert.That(payload.Participants.Count, Is.EqualTo(3));
        Assert.That(payload.Participants[0].Outcome, Is.EqualTo("extracted"));
        Assert.That(payload.Participants[1].Outcome, Is.EqualTo("dead"));
        Assert.That(payload.Participants[2].Outcome, Is.EqualTo("disconnected"));
        foreach (MatchParticipantResult participant in payload.Participants)
            Assert.That(participant.RewardCode, Is.EqualTo(MatchResultBuilder.ExtractedRewardCode));
    }

    [Test]
    public void ToJsonEscapesControlCharactersInStrings()
    {
        var payload = MatchResultPayload.EmptyForTests(Guid.Parse("11111111-1111-1111-1111-111111111111"), "server\n\t\u0001\"x\\");

        string json = payload.ToJson();

        Assert.That(json, Does.Contain("server\\n\\t\\u0001\\\"x\\\\"));
        Assert.That(json, Does.Not.Contain("server\n"));
    }
}
