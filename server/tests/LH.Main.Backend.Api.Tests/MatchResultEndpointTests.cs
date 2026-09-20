using System.Net;
using System.Net.Http.Json;
using LH.Main.Backend.Api.Persistence;
using LH.Main.Backend.Api.Persistence.Entities;
using LH.Main.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LH.Main.Backend.Api.Tests;

[Collection(PostgreSqlCollection.Name)]
public sealed class MatchResultEndpointTests(PostgreSqlFixture database) : IClassFixture<WebApplicationFactory<Program>>
{
    private const string SharedKey = "local-shared-game-server-key";
    private static readonly Guid MatchId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ResultId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly WebApplicationFactory<Program> _factory = new();

    [Fact]
    public async Task InternalResultEndpointRejectsMissingServerKey()
    {
        using var application = CreateApplication();
        using var client = application.CreateClient();

        using var response = await client.PostAsJsonAsync("/internal/v1/matches/results", ValidRequest());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task InternalResultEndpointAcceptsResultAndTreatsRepeatedBodyAsDuplicate()
    {
        await ResetDatabaseAsync();
        using var application = CreateApplication();
        using var client = application.CreateClient();
        await using var setupContext = CreateDbContext();
        var playerId = await CreateAssignedMatchAsync(setupContext);
        var request = ValidRequest(playerId);

        using var first = await SubmitResultAsync(client, request);
        using var second = await SubmitResultAsync(client, request);
        var firstBody = await first.Content.ReadFromJsonAsync<MatchResultSubmissionResponse>();
        var secondBodyText = await second.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.NotNull(firstBody);
        Assert.True(firstBody.Accepted);
        Assert.False(firstBody.Duplicate);
        Assert.Equal(1, firstBody.NewRewardTransactions);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Contains("\"duplicate\":true", secondBodyText);
        await using var verificationContext = CreateDbContext();
        Assert.Equal(1, await verificationContext.RewardTransactions.CountAsync());
    }

    [Fact]
    public async Task InternalResultEndpointReturnsConflictCodeForConflictingResultPayload()
    {
        await ResetDatabaseAsync();
        using var application = CreateApplication();
        using var client = application.CreateClient();
        await using var setupContext = CreateDbContext();
        var playerId = await CreateAssignedMatchAsync(setupContext);
        var acceptedRequest = ValidRequest(playerId);
        var conflictingRequest = acceptedRequest with
        {
            Participants = [new MatchResultParticipantRequest(playerId, "dead", 180, 25, 10, "phase05_test_reward")]
        };
        using var accepted = await SubmitResultAsync(client, acceptedRequest);

        using var conflict = await SubmitResultAsync(client, conflictingRequest);
        var problem = await conflict.Content.ReadFromJsonAsync<ProblemResponse>();

        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.NotNull(problem);
        Assert.Equal("result_conflict", problem.Code);
    }

    private static async Task<HttpResponseMessage> SubmitResultAsync(HttpClient client, MatchResultSubmissionRequest resultRequest)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/internal/v1/matches/results")
        {
            Content = JsonContent.Create(resultRequest)
        };
        request.Headers.Add("X-Game-Server-Key", SharedKey);

        return await client.SendAsync(request);
    }

    private async Task ResetDatabaseAsync()
    {
        await using var context = CreateDbContext();
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
    }

    private AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(database.ConnectionString)
            .Options;

        return new AppDbContext(options);
    }

    private static async Task<Guid> CreateAssignedMatchAsync(AppDbContext context)
    {
        var playerId = Guid.NewGuid();
        var now = DateTimeOffset.Parse("2026-09-20T00:00:00Z");
        context.Users.Add(new User { Id = playerId, NormalizedUsername = "RESULT_PLAYER", CreatedAtUtc = now });
        context.PlayerProfiles.Add(new PlayerProfile { UserId = playerId, Username = "result_player", CreatedAtUtc = now });
        await context.GameServerSlots
            .Where(slot => slot.ServerId == "game-server-1")
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(slot => slot.Status, GameServerSlotStatuses.Occupied)
                .SetProperty(slot => slot.CurrentMatchId, MatchId)
                .SetProperty(slot => slot.UpdatedAtUtc, now));
        context.Matches.Add(new MatchSession
        {
            Id = MatchId,
            PlayerId = playerId,
            ServerId = "game-server-1",
            Status = MatchSessionStatuses.TicketValidated,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        await context.SaveChangesAsync();
        return playerId;
    }

    private WebApplicationFactory<Program> CreateApplication() =>
        _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:MainDb", database.ConnectionString);
            builder.UseSetting("GameServers:SharedKey", SharedKey);
            builder.UseSetting("GameServers:TicketLifetimeSeconds", "60");
            builder.UseSetting("GameServers:Slots:0:ServerId", "game-server-1");
            builder.UseSetting("GameServers:Slots:0:PublicHost", "localhost");
            builder.UseSetting("GameServers:Slots:0:PublicPort", "7771");
        });

    private static MatchResultSubmissionRequest ValidRequest(Guid? playerId = null) => new(
        MatchId,
        "game-server-1",
        ResultId,
        DateTimeOffset.Parse("2026-09-20T00:05:00Z"),
        [new MatchResultParticipantRequest(playerId ?? Guid.Parse("33333333-3333-3333-3333-333333333333"), "extracted", 180, 25, 10, "phase05_test_reward")]);

    private sealed record ProblemResponse(string? Code);
}
