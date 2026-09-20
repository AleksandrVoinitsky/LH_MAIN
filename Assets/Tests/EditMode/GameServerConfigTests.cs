using LH.Main.Unity.Server;
using NUnit.Framework;

public sealed class GameServerConfigTests
{
    [Test]
    public void ValidateRequiresBackendAdmissionSettings()
    {
        var config = new GameServerConfig("game-server-1", 8081, 7771, "localhost", 7771, string.Empty, string.Empty, 0);

        bool valid = config.Validate(out string error);

        Assert.That(valid, Is.False);
        Assert.That(error, Is.EqualTo("GAME_SERVER_BACKEND_BASE_URL is required."));
    }

    [Test]
    public void ValidateRequiresSharedKey()
    {
        var config = new GameServerConfig("game-server-1", 8081, 7771, "localhost", 7771, "http://backend:8080", string.Empty, 3);

        bool valid = config.Validate(out string error);

        Assert.That(valid, Is.False);
        Assert.That(error, Is.EqualTo("GAME_SERVER_SHARED_KEY is required."));
    }

    [Test]
    public void ValidateRequiresPositiveTicketValidationTimeout()
    {
        var config = new GameServerConfig("game-server-1", 8081, 7771, "localhost", 7771, "http://backend:8080", "shared-key", 0);

        bool valid = config.Validate(out string error);
        Assert.That(valid, Is.False);
        Assert.That(error, Is.EqualTo("GAME_SERVER_TICKET_VALIDATION_TIMEOUT_SECONDS must be greater than zero."));
    }

    [Test]
    public void ValidateAcceptsCompleteBackendAdmissionSettings()
    {
        var config = new GameServerConfig("game-server-1", 8081, 7771, "localhost", 7771, "http://backend:8080", "shared-key", 3);

        bool valid = config.Validate(out string error);

        Assert.That(valid, Is.True, error);
        Assert.That(config.BackendBaseUrl, Is.EqualTo("http://backend:8080"));
        Assert.That(config.SharedKey, Is.EqualTo("shared-key"));
        Assert.That(config.TicketValidationTimeoutSeconds, Is.EqualTo(3));
    }
}
