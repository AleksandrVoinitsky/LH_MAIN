using LH.Main.Backend.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LH.Main.Backend.Api.Tests;

[Collection(PostgreSqlCollection.Name)]
public sealed class MatchResultServiceTests(PostgreSqlFixture database)
{
    [Fact]
    public async Task DatabaseContainsMatchResultRewardTablesAfterMigration()
    {
        await using var context = CreateDbContext();
        await context.Database.MigrateAsync();

        var tableNames = await context.Database
            .SqlQueryRaw<string>("select table_name as \"Value\" from information_schema.tables where table_schema = 'public'")
            .ToListAsync();

        Assert.Contains("match_results", tableNames);
        Assert.Contains("match_result_participants", tableNames);
        Assert.Contains("reward_transactions", tableNames);
    }

    private AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(database.ConnectionString)
            .Options;
        return new AppDbContext(options);
    }
}
