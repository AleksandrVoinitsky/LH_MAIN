using LH.Main.Backend.Api.Matchmaking;
using Xunit;

namespace LH.Main.Backend.Api.Tests;

public sealed class TicketServiceTests
{
    [Fact]
    public void IssueCreatesOpaqueTicketAndHashWithExpectedExpiry()
    {
        var now = DateTimeOffset.Parse("2026-09-20T00:00:00Z");
        var clock = new FixedClock(now);
        var service = new TicketService(clock);

        var ticket = service.Issue(Guid.NewGuid(), Guid.NewGuid(), "game-server-1", TimeSpan.FromSeconds(60));

        Assert.False(string.IsNullOrWhiteSpace(ticket.PlaintextTicket));
        Assert.DoesNotContain("=", ticket.PlaintextTicket);
        Assert.DoesNotContain("+", ticket.PlaintextTicket);
        Assert.DoesNotContain("/", ticket.PlaintextTicket);
        Assert.NotEqual(ticket.PlaintextTicket, ticket.TicketHash);
        Assert.Equal(64, ticket.TicketHash.Length);
        Assert.Equal(now.AddSeconds(60), ticket.ExpiresAtUtc);
    }

    [Fact]
    public void IssueCreatesUniqueTicketsAndHashes()
    {
        var service = new TicketService(new FixedClock(DateTimeOffset.Parse("2026-09-20T00:00:00Z")));

        var first = service.Issue(Guid.NewGuid(), Guid.NewGuid(), "game-server-1", TimeSpan.FromSeconds(60));
        var second = service.Issue(Guid.NewGuid(), Guid.NewGuid(), "game-server-1", TimeSpan.FromSeconds(60));

        Assert.NotEqual(first.PlaintextTicket, second.PlaintextTicket);
        Assert.NotEqual(first.TicketHash, second.TicketHash);
    }

    [Fact]
    public void HashReturnsStableSha256HexForPlaintextTicket()
    {
        var service = new TicketService(new FixedClock(DateTimeOffset.Parse("2026-09-20T00:00:00Z")));

        var first = service.Hash("opaque-ticket");
        var second = service.Hash("opaque-ticket");

        Assert.Equal(first, second);
        Assert.Equal("f13ee9b2677f32949e09f039d588b7b401291659f611ee9a1881283f5a3ba481", first);
    }

    private sealed class FixedClock(DateTimeOffset now) : ISystemClock
    {
        public DateTimeOffset UtcNow => now;
    }
}
