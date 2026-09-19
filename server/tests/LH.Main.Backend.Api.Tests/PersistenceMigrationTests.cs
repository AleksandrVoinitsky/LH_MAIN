using Npgsql;
using LH.Main.Backend.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LH.Main.Backend.Api.Tests;

[Collection(PostgreSqlCollection.Name)]
public sealed class PersistenceMigrationTests(PostgreSqlFixture database) : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory = new();

    [Fact]
    public async Task StartupAppliesIdentityMigrationToEmptyDatabase()
    {
        using var application = _factory.WithWebHostBuilder(builder =>
            builder.UseSetting("ConnectionStrings:MainDb", database.ConnectionString));
        using var client = application.CreateClient();
        await client.GetAsync("/health/live");

        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();

        const string sql = """
            SELECT table_name
            FROM information_schema.tables
            WHERE table_schema = 'public'
              AND table_name IN ('users', 'password_credentials', 'player_profiles')
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();

        var tables = new List<string>();
        while (await reader.ReadAsync())
        {
            tables.Add(reader.GetString(0));
        }

        Assert.Equal(
            ["password_credentials", "player_profiles", "users"],
            tables.OrderBy(table => table));
    }

    [Fact]
    public async Task ApplyingMigrationsAgainPreservesExistingRecords()
    {
        using var application = _factory.WithWebHostBuilder(builder =>
            builder.UseSetting("ConnectionStrings:MainDb", database.ConnectionString));

        using (var scope = application.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            context.Database.SetConnectionString(database.ConnectionString);
            await context.Database.MigrateAsync();

            await context.Database.ExecuteSqlRawAsync(
                "INSERT INTO users (id, normalized_username, created_at_utc) VALUES ({0}, {1}, {2})",
                Guid.NewGuid(),
                "MIGRATION_TEST_USER",
                DateTimeOffset.UtcNow);
        }

        using (var scope = application.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            context.Database.SetConnectionString(database.ConnectionString);
            await context.Database.MigrateAsync();
        }

        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT COUNT(*) FROM users WHERE normalized_username = 'MIGRATION_TEST_USER'",
            connection);

        Assert.Equal(1L, await command.ExecuteScalarAsync());
    }

    [Fact]
    public async Task UpgradeFromInitialMigrationRenamesColumnsWithoutDataLoss()
    {
        using var application = _factory.WithWebHostBuilder(builder =>
            builder.UseSetting("ConnectionStrings:MainDb", database.ConnectionString));
        using var scope = application.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        context.Database.SetConnectionString(database.ConnectionString);
        var migrator = context.Database.GetService<IMigrator>();
        await migrator.MigrateAsync("20260919131910_InitialIdentity");
        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO users (\"Id\", \"NormalizedUsername\", \"CreatedAtUtc\") VALUES ({0}, {1}, {2})",
            Guid.NewGuid(),
            "UPGRADE_TEST_USER",
            DateTimeOffset.UtcNow);

        await migrator.MigrateAsync();

        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT COUNT(*) FROM users WHERE normalized_username = 'UPGRADE_TEST_USER'",
            connection);

        Assert.Equal(1L, await command.ExecuteScalarAsync());
    }

    [Fact]
    public async Task MigrationsUseLowercaseIdentityConstraintNames()
    {
        using var application = _factory.WithWebHostBuilder(builder =>
            builder.UseSetting("ConnectionStrings:MainDb", database.ConnectionString));
        using var client = application.CreateClient();
        await client.GetAsync("/health/live");

        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT conname FROM pg_constraint WHERE conrelid IN ('users'::regclass, 'password_credentials'::regclass, 'player_profiles'::regclass) ORDER BY conname",
            connection);
        await using var reader = await command.ExecuteReaderAsync();
        var constraints = new List<string>();
        while (await reader.ReadAsync())
        {
            constraints.Add(reader.GetString(0));
        }

        Assert.All(constraints, constraint => Assert.Equal(constraint.ToLowerInvariant(), constraint));
    }
}
