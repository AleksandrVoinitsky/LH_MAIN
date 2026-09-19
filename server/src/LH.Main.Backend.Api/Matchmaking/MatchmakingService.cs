using LH.Main.Backend.Api.Persistence;
using LH.Main.Backend.Api.Persistence.Entities;
using LH.Main.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LH.Main.Backend.Api.Matchmaking;

public sealed record MatchmakingCancelResult(bool ConflictAssigned, MatchmakingStatusResponse Response);

public sealed class MatchmakingService(
    AppDbContext database,
    TicketService ticketService,
    ISystemClock clock,
    IOptions<GameServerOptions> gameServerOptions)
{
    public async Task<MatchmakingStatusResponse> EnqueueAsync(Guid playerId, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        await LockPlayerAsync(playerId, cancellationToken);

        var activeEntry = await GetActiveEntryAsync(playerId, cancellationToken);
        if (activeEntry is not null)
        {
            var activeResponse = await BuildResponseAsync(activeEntry, null, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return activeResponse;
        }

        var now = clock.UtcNow;
        var entry = new MatchQueueEntry
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            Status = MatchQueueEntryStatuses.Queued,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        database.MatchQueueEntries.Add(entry);
        await database.SaveChangesAsync(cancellationToken);

        var slot = await database.GameServerSlots
            .AsNoTracking()
            .Where(candidate => candidate.Status == GameServerSlotStatuses.Available)
            .OrderBy(candidate => candidate.ServerId)
            .FirstOrDefaultAsync(cancellationToken);

        if (slot is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return BuildQueuedResponse(entry);
        }

        var matchId = Guid.NewGuid();
        var slotRowsUpdated = await database.GameServerSlots
            .Where(candidate => candidate.ServerId == slot.ServerId && candidate.Status == GameServerSlotStatuses.Available)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(candidate => candidate.Status, GameServerSlotStatuses.Occupied)
                .SetProperty(candidate => candidate.CurrentMatchId, matchId)
                .SetProperty(candidate => candidate.UpdatedAtUtc, now), cancellationToken);

        if (slotRowsUpdated != 1)
        {
            await transaction.CommitAsync(cancellationToken);
            return BuildQueuedResponse(entry);
        }

        var trackedSlot = database.GameServerSlots.Local.FirstOrDefault(candidate => candidate.ServerId == slot.ServerId);
        if (trackedSlot is not null)
        {
            trackedSlot.Status = GameServerSlotStatuses.Occupied;
            trackedSlot.CurrentMatchId = matchId;
            trackedSlot.UpdatedAtUtc = now;
        }

        var ticket = ticketService.Issue(matchId, playerId, slot.ServerId, gameServerOptions.Value.GetTicketLifetime());
        var match = new MatchSession
        {
            Id = matchId,
            PlayerId = playerId,
            ServerId = slot.ServerId,
            Status = MatchSessionStatuses.Reserved,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        database.Matches.Add(match);
        database.MatchTickets.Add(new MatchTicket
        {
            Id = Guid.NewGuid(),
            MatchId = matchId,
            PlayerId = playerId,
            ServerId = slot.ServerId,
            TicketHash = ticket.TicketHash,
            ExpiresAtUtc = ticket.ExpiresAtUtc,
            CreatedAtUtc = now
        });

        entry.Status = MatchQueueEntryStatuses.Assigned;
        entry.AssignedMatchId = matchId;
        entry.UpdatedAtUtc = now;
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new MatchmakingStatusResponse(
            MatchQueueEntryStatuses.Assigned,
            Assignment: new MatchAssignmentResponse(
                matchId,
                slot.ServerId,
                slot.PublicHost,
                slot.PublicPort,
                ticket.PlaintextTicket,
                ticket.ExpiresAtUtc));
    }

    public async Task<MatchmakingStatusResponse> GetStatusAsync(Guid playerId, CancellationToken cancellationToken)
    {
        var activeEntry = await GetActiveEntryAsync(playerId, cancellationToken);
        return activeEntry is null
            ? new MatchmakingStatusResponse("none")
            : await BuildResponseAsync(activeEntry, null, cancellationToken);
    }

    public async Task<MatchmakingCancelResult> CancelAsync(Guid playerId, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        await LockPlayerAsync(playerId, cancellationToken);

        var activeEntry = await GetActiveEntryAsync(playerId, cancellationToken);
        if (activeEntry is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return new MatchmakingCancelResult(false, new MatchmakingStatusResponse(MatchQueueEntryStatuses.Cancelled));
        }

        if (activeEntry.Status == MatchQueueEntryStatuses.Assigned)
        {
            var response = await BuildResponseAsync(activeEntry, null, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new MatchmakingCancelResult(true, response);
        }

        activeEntry.Status = MatchQueueEntryStatuses.Cancelled;
        activeEntry.UpdatedAtUtc = clock.UtcNow;
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new MatchmakingCancelResult(false, new MatchmakingStatusResponse(MatchQueueEntryStatuses.Cancelled));
    }

    private Task<int> LockPlayerAsync(Guid playerId, CancellationToken cancellationToken) => database.Database.ExecuteSqlInterpolatedAsync(
        $"SELECT pg_advisory_xact_lock(hashtextextended({playerId.ToString()}, 0))",
        cancellationToken);

    private Task<MatchQueueEntry?> GetActiveEntryAsync(Guid playerId, CancellationToken cancellationToken) => database.MatchQueueEntries
        .Where(entry => entry.PlayerId == playerId
            && (entry.Status == MatchQueueEntryStatuses.Queued || entry.Status == MatchQueueEntryStatuses.Assigned))
        .OrderByDescending(entry => entry.UpdatedAtUtc)
        .FirstOrDefaultAsync(cancellationToken);

    private async Task<MatchmakingStatusResponse> BuildResponseAsync(
        MatchQueueEntry entry,
        string? plaintextTicket,
        CancellationToken cancellationToken)
    {
        if (entry.Status == MatchQueueEntryStatuses.Queued)
        {
            return BuildQueuedResponse(entry);
        }

        if (entry.AssignedMatchId is not { } matchId)
        {
            return new MatchmakingStatusResponse(entry.Status);
        }

        var assignment = await (
            from match in database.Matches.AsNoTracking()
            join slot in database.GameServerSlots.AsNoTracking() on match.ServerId equals slot.ServerId
            join ticket in database.MatchTickets.AsNoTracking() on match.Id equals ticket.MatchId
            where match.Id == matchId
            select new MatchAssignmentResponse(
                match.Id,
                slot.ServerId,
                slot.PublicHost,
                slot.PublicPort,
                plaintextTicket ?? string.Empty,
                ticket.ExpiresAtUtc))
            .SingleOrDefaultAsync(cancellationToken);

        return assignment is null
            ? new MatchmakingStatusResponse(entry.Status)
            : new MatchmakingStatusResponse(MatchQueueEntryStatuses.Assigned, Assignment: assignment);
    }

    private static MatchmakingStatusResponse BuildQueuedResponse(MatchQueueEntry entry) => new(
        MatchQueueEntryStatuses.Queued,
        entry.CreatedAtUtc);
}
