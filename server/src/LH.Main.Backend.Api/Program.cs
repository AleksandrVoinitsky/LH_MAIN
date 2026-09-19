using LH.Main.Contracts;
using LH.Main.Backend.Api.Identity;
using LH.Main.Backend.Api.Persistence;
using LH.Main.Backend.Api.Persistence.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("MainDb");

if (!string.IsNullOrWhiteSpace(connectionString))
{
    builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
    builder.Services.AddScoped<IdentityService>();
    builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
}

var app = builder.Build();

if (!string.IsNullOrWhiteSpace(connectionString))
{
    await using var scope = app.Services.CreateAsyncScope();
    var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await database.Database.MigrateAsync();
}

app.MapGet("/health/live", () => Results.Ok(new HealthStatusResponse("ok")));
app.MapGet("/health/ready", () => Results.Ok(new HealthStatusResponse("ok")));

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
            RegistrationStatus.Duplicate => Results.Conflict(),
            _ => Results.BadRequest()
        };
    });
}

app.Run();

public partial class Program;
