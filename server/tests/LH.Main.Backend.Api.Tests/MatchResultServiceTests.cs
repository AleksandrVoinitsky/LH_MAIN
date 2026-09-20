using LH.Main.Backend.Api.Matchmaking;
using LH.Main.Backend.Api.MatchResults;
using LH.Main.Backend.Api.Persistence;
using LH.Main.Backend.Api.Persistence.Entities;
using LH.Main.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace LH.Main.Backend.Api.Tests;

[Collection(PostgreSqlCollection.Name)]
public sealed class MatchResultServiceTests(PostgreSqlFixture database)
{
    private static readonly Guid MatchId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ResultId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task DatabaseContainsMatchResultRewardTablesAfterMigration()
    {
        await using var context = CreateDbContext();
        await context.Database.MigrateAsync();

        var tableNames = await context.Database
            .SqlQueryRaw<string>("select table_name as \"Value\" from information_schema.tables where table_schema = 'public'")
            .ToListAsync();

        Assert.Contains("match_results", tableNames);
        Assert.Contains("match_result_participants", tableNames);
        Assert.Contains("reward_transactions", tableNames);
    }

    [Fact]
    public async Task SubmitAsyncRejectsInvalidServerKey()
    {
        await using var context = await CreateCleanDbContextAsync();
        var service = CreateService(context);

        var result = await service.SubmitAsync(ValidRequest(Guid.NewGuid(), "game-server-1", Guid.NewGuid()), "wrong-key", CancellationToken.None);

        Assert.True(result.Unauthorized);
        Assert.Null(result.Response);
    }

    [Fact]
    public async Task SubmitAsyncPersistsResultAndRewardsExactlyOnce()
    {
        await using var context = await CreateCleanDbContextAsync();
        var playerId = await CreateAssignedMatchAsync(context, MatchId, "game-server-1", "result-player");
        var service = CreateService(context);
        var request = ValidRequest(MatchId, "game-server-1", ResultId, playerId, "extracted");

        var first = await service.SubmitAsync(request, "local-shared-game-server-key", CancellationToken.None);
        var second = await service.SubmitAsync(request, "local-shared-game-server-key", CancellationToken.None);

        Assert.False(first.Unauthorized);
        Assert.NotNull(first.Response);
        Assert.True(first.Response.Accepted);
        Assert.False(first.Response.Duplicate);
        Assert.Equal(1, first.Response.NewRewardTransactions);
        Assert.NotNull(second.Response);
        Assert.True(second.Response.Duplicate);
        Assert.Equal(0, second.Response.NewRewardTransactions);
        Assert.Equal(1, await context.MatchResults.CountAsync());
        Assert.Equal(1, await context.MatchResultParticipants.CountAsync());
        Assert.Equal(1, await context.RewardTransactions.CountAsync());
        Assert.Equal(10, await context.RewardTransactions.Select(reward => reward.Amount).SingleAsync());
    }

    [Fact]
    public async Task SubmitAsyncRejectsSameResultIdWithConflictingPayload()
    {
        await using var context = await CreateCleanDbContextAsync();
        var playerId = await CreateAssignedMatchAsync(context, MatchId, "game-server-1", "conflict-player");
        var service = CreateService(context);
        await service.SubmitAsync(ValidRequest(MatchId, "game-server-1", ResultId, playerId, "dead"), "local-shared-game-server-key", CancellationToken.None);

        var conflict = await service.SubmitAsync(ValidRequest(MatchId, "game-server-1", ResultId, playerId, "extracted"), "local-shared-game-server-key", CancellationToken.None);

        Assert.Equal("result_conflict", conflict.ConflictCode);
        Assert.Equal(1, await context.RewardTransactions.CountAsync());
    }

    private async Task<AppDbContext> CreateCleanDbContextAsync()
    {
        var context = CreateDbContext();
        await context.Database.MigrateAsync();
        await context.RewardTransactions.ExecuteDeleteAsync();
        await context.MatchResultParticipants.ExecuteDeleteAsync();
        await context.MatchResults.ExecuteDeleteAsync();
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

    private static MatchResultService CreateService(AppDbContext context)
    {
        var clock = new FixedClock(DateTimeOffset.Parse("2026-09-20T00:00:00Z"));
        return new MatchResultService(context, clock, Options.Create(LocalOptions()));
    }

    private static async Task<Guid> CreateAssignedMatchAsync(AppDbContext context, Guid matchId, string serverId, string username)
    {
        var playerId = Guid.NewGuid();
        var now = DateTimeOffset.Parse("2026-09-20T00:00:00Z");
        context.Users.Add(new User { Id = playerId, NormalizedUsername = username.ToUpperInvariant(), CreatedAtUtc = now });
        context.PlayerProfiles.Add(new PlayerProfile { UserId = playerId, Username = username, CreatedAtUtc = now });
        context.GameServerSlots.Add(new GameServerSlot
        {
            ServerId = serverId,
            PublicHost = "localhost",
            PublicPort = 7771,
            Status = GameServerSlotStatuses.Occupied,
            CurrentMatchId = matchId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        context.Matches.Add(new MatchSession
        {
            Id = matchId,
            PlayerId = playerId,
            ServerId = serverId,
            Status = MatchSessionStatuses.TicketValidated,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        await context.SaveChangesAsync();
        return playerId;
    }

    private static MatchResultSubmissionRequest ValidRequest(Guid matchId, string serverId, Guid resultId, Guid? playerId = null, string outcome = "extracted") => new(
        matchId,
        serverId,
        resultId,
        DateTimeOffset.Parse("2026-09-20T00:05:00Z"),
        [new MatchResultParticipantRequest(playerId ?? Guid.Parse("33333333-3333-3333-3333-333333333333"), outcome, 180, 25, 10, "phase05_test_reward")]);

    private static GameServerOptions LocalOptions() => new()
    {
        SharedKey = "local-shared-game-server-key",
        TicketLifetimeSeconds = 60,
        MaxPlayersPerMatch = 64,
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
