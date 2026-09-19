using System.Net;
using System.Net.Http.Json;
using LH.Main.Backend.Api.Matchmaking;
using LH.Main.Backend.Api.Persistence;
using LH.Main.Backend.Api.Persistence.Entities;
using LH.Main.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LH.Main.Backend.Api.Tests;

[Collection(PostgreSqlCollection.Name)]
public sealed class TicketValidationEndpointsTests(PostgreSqlFixture database) : IClassFixture<WebApplicationFactory<Program>>
{
    private const string SharedKey = "local-shared-game-server-key";
    private readonly WebApplicationFactory<Program> _factory = new();

    [Theory]
    [InlineData(null)]
    [InlineData("wrong-game-server-key")]
    public async Task TicketValidationRequiresGameServerKeyAndDoesNotRevealTicketValidity(string? serverKey)
    {
        await ResetDatabaseAsync();
        using var application = CreateApplication();
        using var client = application.CreateClient();
        var (playerId, assignment) = await CreateAssignmentAsync(client);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/internal/v1/matches/tickets/validate")
        {
            Content = JsonContent.Create(new TicketValidationRequest(
                assignment.Ticket,
                assignment.MatchId,
                playerId,
                assignment.ServerId))
        };
        if (serverKey is not null)
        {
            request.Headers.Add("X-Game-Server-Key", serverKey);
        }

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.True(string.IsNullOrWhiteSpace(await response.Content.ReadAsStringAsync()));
        await using var context = CreateDbContext();
        var ticket = await context.MatchTickets.SingleAsync(ticket => ticket.MatchId == assignment.MatchId);
        var match = await context.Matches.SingleAsync(match => match.Id == assignment.MatchId);
        Assert.Null(ticket.ConsumedAtUtc);
        Assert.Equal(MatchSessionStatuses.Reserved, match.Status);
    }

    [Fact]
    public async Task TicketValidationSucceedsOnceAndConsumesTicket()
    {
        await ResetDatabaseAsync();
        using var application = CreateApplication();
        using var client = application.CreateClient();
        var (playerId, assignment) = await CreateAssignmentAsync(client);
        var request = new TicketValidationRequest(assignment.Ticket, assignment.MatchId, playerId, assignment.ServerId);

        using var first = await ValidateTicketAsync(client, request);
        using var second = await ValidateTicketAsync(client, request);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstBody = await first.Content.ReadFromJsonAsync<TicketValidationResponse>();
        Assert.NotNull(firstBody);
        Assert.True(firstBody.Valid);
        Assert.Equal(assignment.MatchId, firstBody.MatchId);
        Assert.Equal(playerId, firstBody.PlayerId);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var secondBody = await second.Content.ReadFromJsonAsync<TicketValidationResponse>();
        Assert.NotNull(secondBody);
        Assert.False(secondBody.Valid);
        Assert.Null(secondBody.MatchId);
        Assert.Null(secondBody.PlayerId);
        await using var context = CreateDbContext();
        var ticket = await context.MatchTickets.SingleAsync(ticket => ticket.MatchId == assignment.MatchId);
        var match = await context.Matches.SingleAsync(match => match.Id == assignment.MatchId);
        Assert.NotNull(ticket.ConsumedAtUtc);
        Assert.Equal(MatchSessionStatuses.TicketValidated, match.Status);
    }

    [Theory]
    [InlineData("wrong-player")]
    [InlineData("wrong-match")]
    [InlineData("wrong-server")]
    [InlineData("expired-ticket")]
    [InlineData("unknown-ticket")]
    public async Task InvalidTicketValidationRequestsReturnInvalidWithoutUnauthorized(string scenario)
    {
        await ResetDatabaseAsync();
        using var application = CreateApplication();
        using var client = application.CreateClient();
        var (playerId, assignment) = await CreateAssignmentAsync(client);
        var request = new TicketValidationRequest(assignment.Ticket, assignment.MatchId, playerId, assignment.ServerId);

        if (scenario == "wrong-player")
        {
            request = request with { PlayerId = Guid.NewGuid() };
        }
        else if (scenario == "wrong-match")
        {
            request = request with { MatchId = Guid.NewGuid() };
        }
        else if (scenario == "wrong-server")
        {
            request = request with { ServerId = "game-server-2" };
        }
        else if (scenario == "expired-ticket")
        {
            await using var context = CreateDbContext();
            await context.MatchTickets
                .Where(ticket => ticket.MatchId == assignment.MatchId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(ticket => ticket.ExpiresAtUtc, DateTimeOffset.UtcNow.AddSeconds(-1)));
        }
        else if (scenario == "unknown-ticket")
        {
            request = request with { Ticket = "unknown-ticket-material" };
        }

        using var response = await ValidateTicketAsync(client, request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<TicketValidationResponse>();
        Assert.NotNull(body);
        Assert.False(body.Valid);
        Assert.Null(body.MatchId);
        Assert.Null(body.PlayerId);
        await using var verificationContext = CreateDbContext();
        var ticket = await verificationContext.MatchTickets.SingleAsync(ticket => ticket.MatchId == assignment.MatchId);
        Assert.Null(ticket.ConsumedAtUtc);
    }

    private async Task<(Guid PlayerId, MatchAssignmentResponse Assignment)> CreateAssignmentAsync(HttpClient client)
    {
        var username = $"ticket_user_{Guid.NewGuid():N}";
        const string password = "correct-horse-battery-staple";
        using var registerResponse = await client.PostAsJsonAsync("/v1/auth/dev-register", new { username, password });
        registerResponse.EnsureSuccessStatusCode();
        using var loginResponse = await client.PostAsJsonAsync("/v1/auth/dev-login", new { username, password });
        loginResponse.EnsureSuccessStatusCode();
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(login);
        client.DefaultRequestHeaders.Authorization = new("Bearer", login.AccessToken);
        using var queueResponse = await client.PostAsync("/v1/matchmaking/queue", null);
        queueResponse.EnsureSuccessStatusCode();
        var queue = await queueResponse.Content.ReadFromJsonAsync<MatchmakingStatusResponse>();
        Assert.NotNull(queue?.Assignment);
        client.DefaultRequestHeaders.Authorization = null;
        await using var context = CreateDbContext();
        var playerId = await context.Users
            .Where(user => user.NormalizedUsername == username.ToUpperInvariant())
            .Select(user => user.Id)
            .SingleAsync();

        return (playerId, queue.Assignment);
    }

    private static async Task<HttpResponseMessage> ValidateTicketAsync(HttpClient client, TicketValidationRequest validationRequest)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/internal/v1/matches/tickets/validate")
        {
            Content = JsonContent.Create(validationRequest)
        };
        request.Headers.Add("X-Game-Server-Key", SharedKey);

        return await client.SendAsync(request);
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
            builder.UseSetting("GameServers:SharedKey", SharedKey);
            builder.UseSetting("GameServers:TicketLifetimeSeconds", "60");
            builder.UseSetting("GameServers:Slots:0:ServerId", "game-server-1");
            builder.UseSetting("GameServers:Slots:0:PublicHost", "localhost");
            builder.UseSetting("GameServers:Slots:0:PublicPort", "7771");
            builder.UseSetting("GameServers:Slots:1:ServerId", "game-server-2");
            builder.UseSetting("GameServers:Slots:1:PublicHost", "localhost");
            builder.UseSetting("GameServers:Slots:1:PublicPort", "7772");
        });

    private sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAtUtc);
}
