using LH.Main.Backend.Api.Matchmaking;
using LH.Main.Backend.Api.Persistence;
using LH.Main.Backend.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace LH.Main.Backend.Api.Tests;

[Collection(PostgreSqlCollection.Name)]
public sealed class MatchmakingServiceTests(PostgreSqlFixture database)
{
    [Fact]
    public async Task EnqueueAssignsAvailableSlotAndIsIdempotentForSamePlayer()
    {
        await using var context = await CreateCleanDbContextAsync();
        var playerId = await CreateUserAsync(context, "assigns-idempotent");
        await SeedSlotsAsync(context, "game-server-1", "game-server-2");
        var service = CreateService(context);

        var first = await service.EnqueueAsync(playerId, CancellationToken.None);
        var second = await service.EnqueueAsync(playerId, CancellationToken.None);

        Assert.Equal("assigned", first.Status);
        Assert.NotNull(first.Assignment);
        Assert.Contains(first.Assignment.ServerId, new[] { "game-server-1", "game-server-2" });
        Assert.False(string.IsNullOrWhiteSpace(first.Assignment.Ticket));
        Assert.Equal(first.Assignment.MatchId, second.Assignment!.MatchId);
        Assert.Equal(1, await context.Matches.CountAsync());
        Assert.Equal(1, await context.MatchTickets.CountAsync());
    }

    [Fact]
    public async Task EnqueueReturnsQueuedWithoutTicketWhenAllSlotsAreOccupied()
    {
        await using var context = await CreateCleanDbContextAsync();
        var playerId = await CreateUserAsync(context, "no-capacity");
        await SeedSlotsAsync(context, "game-server-1", "game-server-2");
        await context.GameServerSlots.ExecuteUpdateAsync(setters => setters
            .SetProperty(slot => slot.Status, GameServerSlotStatuses.Occupied)
            .SetProperty(slot => slot.CurrentMatchId, Guid.NewGuid()));
        var service = CreateService(context);

        var response = await service.EnqueueAsync(playerId, CancellationToken.None);

        Assert.Equal("queued", response.Status);
        Assert.NotNull(response.QueuedAtUtc);
        Assert.Null(response.Assignment);
        Assert.Empty(await context.MatchTickets.ToArrayAsync());
    }

    [Fact]
    public async Task CancelQueuedEntryIsIdempotent()
    {
        await using var context = await CreateCleanDbContextAsync();
        var playerId = await CreateUserAsync(context, "cancel-queued");
        await SeedSlotsAsync(context, "game-server-1");
        await context.GameServerSlots.ExecuteUpdateAsync(setters => setters
            .SetProperty(slot => slot.Status, GameServerSlotStatuses.Occupied)
            .SetProperty(slot => slot.CurrentMatchId, Guid.NewGuid()));
        var service = CreateService(context);
        await service.EnqueueAsync(playerId, CancellationToken.None);

        var first = await service.CancelAsync(playerId, CancellationToken.None);
        var second = await service.CancelAsync(playerId, CancellationToken.None);

        Assert.False(first.ConflictAssigned);
        Assert.False(second.ConflictAssigned);
        Assert.Equal("cancelled", first.Response.Status);
        Assert.Equal("cancelled", second.Response.Status);
        Assert.Equal(1, await context.MatchQueueEntries.CountAsync(entry => entry.Status == MatchQueueEntryStatuses.Cancelled));
    }

    [Fact]
    public async Task CancelAssignedEntryConflictsAndLeavesSlotOccupied()
    {
        await using var context = await CreateCleanDbContextAsync();
        var playerId = await CreateUserAsync(context, "cancel-assigned");
        await SeedSlotsAsync(context, "game-server-1");
        var service = CreateService(context);
        var assignment = await service.EnqueueAsync(playerId, CancellationToken.None);

        var result = await service.CancelAsync(playerId, CancellationToken.None);

        Assert.Equal("assigned", assignment.Status);
        Assert.True(result.ConflictAssigned);
        Assert.Equal("assigned", result.Response.Status);
        var slot = await context.GameServerSlots.SingleAsync(slot => slot.ServerId == "game-server-1");
        Assert.Equal(GameServerSlotStatuses.Occupied, slot.Status);
        Assert.Equal(assignment.Assignment!.MatchId, slot.CurrentMatchId);
    }

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

    private static MatchmakingService CreateService(AppDbContext context, DateTimeOffset? now = null)
    {
        var clock = new FixedClock(now ?? DateTimeOffset.Parse("2026-09-20T00:00:00Z"));
        return new MatchmakingService(context, new TicketService(clock), clock, Options.Create(LocalOptions()));
    }

    private static async Task<Guid> CreateUserAsync(AppDbContext context, string username)
    {
        var playerId = Guid.NewGuid();
        var now = DateTimeOffset.Parse("2026-09-20T00:00:00Z");
        context.Users.Add(new User { Id = playerId, NormalizedUsername = username.ToUpperInvariant(), CreatedAtUtc = now });
        context.PlayerProfiles.Add(new PlayerProfile { UserId = playerId, Username = username, CreatedAtUtc = now });
        await context.SaveChangesAsync();
        return playerId;
    }

    private static async Task SeedSlotsAsync(AppDbContext context, params string[] serverIds)
    {
        var now = DateTimeOffset.Parse("2026-09-20T00:00:00Z");
        for (var index = 0; index < serverIds.Length; index++)
        {
            context.GameServerSlots.Add(new GameServerSlot
            {
                ServerId = serverIds[index],
                PublicHost = "localhost",
                PublicPort = 7771 + index,
                Status = GameServerSlotStatuses.Available,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
        }

        await context.SaveChangesAsync();
    }

    private static GameServerOptions LocalOptions() => new()
    {
        SharedKey = "local-shared-game-server-key",
        TicketLifetimeSeconds = 60,
        Slots =
        [
            new GameServerSlotOptions { ServerId = "game-server-1", PublicHost = "localhost", PublicPort = 7771 },
            new GameServerSlotOptions { ServerId = "game-server-2", PublicHost = "localhost", PublicPort = 7772 }
        ]
    };

    private sealed class FixedClock(DateTimeOffset now) : ISystemClock
    {
        public DateTimeOffset UtcNow => now;
    }
}
