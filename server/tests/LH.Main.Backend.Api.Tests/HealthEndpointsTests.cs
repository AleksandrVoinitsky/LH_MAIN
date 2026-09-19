using System.Net;
using System.Net.Http.Json;
using LH.Main.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace LH.Main.Backend.Api.Tests;

public sealed class HealthEndpointsTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task HealthEndpointReturnsOkStatus(string path)
    {
        using var response = await factory.CreateClient().GetAsync(path);
        var body = await response.Content.ReadFromJsonAsync<HealthStatusResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("ok", body?.Status);
    }
}
