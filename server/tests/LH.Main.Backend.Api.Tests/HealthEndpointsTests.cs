using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LH.Main.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace LH.Main.Backend.Api.Tests;

[Collection(PostgreSqlCollection.Name)]
public sealed class HealthEndpointsTests(WebApplicationFactory<Program> factory, PostgreSqlFixture database) : IClassFixture<WebApplicationFactory<Program>>
{
    [Theory]
    [InlineData("/health/live")]
    public async Task HealthEndpointReturnsOkStatus(string path)
    {
        using var response = await factory.CreateClient().GetAsync(path);
        var body = await response.Content.ReadFromJsonAsync<HealthStatusResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("ok", body?.Status);
    }

    [Fact]
    public async Task ReadinessReturnsServiceUnavailableWithoutDatabaseConfiguration()
    {
        using var response = await factory.CreateClient().GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task ReadinessReturnsOkWhenDatabaseIsAvailable()
    {
        using var application = factory.WithWebHostBuilder(builder =>
            builder.UseSetting("ConnectionStrings:MainDb", database.ConnectionString));
        using var response = await application.CreateClient().GetAsync("/health/ready");
        var body = await response.Content.ReadFromJsonAsync<HealthStatusResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("ok", body?.Status);
    }

    [Fact]
    public async Task CorrelationIdPreservesValidRequestHeader()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        var correlationId = Guid.NewGuid().ToString();
        request.Headers.Add("X-Correlation-ID", correlationId);

        using var response = await factory.CreateClient().SendAsync(request);

        Assert.True(response.Headers.TryGetValues("X-Correlation-ID", out var values));
        Assert.Equal(correlationId, Assert.Single(values));
    }

    [Fact]
    public async Task CorrelationIdReplacesInvalidRequestHeader()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add("X-Correlation-ID", "not-a-uuid");

        using var response = await factory.CreateClient().SendAsync(request);

        Assert.True(response.Headers.TryGetValues("X-Correlation-ID", out var values));
        Assert.True(Guid.TryParse(Assert.Single(values), out _));
    }

    [Theory]
    [InlineData("Development", HttpStatusCode.OK)]
    [InlineData("Production", HttpStatusCode.NotFound)]
    public async Task OpenApiIsAvailableOnlyInDevelopment(string environment, HttpStatusCode expectedStatus)
    {
        using var application = factory.WithWebHostBuilder(builder => builder.UseSetting("environment", environment));

        using var response = await application.CreateClient().GetAsync("/openapi/v1.json");

        Assert.Equal(expectedStatus, response.StatusCode);
    }

    [Fact]
    public async Task OpenApiDescribesBearerAuthenticationForPublicAuthenticatedEndpoints()
    {
        using var application = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("environment", "Development");
            builder.UseSetting("ConnectionStrings:MainDb", database.ConnectionString);
            builder.UseSetting("Authentication:JwtSigningKey", "test-signing-key-that-is-at-least-thirty-two-characters");
            builder.UseSetting("Authentication:Issuer", "lh-main-tests");
            builder.UseSetting("Authentication:Audience", "lh-main-tests");
            builder.UseSetting("Authentication:AccessTokenLifetimeMinutes", "15");
            builder.UseSetting("Authentication:EnableDevRegistration", "true");
        });
        using var response = await application.CreateClient().GetAsync("/openapi/v1.json");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        var bearerScheme = document.RootElement
            .GetProperty("components")
            .GetProperty("securitySchemes")
            .GetProperty("Bearer");
        var paths = document.RootElement.GetProperty("paths");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("http", bearerScheme.GetProperty("type").GetString());
        AssertOperationHasSecurity(paths, "/v1/profile", "get");
        AssertOperationHasSecurity(paths, "/v1/matchmaking/queue", "post");
        AssertOperationHasSecurity(paths, "/v1/matchmaking/status", "get");
        AssertOperationHasSecurity(paths, "/v1/matchmaking/cancel", "post");
    }

    private static void AssertOperationHasSecurity(JsonElement paths, string path, string method)
    {
        var operation = paths
            .GetProperty(path)
            .GetProperty(method);

        Assert.True(operation.TryGetProperty("security", out _));
    }
}
