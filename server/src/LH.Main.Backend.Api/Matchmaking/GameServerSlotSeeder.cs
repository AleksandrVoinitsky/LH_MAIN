using LH.Main.Backend.Api.Persistence;
using LH.Main.Backend.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace LH.Main.Backend.Api.Matchmaking;

public static class GameServerSlotSeeder
{
    public static async Task SeedAsync(
        AppDbContext database,
        GameServerOptions options,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var configuredSlot in options.Slots)
        {
            var serverId = configuredSlot.ServerId!;
            var slot = await database.GameServerSlots.SingleOrDefaultAsync(
                candidate => candidate.ServerId == serverId,
                cancellationToken);

            if (slot is null)
            {
                database.GameServerSlots.Add(new GameServerSlot
                {
                    ServerId = serverId,
                    PublicHost = configuredSlot.PublicHost!,
                    PublicPort = configuredSlot.PublicPort,
                    Status = GameServerSlotStatuses.Available,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                });
                continue;
            }

            slot.PublicHost = configuredSlot.PublicHost!;
            slot.PublicPort = configuredSlot.PublicPort;
            if (slot.Status != GameServerSlotStatuses.Occupied && slot.CurrentMatchId is null)
            {
                slot.Status = GameServerSlotStatuses.Available;
            }

            slot.UpdatedAtUtc = now;
        }

        await database.SaveChangesAsync(cancellationToken);
    }
}
