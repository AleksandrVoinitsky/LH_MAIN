using LH.Main.Backend.Api.Matchmaking;
using LH.Main.Backend.Api.Persistence;
using LH.Main.Backend.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LH.Main.Backend.Api.Tests;

[Collection(PostgreSqlCollection.Name)]
public sealed class GameServerSlotSeederTests(PostgreSqlFixture database)
{
    [Fact]
    public async Task SeedAsyncUpsertsConfiguredSlotsWithoutClearingCurrentMatch()
    {
        await using var context = CreateDbContext();
        await context.Database.MigrateAsync();
        await context.GameServerSlots.ExecuteDeleteAsync();

        var matchId = Guid.NewGuid();
        context.GameServerSlots.Add(new GameServerSlot
        {
            ServerId = "game-server-1",
            PublicHost = "old-host",
            PublicPort = 1111,
            Status = GameServerSlotStatuses.Occupied,
            CurrentMatchId = matchId,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });
        await context.SaveChangesAsync();

        await GameServerSlotSeeder.SeedAsync(context, LocalOptions(), CancellationToken.None);

        var slot = await context.GameServerSlots.SingleAsync(s => s.ServerId == "game-server-1");
        Assert.Equal("localhost", slot.PublicHost);
        Assert.Equal(7771, slot.PublicPort);
        Assert.Equal(GameServerSlotStatuses.Occupied, slot.Status);
        Assert.Equal(matchId, slot.CurrentMatchId);
    }

    [Fact]
    public async Task SeedAsyncInsertsConfiguredSlots()
    {
        await using var context = CreateDbContext();
        await context.Database.MigrateAsync();
        await context.GameServerSlots.ExecuteDeleteAsync();

        await GameServerSlotSeeder.SeedAsync(context, LocalOptions(), CancellationToken.None);

        var slots = await context.GameServerSlots.OrderBy(slot => slot.ServerId).ToArrayAsync();
        Assert.Collection(
            slots,
            slot =>
            {
                Assert.Equal("game-server-1", slot.ServerId);
                Assert.Equal("localhost", slot.PublicHost);
                Assert.Equal(7771, slot.PublicPort);
                Assert.Equal(GameServerSlotStatuses.Available, slot.Status);
                Assert.Null(slot.CurrentMatchId);
            },
            slot =>
            {
                Assert.Equal("game-server-2", slot.ServerId);
                Assert.Equal("localhost", slot.PublicHost);
                Assert.Equal(7772, slot.PublicPort);
                Assert.Equal(GameServerSlotStatuses.Available, slot.Status);
                Assert.Null(slot.CurrentMatchId);
            });
    }

    private AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(database.ConnectionString)
            .Options;

        return new AppDbContext(options);
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
}
