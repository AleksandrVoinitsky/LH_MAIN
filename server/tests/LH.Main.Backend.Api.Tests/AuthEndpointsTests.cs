using System.Net;
using System.Net.Http.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.IdentityModel.Tokens;
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
            "SELECT password_hash FROM password_credentials WHERE user_id = @userId",
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
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Equal("username_already_exists", problem.Code);
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
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(problem);
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

    [Fact]
    public async Task DevLoginIsHiddenWhenDisabled()
    {
        using var application = CreateApplication(enableDevRegistration: false);
        using var response = await application.CreateClient().PostAsJsonAsync(
            "/v1/auth/dev-login",
            new { username = "dev_player", password = "correct-horse-battery-staple" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task LoginIsUnavailableWhenSigningKeyIsTooShort()
    {
        using var application = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:MainDb", database.ConnectionString);
            builder.UseSetting("Authentication:EnableDevRegistration", "true");
            builder.UseSetting("Authentication:JwtSigningKey", "too-short");
            builder.UseSetting("Authentication:Issuer", "lh-main-tests");
            builder.UseSetting("Authentication:Audience", "lh-main-tests");
            builder.UseSetting("Authentication:AccessTokenLifetimeMinutes", "15");
        });

        using var response = await application.CreateClient().PostAsJsonAsync(
            "/v1/auth/dev-login",
            new { username = "dev_player", password = "correct-horse-battery-staple" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task LoginIssuesTokenThatAuthorizesProfileAccess()
    {
        var username = $"login_user_{Guid.NewGuid():N}";
        const string password = "correct-horse-battery-staple";
        using var application = CreateApplication(enableDevRegistration: true);
        using var client = application.CreateClient();
        await client.PostAsJsonAsync("/v1/auth/dev-register", new { username, password });

        using var loginResponse = await client.PostAsJsonAsync("/v1/auth/dev-login", new { username, password });
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization = new("Bearer", login?.AccessToken);
        using var profileResponse = await client.GetAsync("/v1/profile");

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        Assert.NotNull(login);
        Assert.False(string.IsNullOrWhiteSpace(login.AccessToken));
        Assert.True(login.ExpiresAtUtc > DateTimeOffset.UtcNow);
        Assert.Equal(HttpStatusCode.OK, profileResponse.StatusCode);
    }

    [Fact]
    public async Task LoginDoesNotRevealWhetherUsernameExists()
    {
        var username = $"known_user_{Guid.NewGuid():N}";
        using var application = CreateApplication(enableDevRegistration: true);
        using var client = application.CreateClient();
        await client.PostAsJsonAsync("/v1/auth/dev-register", new { username, password = "correct-horse-battery-staple" });

        using var wrongPassword = await client.PostAsJsonAsync("/v1/auth/dev-login", new { username, password = "wrong-password-value" });
        using var unknownUser = await client.PostAsJsonAsync("/v1/auth/dev-login", new { username = "unknown_user", password = "wrong-password-value" });

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownUser.StatusCode);
    }

    [Fact]
    public async Task LoginReturnsNeutralProblemCodeForInvalidCredentials()
    {
        using var application = CreateApplication(enableDevRegistration: true);
        using var client = application.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/v1/auth/dev-login",
            new { username = "unknown_user", password = "wrong-password-value" });

        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Equal("invalid_credentials", problem.Code);
    }

    [Fact]
    public async Task ProfileRejectsTokenForNonexistentUser()
    {
        using var application = CreateApplication(enableDevRegistration: true);
        using var client = application.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", CreateToken(Guid.NewGuid()));

        using var response = await client.GetAsync("/v1/profile");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProfileRejectsRequestWithoutToken()
    {
        using var application = CreateApplication(enableDevRegistration: true);
        using var response = await application.CreateClient().GetAsync("/v1/profile");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProfileRejectsExpiredToken()
    {
        var username = $"expired_user_{Guid.NewGuid():N}";
        using var application = CreateApplication(enableDevRegistration: true);
        using var client = application.CreateClient();
        await client.PostAsJsonAsync(
            "/v1/auth/dev-register",
            new { username, password = "correct-horse-battery-staple" });
        client.DefaultRequestHeaders.Authorization = new("Bearer", CreateToken(Guid.NewGuid(), DateTime.UtcNow.AddMinutes(-1)));

        using var response = await client.GetAsync("/v1/profile");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private WebApplicationFactory<Program> CreateApplication(bool enableDevRegistration) =>
        _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:MainDb", database.ConnectionString);
            builder.UseSetting("Authentication:EnableDevRegistration", enableDevRegistration.ToString());
            builder.UseSetting("Authentication:JwtSigningKey", "test-signing-key-that-is-at-least-thirty-two-characters");
            builder.UseSetting("Authentication:Issuer", "lh-main-tests");
            builder.UseSetting("Authentication:Audience", "lh-main-tests");
            builder.UseSetting("Authentication:AccessTokenLifetimeMinutes", "15");
        });

    private sealed record RegistrationResponse(Guid UserId, string Username, DateTimeOffset CreatedAtUtc);

    private sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAtUtc);

    private sealed record ProblemResponse(string? Code);

    private static string CreateToken(Guid userId, DateTime? expiresAt = null)
    {
        var expires = expiresAt ?? DateTime.UtcNow.AddMinutes(15);
        var securityKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes("test-signing-key-that-is-at-least-thirty-two-characters"));
        var token = new JwtSecurityToken(
            issuer: "lh-main-tests",
            audience: "lh-main-tests",
            claims: [new Claim(JwtRegisteredClaimNames.Sub, userId.ToString())],
            notBefore: expires.AddMinutes(-15),
            expires: expires,
            signingCredentials: new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
