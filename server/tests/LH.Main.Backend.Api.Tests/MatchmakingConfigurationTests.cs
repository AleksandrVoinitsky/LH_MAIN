using LH.Main.Backend.Api.Matchmaking;
using Xunit;

namespace LH.Main.Backend.Api.Tests;

public sealed class MatchmakingConfigurationTests
{
    [Fact]
    public void GameServerOptionsRequireSharedKeyTicketLifetimeAndSlots()
    {
        var options = new GameServerOptions();

        var errors = options.Validate();

        Assert.Contains("GameServers:SharedKey is required.", errors);
        Assert.Contains("GameServers:TicketLifetimeSeconds must be greater than zero.", errors);
        Assert.Contains("At least one GameServers:Slots entry is required.", errors);
    }

    [Fact]
    public void GameServerOptionsAcceptLocalTwoSlotConfiguration()
    {
        var options = new GameServerOptions
        {
            SharedKey = "local-shared-game-server-key",
            TicketLifetimeSeconds = 60,
            Slots =
            [
                new GameServerSlotOptions { ServerId = "game-server-1", PublicHost = "localhost", PublicPort = 7771 },
                new GameServerSlotOptions { ServerId = "game-server-2", PublicHost = "localhost", PublicPort = 7772 }
            ]
        };

        Assert.Empty(options.Validate());
        Assert.Equal(TimeSpan.FromSeconds(60), options.GetTicketLifetime());
    }
}
