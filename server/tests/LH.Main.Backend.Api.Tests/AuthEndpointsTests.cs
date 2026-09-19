using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Xunit;

namespace LH.Main.Backend.Api.Tests;

[Collection(PostgreSqlCollection.Name)]
public sealed class AuthEndpointsTests(PostgreSqlFixture database) : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory = new();

    [Fact]
    public async Task DevRegistrationCreatesAndReturnsPlayerProfile()
    {
        using var application = CreateApplication(enableDevRegistration: true);
        using var client = application.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/v1/auth/dev-register",
            new { username = "Dev_Player", password = "correct-horse-battery-staple" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<RegistrationResponse>();
        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body.UserId);
        Assert.Equal("Dev_Player", body.Username);
        Assert.True(body.CreatedAtUtc <= DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task DevRegistrationStoresPasswordOnlyAsHash()
    {
        const string password = "correct-horse-battery-staple";
        var username = $"hash_user_{Guid.NewGuid():N}";
        using var application = CreateApplication(enableDevRegistration: true);
        using var client = application.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/v1/auth/dev-register",
            new { username, password });
        var profile = await response.Content.ReadFromJsonAsync<RegistrationResponse>();

        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT \"PasswordHash\" FROM password_credentials WHERE \"UserId\" = @userId",
            connection);
        command.Parameters.AddWithValue("userId", profile!.UserId);
        var passwordHash = (string?)await command.ExecuteScalarAsync();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(passwordHash);
        Assert.NotEqual(password, passwordHash);
    }

    [Fact]
    public async Task DevRegistrationRejectsCaseInsensitiveDuplicateUsername()
    {
        using var application = CreateApplication(enableDevRegistration: true);
        using var client = application.CreateClient();
        var request = new { username = "Duplicated_User", password = "correct-horse-battery-staple" };

        await client.PostAsJsonAsync("/v1/auth/dev-register", request);
        using var response = await client.PostAsJsonAsync(
            "/v1/auth/dev-register",
            new { username = "duplicated_user", password = "correct-horse-battery-staple" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Theory]
    [InlineData("ab", "correct-horse-battery-staple")]
    [InlineData("invalid-name", "correct-horse-battery-staple")]
    [InlineData("valid_name", "short")]
    public async Task DevRegistrationRejectsInvalidCredentials(string username, string password)
    {
        using var application = CreateApplication(enableDevRegistration: true);
        using var client = application.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/v1/auth/dev-register",
            new { username, password });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DevRegistrationIsHiddenWhenDisabled()
    {
        using var application = CreateApplication(enableDevRegistration: false);
        using var client = application.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/v1/auth/dev-register",
            new { username = "dev_player", password = "correct-horse-battery-staple" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private WebApplicationFactory<Program> CreateApplication(bool enableDevRegistration) =>
        _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:MainDb", database.ConnectionString);
            builder.UseSetting("Authentication:EnableDevRegistration", enableDevRegistration.ToString());
        });

    private sealed record RegistrationResponse(Guid UserId, string Username, DateTimeOffset CreatedAtUtc);
}
