using System.Net;
using System.Net.Http.Json;
using LH.Main.Backend.Api.Persistence;
using LH.Main.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LH.Main.Backend.Api.Tests;

[Collection(PostgreSqlCollection.Name)]
public sealed class MatchmakingEndpointsTests(PostgreSqlFixture database) : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory = new();

    [Theory]
    [InlineData("POST", "/v1/matchmaking/queue")]
    [InlineData("GET", "/v1/matchmaking/status")]
    [InlineData("POST", "/v1/matchmaking/cancel")]
    public async Task MatchmakingEndpointsRequireAuthentication(string method, string path)
    {
        using var application = CreateApplication();
        using var client = application.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AuthenticatedPlayerCanQueueCheckStatusAndCannotCancelAssignedMatch()
    {
        await ResetDatabaseAsync();
        using var application = CreateApplication();
        using var client = application.CreateClient();
        var username = $"match_user_{Guid.NewGuid():N}";
        const string password = "correct-horse-battery-staple";
        await client.PostAsJsonAsync("/v1/auth/dev-register", new { username, password });
        using var loginResponse = await client.PostAsJsonAsync("/v1/auth/dev-login", new { username, password });
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization = new("Bearer", login?.AccessToken);

        using var queueResponse = await client.PostAsync("/v1/matchmaking/queue", null);
        var queue = await queueResponse.Content.ReadFromJsonAsync<MatchmakingStatusResponse>();
        using var statusResponse = await client.GetAsync("/v1/matchmaking/status");
        var status = await statusResponse.Content.ReadFromJsonAsync<MatchmakingStatusResponse>();
        using var cancelResponse = await client.PostAsync("/v1/matchmaking/cancel", null);
        var problem = await cancelResponse.Content.ReadFromJsonAsync<ProblemResponse>();

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        Assert.NotNull(login);
        Assert.Equal(HttpStatusCode.OK, queueResponse.StatusCode);
        Assert.NotNull(queue);
        Assert.Equal("assigned", queue.Status);
        Assert.NotNull(queue.Assignment);
        Assert.False(string.IsNullOrWhiteSpace(queue.Assignment.Ticket));
        Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);
        Assert.NotNull(status);
        Assert.Equal("assigned", status.Status);
        Assert.Equal(queue.Assignment.MatchId, status.Assignment?.MatchId);
        Assert.Equal(HttpStatusCode.Conflict, cancelResponse.StatusCode);
        Assert.NotNull(problem);
        Assert.Equal("match_already_assigned", problem.Code);
    }

    private async Task ResetDatabaseAsync()
    {
        await using var context = CreateDbContext();
        await context.Database.MigrateAsync();
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

    private WebApplicationFactory<Program> CreateApplication() =>
        _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:MainDb", database.ConnectionString);
            builder.UseSetting("Authentication:EnableDevRegistration", "true");
            builder.UseSetting("Authentication:JwtSigningKey", "test-signing-key-that-is-at-least-thirty-two-characters");
            builder.UseSetting("Authentication:Issuer", "lh-main-tests");
            builder.UseSetting("Authentication:Audience", "lh-main-tests");
            builder.UseSetting("Authentication:AccessTokenLifetimeMinutes", "15");
            builder.UseSetting("GameServers:SharedKey", "local-shared-game-server-key");
            builder.UseSetting("GameServers:TicketLifetimeSeconds", "60");
            builder.UseSetting("GameServers:Slots:0:ServerId", "game-server-1");
            builder.UseSetting("GameServers:Slots:0:PublicHost", "localhost");
            builder.UseSetting("GameServers:Slots:0:PublicPort", "7771");
            builder.UseSetting("GameServers:Slots:1:ServerId", "game-server-2");
            builder.UseSetting("GameServers:Slots:1:PublicHost", "localhost");
            builder.UseSetting("GameServers:Slots:1:PublicPort", "7772");
        });

    private sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAtUtc);

    private sealed record ProblemResponse(string? Code);
}
