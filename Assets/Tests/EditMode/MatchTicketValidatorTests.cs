using System;
using System.Threading;
using System.Threading.Tasks;
using LH.Main.Unity.Networking;
using LH.Main.Unity.Server;
using NUnit.Framework;

public sealed class MatchTicketValidatorTests
{
    private static readonly Guid MatchId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid PlayerId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Test]
    public async Task ValidateAsyncSendsSharedKeyHeaderAndAcceptsValidResponse()
    {
        var payload = new MatchAdmissionPayload(MatchId, PlayerId, "game-server-1", "secret-ticket");
        var sender = new RecordingTicketValidationSender(200, "{\"valid\":true,\"matchId\":\"" + MatchId + "\",\"playerId\":\"" + PlayerId + "\"}");
        var validator = new MatchTicketValidator("http://backend:8080", "shared-key", TimeSpan.FromSeconds(3), sender);

        MatchAdmissionResult result = await validator.ValidateAsync(payload, CancellationToken.None);

        Assert.That(result.IsAccepted, Is.True);
        Assert.That(result.MatchId, Is.EqualTo(MatchId));
        Assert.That(result.PlayerId, Is.EqualTo(PlayerId));
        Assert.That(sender.Path, Is.EqualTo("/internal/v1/matches/tickets/validate"));
        Assert.That(sender.Endpoint.ToString(), Is.EqualTo("http://backend:8080/internal/v1/matches/tickets/validate"));
        Assert.That(sender.SharedKeyHeader, Is.EqualTo("shared-key"));
        Assert.That(sender.Body, Does.Contain("\"ticket\":\"secret-ticket\""));
        Assert.That(sender.Body, Does.Contain("\"matchId\":\"" + MatchId + "\""));
        Assert.That(sender.Body, Does.Contain("\"playerId\":\"" + PlayerId + "\""));
        Assert.That(sender.Body, Does.Contain("\"serverId\":\"game-server-1\""));
    }

    [Test]
    public async Task ValidateAsyncCreatedFromConfigSendsConfiguredEndpointAndSharedKey()
    {
        var payload = new MatchAdmissionPayload(MatchId, PlayerId, "game-server-from-config", "secret-ticket");
        var sender = new RecordingTicketValidationSender(200, "{\"valid\":true,\"matchId\":\"" + MatchId + "\",\"playerId\":\"" + PlayerId + "\"}");
        var validator = new MatchTicketValidator(CreateConfig(), sender);

        MatchAdmissionResult result = await validator.ValidateAsync(payload, CancellationToken.None);

        Assert.That(result.IsAccepted, Is.True);
        Assert.That(sender.Endpoint.ToString(), Is.EqualTo("http://configured-backend:9090/internal/v1/matches/tickets/validate"));
        Assert.That(sender.SharedKeyHeader, Is.EqualTo("configured-shared-key"));
    }

    [Test]
    public void CanBeCreatedFromConfigWithoutExternalSender()
    {
        Assert.DoesNotThrow(() => new MatchTicketValidator(CreateConfig()));
    }

    [Test]
    public async Task ValidateAsyncCreatedFromConfigAppliesConfiguredTimeout()
    {
        var sender = new RecordingTicketValidationSender(200, "{}") { WaitForCancellation = true };
        var validator = new MatchTicketValidator(CreateConfig(), sender);

        MatchAdmissionResult result = await validator.ValidateAsync(CreatePayload(), CancellationToken.None);

        Assert.That(result.IsAccepted, Is.False);
        Assert.That(result.Reason, Is.EqualTo("validation_timeout"));
    }

    [Test]
    public async Task ValidateAsyncRejectsInvalidTicketResponse()
    {
        var validator = CreateValidator(new RecordingTicketValidationSender(200, "{\"valid\":false}"));

        MatchAdmissionResult result = await validator.ValidateAsync(CreatePayload(), CancellationToken.None);

        Assert.That(result.IsAccepted, Is.False);
        Assert.That(result.Reason, Is.EqualTo("ticket_invalid"));
    }

    [Test]
    public async Task ValidateAsyncRejectsServerKeyFailure()
    {
        var validator = CreateValidator(new RecordingTicketValidationSender(401, "{\"valid\":false}"));

        MatchAdmissionResult result = await validator.ValidateAsync(CreatePayload(), CancellationToken.None);

        Assert.That(result.IsAccepted, Is.False);
        Assert.That(result.Reason, Is.EqualTo("server_key_rejected"));
    }

    [Test]
    public async Task ValidateAsyncRejectsTimedOutValidation()
    {
        var validator = CreateValidator(new RecordingTicketValidationSender(200, "{}") { ThrowTimeout = true });

        MatchAdmissionResult result = await validator.ValidateAsync(CreatePayload(), CancellationToken.None);

        Assert.That(result.IsAccepted, Is.False);
        Assert.That(result.Reason, Is.EqualTo("validation_timeout"));
    }

    [Test]
    public async Task ValidateAsyncRejectsAcceptedResponseForDifferentAdmission()
    {
        var otherPlayerId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var sender = new RecordingTicketValidationSender(200, "{\"valid\":true,\"matchId\":\"" + MatchId + "\",\"playerId\":\"" + otherPlayerId + "\"}");
        var validator = CreateValidator(sender);

        MatchAdmissionResult result = await validator.ValidateAsync(CreatePayload(), CancellationToken.None);

        Assert.That(result.IsAccepted, Is.False);
        Assert.That(result.Reason, Is.EqualTo("validation_mismatch"));
    }

    [Test]
    public async Task ValidateAsyncDiagnosticsIncludeContextButNotSecrets()
    {
        string diagnostic = string.Empty;
        var sender = new RecordingTicketValidationSender(500, "{\"valid\":false}");
        var validator = new MatchTicketValidator("http://backend:8080", "shared-key", TimeSpan.FromSeconds(3), sender, message => diagnostic = message);

        MatchAdmissionResult result = await validator.ValidateAsync(CreatePayload(), CancellationToken.None);

        Assert.That(result.IsAccepted, Is.False);
        Assert.That(diagnostic, Does.Contain("backend"));
        Assert.That(diagnostic, Does.Contain("server_error"));
        Assert.That(diagnostic, Does.Contain(MatchId.ToString()));
        Assert.That(diagnostic, Does.Contain(PlayerId.ToString()));
        Assert.That(diagnostic, Does.Contain("game-server-1"));
        Assert.That(diagnostic, Does.Not.Contain("secret-ticket"));
        Assert.That(diagnostic, Does.Not.Contain("shared-key"));
    }

    private static MatchTicketValidator CreateValidator(RecordingTicketValidationSender sender)
    {
        return new MatchTicketValidator("http://backend:8080", "shared-key", TimeSpan.FromSeconds(3), sender);
    }

    private static MatchAdmissionPayload CreatePayload()
    {
        return new MatchAdmissionPayload(MatchId, PlayerId, "game-server-1", "secret-ticket");
    }

    private static GameServerConfig CreateConfig()
    {
        return new GameServerConfig(
            "game-server-from-config",
            8081,
            7770,
            "127.0.0.1",
            7770,
            "http://configured-backend:9090",
            "configured-shared-key",
            1);
    }

    private sealed class RecordingTicketValidationSender : IMatchTicketValidationSender
    {
        private readonly int _statusCode;
        private readonly string _responseBody;

        public RecordingTicketValidationSender(int statusCode, string responseBody)
        {
            _statusCode = statusCode;
            _responseBody = responseBody;
        }

        public string Path { get; private set; }
        public Uri Endpoint { get; private set; }
        public string SharedKeyHeader { get; private set; }
        public string Body { get; private set; }
        public bool ThrowTimeout { get; set; }
        public bool WaitForCancellation { get; set; }

        public async Task<MatchTicketValidationResponse> SendAsync(Uri endpoint, string sharedKey, string body, CancellationToken cancellationToken)
        {
            Endpoint = endpoint;
            Path = endpoint.AbsolutePath;
            SharedKeyHeader = sharedKey;
            Body = body;

            if (ThrowTimeout)
                throw new OperationCanceledException(cancellationToken);

            if (WaitForCancellation)
                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);

            return new MatchTicketValidationResponse(_statusCode, _responseBody);
        }
    }
}
