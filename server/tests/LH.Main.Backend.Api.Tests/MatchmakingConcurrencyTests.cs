using LH.Main.Backend.Api.Matchmaking;
using LH.Main.Backend.Api.Persistence;
using LH.Main.Backend.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace LH.Main.Backend.Api.Tests;

[Collection(PostgreSqlCollection.Name)]
public sealed class MatchmakingConcurrencyTests(PostgreSqlFixture database)
{
    [Fact]
    public async Task ConcurrentEnqueueRequestsReserveSingleSlotOnlyOnce()
    {
        await using (var setupContext = await CreateCleanDbContextAsync())
        {
            await CreateUserAsync(setupContext, FirstPlayerId, "concurrent-one");
            await CreateUserAsync(setupContext, SecondPlayerId, "concurrent-two");
            await SeedSlotAsync(setupContext);
        }

        await using var firstContext = CreateDbContext();
        await using var secondContext = CreateDbContext();
        var firstService = CreateService(firstContext);
        var secondService = CreateService(secondContext);

        var tasks = new[]
        {
            Task.Run(() => firstService.EnqueueAsync(FirstPlayerId, CancellationToken.None)),
            Task.Run(() => secondService.EnqueueAsync(SecondPlayerId, CancellationToken.None))
        };

        var results = await Task.WhenAll(tasks);

        Assert.Equal(1, results.Count(result => result.Status == "assigned"));
        Assert.Equal(1, results.Count(result => result.Status == "queued"));
        Assert.Equal("game-server-1", results.Single(result => result.Status == "assigned").Assignment!.ServerId);

        await using var verificationContext = CreateDbContext();
        Assert.Equal(1, await verificationContext.Matches.CountAsync());
        var slot = await verificationContext.GameServerSlots.SingleAsync(slot => slot.ServerId == "game-server-1");
        Assert.Equal(GameServerSlotStatuses.Occupied, slot.Status);
        Assert.Equal(await verificationContext.Matches.Select(match => match.Id).SingleAsync(), slot.CurrentMatchId);
    }

    private static readonly Guid FirstPlayerId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static readonly Guid SecondPlayerId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private async Task<AppDbContext> CreateCleanDbContextAsync()
    {
        var context = CreateDbContext();
        await context.Database.MigrateAsync();
        await context.MatchTickets.ExecuteDeleteAsync();
        await context.MatchQueueEntries.ExecuteDeleteAsync();
        await context.Matches.ExecuteDeleteAsync();
        await context.GameServerSlots.ExecuteDeleteAsync();
        await context.PlayerProfiles.ExecuteDeleteAsync();
        await context.PasswordCredentials.ExecuteDeleteAsync();
        await context.Users.ExecuteDeleteAsync();
        return context;
    }

    private AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(database.ConnectionString)
            .Options;

        return new AppDbContext(options);
    }

    private static MatchmakingService CreateService(AppDbContext context)
    {
        var clock = new FixedClock(DateTimeOffset.Parse("2026-09-20T00:00:00Z"));
        return new MatchmakingService(context, new TicketService(clock), clock, Options.Create(LocalOptions()));
    }

    private static async Task CreateUserAsync(AppDbContext context, Guid playerId, string username)
    {
        var now = DateTimeOffset.Parse("2026-09-20T00:00:00Z");
        context.Users.Add(new User { Id = playerId, NormalizedUsername = username.ToUpperInvariant(), CreatedAtUtc = now });
        context.PlayerProfiles.Add(new PlayerProfile { UserId = playerId, Username = username, CreatedAtUtc = now });
        await context.SaveChangesAsync();
    }

    private static async Task SeedSlotAsync(AppDbContext context)
    {
        var now = DateTimeOffset.Parse("2026-09-20T00:00:00Z");
        context.GameServerSlots.Add(new GameServerSlot
        {
            ServerId = "game-server-1",
            PublicHost = "localhost",
            PublicPort = 7771,
            Status = GameServerSlotStatuses.Available,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        await context.SaveChangesAsync();
    }

    private static GameServerOptions LocalOptions() => new()
    {
        SharedKey = "local-shared-game-server-key",
        TicketLifetimeSeconds = 60,
        Slots =
        [
            new GameServerSlotOptions { ServerId = "game-server-1", PublicHost = "localhost", PublicPort = 7771 }
        ]
    };

    private sealed class FixedClock(DateTimeOffset now) : ISystemClock
    {
        public DateTimeOffset UtcNow => now;
    }
}
