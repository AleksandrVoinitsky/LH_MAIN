using System;
using LH.Main.Unity.Client;
using LH.Main.Unity.Networking;
using NUnit.Framework;

public sealed class MatchConnectionClientTests
{
    [Test]
    public void BuildAdmissionPayloadUsesAssignmentAndPlayerId()
    {
        var matchId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var playerId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var assignment = new MatchAssignment(matchId, "game-server-1", "127.0.0.1", 7771, "ticket-value");

        string payloadJson = MatchConnectionClient.BuildAdmissionPayload(assignment, playerId).ToJson();

        Assert.That(payloadJson, Does.Contain("\"matchId\":\"11111111-1111-1111-1111-111111111111\""));
        Assert.That(payloadJson, Does.Contain("\"playerId\":\"22222222-2222-2222-2222-222222222222\""));
        Assert.That(payloadJson, Does.Contain("\"serverId\":\"game-server-1\""));
        Assert.That(payloadJson, Does.Contain("\"ticket\":\"ticket-value\""));
    }

    [Test]
    public void AdmissionResultBroadcastCarriesAcceptedStateWithoutPayloadSecrets()
    {
        var broadcast = new GameServerAdmissionResultFishNetBroadcast
        {
            Accepted = false,
            Reason = "invalid_ticket"
        };

        Assert.That(broadcast.Accepted, Is.False);
        Assert.That(broadcast.Reason, Is.EqualTo("invalid_ticket"));
    }
}
