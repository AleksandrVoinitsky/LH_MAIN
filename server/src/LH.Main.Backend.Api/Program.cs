using System.IdentityModel.Tokens.Jwt;
using System.Text;
using LH.Main.Contracts;
using LH.Main.Backend.Api.Identity;
using LH.Main.Backend.Api.Matchmaking;
using LH.Main.Backend.Api.Persistence;
using LH.Main.Backend.Api.Persistence.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("MainDb");
var authenticationSection = builder.Configuration.GetSection("Authentication");
var jwtSigningKey = authenticationSection["JwtSigningKey"];
var jwtIssuer = authenticationSection["Issuer"];
var jwtAudience = authenticationSection["Audience"];
var accessTokenLifetimeMinutes = authenticationSection.GetValue<int?>("AccessTokenLifetimeMinutes");
var hasJwtConfiguration = !string.IsNullOrWhiteSpace(jwtSigningKey)
    && Encoding.UTF8.GetByteCount(jwtSigningKey) >= 32
    && !string.IsNullOrWhiteSpace(jwtIssuer)
    && !string.IsNullOrWhiteSpace(jwtAudience)
    && accessTokenLifetimeMinutes is > 0;

builder.Services.AddAuthentication();

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddOpenApi(options =>
    {
        options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
        options.AddOperationTransformer<BearerSecurityRequirementTransformer>();
    });
}

if (!string.IsNullOrWhiteSpace(connectionString))
{
    builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
    builder.Services.AddScoped<IdentityService>();
    builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
    builder.Services.AddScoped<MatchmakingService>();
}

builder.Services.Configure<GameServerOptions>(builder.Configuration.GetSection("GameServers"));
builder.Services.AddSingleton<LH.Main.Backend.Api.Matchmaking.ISystemClock, LH.Main.Backend.Api.Matchmaking.SystemClock>();
builder.Services.AddScoped<TicketService>();

if (hasJwtConfiguration)
{
    var tokenOptions = new JwtTokenOptions(
        jwtSigningKey!,
        jwtIssuer!,
        jwtAudience!,
        TimeSpan.FromMinutes(accessTokenLifetimeMinutes!.Value));
    builder.Services.AddSingleton(tokenOptions);
    builder.Services.AddSingleton<JwtTokenService>();
    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(tokenOptions.SigningKey)),
                ValidateIssuer = true,
                ValidIssuer = tokenOptions.Issuer,
                ValidateAudience = true,
                ValidAudience = tokenOptions.Audience,
                ValidateLifetime = true,
                ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                ClockSkew = TimeSpan.Zero
            };
            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = async context =>
                {
                    var subject = context.Principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
                    if (!Guid.TryParse(subject, out var userId))
                    {
                        context.Fail("Invalid token subject.");
                        return;
                    }

                    var identityService = context.HttpContext.RequestServices.GetRequiredService<IdentityService>();
                    if (await identityService.GetProfileAsync(userId, context.HttpContext.RequestAborted) is null)
                    {
                        context.Fail("Token subject no longer exists.");
                    }
                }
            };
        });
    builder.Services.AddAuthorization();
}

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.Use(async (context, next) =>
{
    var correlationId = Guid.TryParse(context.Request.Headers["X-Correlation-ID"], out var requestCorrelationId)
        ? requestCorrelationId
        : Guid.NewGuid();
    var correlationIdValue = correlationId.ToString();
    context.TraceIdentifier = correlationIdValue;
    context.Response.Headers["X-Correlation-ID"] = correlationIdValue;

    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    using (logger.BeginScope(new Dictionary<string, object?> { ["CorrelationId"] = correlationIdValue }))
    {
        await next(context);
    }
});

if (hasJwtConfiguration)
{
    app.UseAuthentication();
    app.UseAuthorization();
}

if (!string.IsNullOrWhiteSpace(connectionString))
{
    await using var scope = app.Services.CreateAsyncScope();
    var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await database.Database.MigrateAsync();

    var gameServerOptions = new GameServerOptions();
    app.Configuration.GetSection("GameServers").Bind(gameServerOptions);
    if (!gameServerOptions.Validate().Any())
    {
        await GameServerSlotSeeder.SeedAsync(database, gameServerOptions, CancellationToken.None);
    }
}

