using System;
using System.IO;
using LH.Main.Unity.Networking;
using NUnit.Framework;

public sealed class MatchAdmissionPayloadTests
{
    [Test]
    public void TryParseAcceptsValidPayload()
    {
        var matchId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var playerId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        string json = "{\"matchId\":\"" + matchId + "\",\"playerId\":\"" + playerId + "\",\"serverId\":\"game-server-1\",\"ticket\":\"secret-ticket\"}";

        bool parsed = MatchAdmissionPayload.TryParse(json, out MatchAdmissionPayload payload, out string error);

        Assert.That(parsed, Is.True, error);
        Assert.That(payload.MatchId, Is.EqualTo(matchId));
        Assert.That(payload.PlayerId, Is.EqualTo(playerId));
        Assert.That(payload.ServerId, Is.EqualTo("game-server-1"));
        Assert.That(payload.Ticket, Is.EqualTo("secret-ticket"));
    }

    [Test]
    public void TryParseRejectsMalformedJson()
    {
        bool parsed = MatchAdmissionPayload.TryParse("{", out _, out string error);

        Assert.That(parsed, Is.False);
        Assert.That(error, Is.EqualTo("payload_malformed"));
    }

    [Test]
    public void TryParseRejectsMissingPayload()
    {
        bool parsed = MatchAdmissionPayload.TryParse(" ", out _, out string error);

        Assert.That(parsed, Is.False);
        Assert.That(error, Is.EqualTo("payload_required"));
    }

    [Test]
    public void TryParseRejectsTrailingGarbageAfterObject()
    {
        string json = "{\"matchId\":\"11111111-1111-1111-1111-111111111111\",\"playerId\":\"22222222-2222-2222-2222-222222222222\",\"serverId\":\"game-server-1\",\"ticket\":\"secret-ticket\"} junk";

        bool parsed = MatchAdmissionPayload.TryParse(json, out _, out string error);

        Assert.That(parsed, Is.False);
        Assert.That(error, Is.EqualTo("payload_malformed"));
    }

    [Test]
    public void TryParseRejectsEmptyTicket()
    {
        string json = "{\"matchId\":\"11111111-1111-1111-1111-111111111111\",\"playerId\":\"22222222-2222-2222-2222-222222222222\",\"serverId\":\"game-server-1\",\"ticket\":\"\"}";

        bool parsed = MatchAdmissionPayload.TryParse(json, out _, out string error);

        Assert.That(parsed, Is.False);
        Assert.That(error, Is.EqualTo("ticket_required"));
    }

    [Test]
    public void TryParseRejectsWrongGuidFormat()
    {
        string json = "{\"matchId\":\"not-a-guid\",\"playerId\":\"22222222-2222-2222-2222-222222222222\",\"serverId\":\"game-server-1\",\"ticket\":\"secret-ticket\"}";

        bool parsed = MatchAdmissionPayload.TryParse(json, out _, out string error);

        Assert.That(parsed, Is.False);
        Assert.That(error, Is.EqualTo("match_id_invalid"));
    }

    [Test]
    public void TryParseRejectsWrongPlayerIdFormat()
    {
        string json = "{\"matchId\":\"11111111-1111-1111-1111-111111111111\",\"playerId\":\"not-a-guid\",\"serverId\":\"game-server-1\",\"ticket\":\"secret-ticket\"}";

        bool parsed = MatchAdmissionPayload.TryParse(json, out _, out string error);

        Assert.That(parsed, Is.False);
        Assert.That(error, Is.EqualTo("player_id_invalid"));
    }

    [Test]
    public void TryParseRejectsMissingServerId()
    {
        string json = "{\"matchId\":\"11111111-1111-1111-1111-111111111111\",\"playerId\":\"22222222-2222-2222-2222-222222222222\",\"serverId\":\"\",\"ticket\":\"secret-ticket\"}";

        bool parsed = MatchAdmissionPayload.TryParse(json, out _, out string error);

        Assert.That(parsed, Is.False);
        Assert.That(error, Is.EqualTo("server_id_required"));
    }

    [Test]
    public void ToJsonWritesAdmissionPayloadFields()
    {
        var payload = new MatchAdmissionPayload(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            "game-server-1",
            "secret-ticket");

        string json = payload.ToJson();

        Assert.That(json, Does.Contain("\"matchId\":\"11111111-1111-1111-1111-111111111111\""));
        Assert.That(json, Does.Contain("\"playerId\":\"22222222-2222-2222-2222-222222222222\""));
        Assert.That(json, Does.Contain("\"serverId\":\"game-server-1\""));
        Assert.That(json, Does.Contain("\"ticket\":\"secret-ticket\""));
    }

    [Test]
    public void SourceDoesNotReferenceUnityEngineOrFishNet()
    {
        string source = File.ReadAllText(Path.Combine(
            Directory.GetCurrentDirectory(),
            "Assets",
            "Scripts",
            "Networking",
            "MatchAdmissionPayload.cs"));

        Assert.That(source, Does.Not.Contain("UnityEngine"));
        Assert.That(source, Does.Not.Contain("FishNet"));
    }
}

public sealed class MatchAdmissionResultTests
{
    [Test]
    public void AcceptedCreatesAcceptedResult()
    {
        var matchId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var playerId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        MatchAdmissionResult result = MatchAdmissionResult.Accepted(matchId, playerId);

        Assert.That(result.IsAccepted, Is.True);
        Assert.That(result.MatchId, Is.EqualTo(matchId));
        Assert.That(result.PlayerId, Is.EqualTo(playerId));
        Assert.That(result.Reason, Is.EqualTo(string.Empty));
    }

    [Test]
    public void RejectedCreatesRejectedResult()
    {
        MatchAdmissionResult result = MatchAdmissionResult.Rejected("ticket_required");

        Assert.That(result.IsAccepted, Is.False);
        Assert.That(result.MatchId, Is.EqualTo(Guid.Empty));
        Assert.That(result.PlayerId, Is.EqualTo(Guid.Empty));
        Assert.That(result.Reason, Is.EqualTo("ticket_required"));
    }
}