app.MapGet("/health/live", () => Results.Ok(new HealthStatusResponse("ok")));
app.MapGet("/health/ready", async (HttpContext context, CancellationToken cancellationToken) =>
{
    var database = context.RequestServices.GetService<AppDbContext>();
    return database is not null && await database.Database.CanConnectAsync(cancellationToken)
        ? Results.Ok(new HealthStatusResponse("ok"))
        : Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
});

if (builder.Configuration.GetValue<bool>("Authentication:EnableDevRegistration"))
{
    app.MapPost("/v1/auth/dev-register", async (
        DevCredentialsRequest request,
        IdentityService identityService,
        CancellationToken cancellationToken) =>
    {
        var result = await identityService.RegisterAsync(request, cancellationToken);
        return result.Status switch
        {
            RegistrationStatus.Success => Results.Created("/v1/profile", result.Profile),
            RegistrationStatus.Duplicate => Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                extensions: new Dictionary<string, object?> { ["code"] = "username_already_exists" }),
            _ => Results.Problem(statusCode: StatusCodes.Status400BadRequest)
        };
    });
}

if (builder.Configuration.GetValue<bool>("Authentication:EnableDevRegistration")
    && hasJwtConfiguration
    && !string.IsNullOrWhiteSpace(connectionString))
{
    app.MapPost("/v1/auth/dev-login", async (
        DevCredentialsRequest request,
        IdentityService identityService,
        JwtTokenService tokenService,
        CancellationToken cancellationToken) =>
    {
        var result = await identityService.LoginAsync(request, cancellationToken);
        return result.UserId is { } userId
            ? Results.Ok(tokenService.Create(userId))
            : Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                extensions: new Dictionary<string, object?> { ["code"] = "invalid_credentials" });
    });

    app.MapGet("/v1/profile", async (
        HttpContext context,
        IdentityService identityService,
        CancellationToken cancellationToken) =>
    {
        var subject = context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (!Guid.TryParse(subject, out var userId))
        {
            return Results.Unauthorized();
        }

        var profile = await identityService.GetProfileAsync(userId, cancellationToken);
        return profile is null ? Results.Unauthorized() : Results.Ok(profile);
    }).RequireAuthorization();

    app.MapPost("/v1/matchmaking/queue", async (
        HttpContext context,
        MatchmakingService service,
        CancellationToken cancellationToken) =>
    {
        var subject = context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (!Guid.TryParse(subject, out var userId))
        {
            return Results.Unauthorized();
        }

        return Results.Ok(await service.EnqueueAsync(userId, cancellationToken));
    }).RequireAuthorization();

    app.MapGet("/v1/matchmaking/status", async (
        HttpContext context,
        MatchmakingService service,
        CancellationToken cancellationToken) =>
    {
        var subject = context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (!Guid.TryParse(subject, out var userId))
        {
            return Results.Unauthorized();
        }

        return Results.Ok(await service.GetStatusAsync(userId, cancellationToken));
    }).RequireAuthorization();

    app.MapPost("/v1/matchmaking/cancel", async (
        HttpContext context,
        MatchmakingService service,
        CancellationToken cancellationToken) =>
    {
        var subject = context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (!Guid.TryParse(subject, out var userId))
        {
            return Results.Unauthorized();
        }

        var result = await service.CancelAsync(userId, cancellationToken);
        return result.ConflictAssigned
            ? Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                extensions: new Dictionary<string, object?> { ["code"] = "match_already_assigned" })
            : Results.Ok(result.Response);
    }).RequireAuthorization();
}

app.Run();

public partial class Program;

public sealed class BearerSecuritySchemeTransformer(IAuthenticationSchemeProvider authenticationSchemeProvider) : IOpenApiDocumentTransformer
{
    public async Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        var authenticationSchemes = await authenticationSchemeProvider.GetAllSchemesAsync();
        if (!authenticationSchemes.Any(scheme => scheme.Name == JwtBearerDefaults.AuthenticationScheme))
        {
            return;
        }

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
        {
            [JwtBearerDefaults.AuthenticationScheme] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                In = ParameterLocation.Header,
                BearerFormat = "JWT"
            }
        };
    }
}

public sealed class BearerSecurityRequirementTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        var requiresAuthorization = context.Description.ActionDescriptor.EndpointMetadata
            .OfType<IAuthorizeData>()
            .Any();
        if (!requiresAuthorization)
        {
            return Task.CompletedTask;
        }

        operation.Security ??= [];
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(JwtBearerDefaults.AuthenticationScheme, null, null)] = []
        });
        return Task.CompletedTask;
    }
}
