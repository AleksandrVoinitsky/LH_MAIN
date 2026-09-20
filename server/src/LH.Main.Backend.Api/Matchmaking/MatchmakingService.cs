using System.Security.Cryptography;
using System.Text;
using LH.Main.Backend.Api.Persistence;
using LH.Main.Backend.Api.Persistence.Entities;
using LH.Main.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LH.Main.Backend.Api.Matchmaking;

public sealed record MatchmakingCancelResult(bool ConflictAssigned, MatchmakingStatusResponse Response);

public sealed record TicketValidationEndpointResult(bool Unauthorized, TicketValidationResponse Response);

public sealed class MatchmakingService(
    AppDbContext database,
    TicketService ticketService,
    ISystemClock clock,
    IOptions<GameServerOptions> gameServerOptions)
{
    public async Task<MatchmakingStatusResponse> EnqueueAsync(Guid playerId, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        await LockMatchmakingAsync(cancellationToken);
        await LockPlayerAsync(playerId, cancellationToken);

        var activeEntry = await GetActiveEntryAsync(playerId, cancellationToken);
        if (activeEntry is not null)
        {
            var activeResponse = await ResolveActiveEntryAsync(activeEntry, cancellationToken);
            if (activeEntry.Status != MatchQueueEntryStatuses.Expired)
            {
                await transaction.CommitAsync(cancellationToken);
                return activeResponse;
            }
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

        var response = await TryAssignSlotAsync(entry, now, cancellationToken) ?? BuildQueuedResponse(entry);
        await transaction.CommitAsync(cancellationToken);
        return response;
    }

    public async Task<MatchmakingStatusResponse> GetStatusAsync(Guid playerId, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        await LockMatchmakingAsync(cancellationToken);
        await LockPlayerAsync(playerId, cancellationToken);

        var activeEntry = await GetActiveEntryAsync(playerId, cancellationToken);
        var response = activeEntry is null
            ? new MatchmakingStatusResponse("none")
            : await ResolveActiveEntryAsync(activeEntry, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return response;
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
            var activeResponse = await ExpireAssignmentIfNeededAsync(activeEntry, cancellationToken)
                ? new MatchmakingStatusResponse(MatchQueueEntryStatuses.Expired)
                : await BuildResponseAsync(activeEntry, null, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return activeEntry.Status == MatchQueueEntryStatuses.Expired
                ? new MatchmakingCancelResult(false, activeResponse)
                : new MatchmakingCancelResult(true, activeResponse);
        }

        activeEntry.Status = MatchQueueEntryStatuses.Cancelled;
        activeEntry.UpdatedAtUtc = clock.UtcNow;
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new MatchmakingCancelResult(false, new MatchmakingStatusResponse(MatchQueueEntryStatuses.Cancelled));
    }

    public async Task<TicketValidationEndpointResult> ValidateTicketAsync(
        TicketValidationRequest request,
        string serverKey,
        CancellationToken cancellationToken)
    {
        if (!ServerKeyMatches(serverKey))
        {
            return new TicketValidationEndpointResult(true, new TicketValidationResponse(false));
        }

        if (string.IsNullOrWhiteSpace(request.Ticket) || string.IsNullOrWhiteSpace(request.ServerId))
        {
            return InvalidTicketValidationResult();
        }

        var ticketHash = ticketService.Hash(request.Ticket);
        var now = clock.UtcNow;
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var ticket = await database.MatchTickets
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.TicketHash == ticketHash, cancellationToken);

        if (ticket is null
            || ticket.MatchId != request.MatchId
            || ticket.PlayerId != request.PlayerId
            || ticket.ServerId != request.ServerId
            || ticket.ExpiresAtUtc <= now
            || ticket.ConsumedAtUtc is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return InvalidTicketValidationResult();
        }

        var consumedRows = await database.MatchTickets
            .Where(candidate => candidate.Id == ticket.Id && candidate.ConsumedAtUtc == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(candidate => candidate.ConsumedAtUtc, now), cancellationToken);
        if (consumedRows != 1)
        {
            await transaction.CommitAsync(cancellationToken);
            return InvalidTicketValidationResult();
        }

        await database.Matches
            .Where(match => match.Id == ticket.MatchId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(match => match.Status, MatchSessionStatuses.TicketValidated)
                .SetProperty(match => match.UpdatedAtUtc, now), cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new TicketValidationEndpointResult(
            false,
            new TicketValidationResponse(true, ticket.MatchId, ticket.PlayerId));
    }

    private Task<int> LockPlayerAsync(Guid playerId, CancellationToken cancellationToken) => database.Database.ExecuteSqlInterpolatedAsync(
        $"SELECT pg_advisory_xact_lock(hashtextextended({playerId.ToString()}, 0))",
        cancellationToken);

    private Task<int> LockMatchmakingAsync(CancellationToken cancellationToken) => database.Database.ExecuteSqlRawAsync(
        "SELECT pg_advisory_xact_lock(hashtextextended('matchmaking-assignment', 0))",
        cancellationToken);

    private bool ServerKeyMatches(string serverKey)
    {
        var configuredKey = gameServerOptions.Value.SharedKey;
        if (string.IsNullOrWhiteSpace(configuredKey) || string.IsNullOrEmpty(serverKey))
        {
            return false;
        }

        var configuredBytes = Encoding.UTF8.GetBytes(configuredKey);
        var suppliedBytes = Encoding.UTF8.GetBytes(serverKey);
        return suppliedBytes.Length == configuredBytes.Length
            && CryptographicOperations.FixedTimeEquals(suppliedBytes, configuredBytes);
    }

    private static TicketValidationEndpointResult InvalidTicketValidationResult() => new(
        false,
        new TicketValidationResponse(false));

    private Task<MatchQueueEntry?> GetActiveEntryAsync(Guid playerId, CancellationToken cancellationToken) => database.MatchQueueEntries
        .Where(entry => entry.PlayerId == playerId
            && (entry.Status == MatchQueueEntryStatuses.Queued || entry.Status == MatchQueueEntryStatuses.Assigned))
        .OrderByDescending(entry => entry.UpdatedAtUtc)
        .FirstOrDefaultAsync(cancellationToken);

    private async Task<MatchmakingStatusResponse> ResolveActiveEntryAsync(
        MatchQueueEntry entry,
        CancellationToken cancellationToken)
    {
        if (entry.Status == MatchQueueEntryStatuses.Assigned
            && await ExpireAssignmentIfNeededAsync(entry, cancellationToken))
        {
            return new MatchmakingStatusResponse(MatchQueueEntryStatuses.Expired);
        }

        if (entry.Status == MatchQueueEntryStatuses.Queued)
        {
            return await TryAssignSlotAsync(entry, clock.UtcNow, cancellationToken) ?? BuildQueuedResponse(entry);
        }

        return await BuildResponseAsync(entry, null, cancellationToken);
    }

    private async Task<bool> ExpireAssignmentIfNeededAsync(MatchQueueEntry entry, CancellationToken cancellationToken)
    {
        if (entry.AssignedMatchId is not { } matchId)
        {
            return false;
        }

        var now = clock.UtcNow;
        var ticket = await database.MatchTickets
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.MatchId == matchId && candidate.PlayerId == entry.PlayerId, cancellationToken);
        if (ticket is null || ticket.ConsumedAtUtc is not null || ticket.ExpiresAtUtc > now)
        {
            return false;
        }

        await database.Matches
            .Where(match => match.Id == matchId && match.Status == MatchSessionStatuses.Reserved)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(match => match.Status, MatchSessionStatuses.Expired)
                .SetProperty(match => match.UpdatedAtUtc, now), cancellationToken);
        var trackedMatch = database.Matches.Local.FirstOrDefault(match => match.Id == matchId);
        if (trackedMatch is not null)
        {
            trackedMatch.Status = MatchSessionStatuses.Expired;
            trackedMatch.UpdatedAtUtc = now;
        }

        await database.GameServerSlots
            .Where(slot => slot.ServerId == ticket.ServerId && slot.CurrentMatchId == matchId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(slot => slot.Status, GameServerSlotStatuses.Available)
                .SetProperty(slot => slot.CurrentMatchId, (Guid?)null)
                .SetProperty(slot => slot.UpdatedAtUtc, now), cancellationToken);

        var trackedSlot = database.GameServerSlots.Local.FirstOrDefault(candidate => candidate.ServerId == ticket.ServerId);
        if (trackedSlot is not null && trackedSlot.CurrentMatchId == matchId)
        {
            trackedSlot.Status = GameServerSlotStatuses.Available;
            trackedSlot.CurrentMatchId = null;
            trackedSlot.UpdatedAtUtc = now;
        }

        entry.Status = MatchQueueEntryStatuses.Expired;
        entry.UpdatedAtUtc = now;
        await database.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<MatchmakingStatusResponse?> TryAssignSlotAsync(
        MatchQueueEntry entry,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await ExpireStaleAssignmentsAsync(now, cancellationToken);

        var occupiedSlot = await TryAssignExistingMatchAsync(entry, now, cancellationToken);
        if (occupiedSlot is not null)
        {
            return occupiedSlot;
        }

        var slot = await database.GameServerSlots
            .AsNoTracking()
            .Where(candidate => candidate.Status == GameServerSlotStatuses.Available)
            .OrderBy(candidate => candidate.ServerId)
            .FirstOrDefaultAsync(cancellationToken);

        if (slot is null)
        {
            return null;
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
            return null;
        }

        var trackedSlot = database.GameServerSlots.Local.FirstOrDefault(candidate => candidate.ServerId == slot.ServerId);
        if (trackedSlot is not null)
        {
            trackedSlot.Status = GameServerSlotStatuses.Occupied;
            trackedSlot.CurrentMatchId = matchId;
            trackedSlot.UpdatedAtUtc = now;
        }

        var ticket = ticketService.Issue(matchId, entry.PlayerId, slot.ServerId, gameServerOptions.Value.GetTicketLifetime());
        database.Matches.Add(new MatchSession
        {
            Id = matchId,
            PlayerId = entry.PlayerId,
            ServerId = slot.ServerId,
            Status = MatchSessionStatuses.Reserved,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });

        database.MatchTickets.Add(new MatchTicket
        {
            Id = Guid.NewGuid(),
            MatchId = matchId,
            PlayerId = entry.PlayerId,
            ServerId = slot.ServerId,
            TicketHash = ticket.TicketHash,
            ExpiresAtUtc = ticket.ExpiresAtUtc,
            CreatedAtUtc = now
        });

        entry.Status = MatchQueueEntryStatuses.Assigned;
        entry.AssignedMatchId = matchId;
        entry.UpdatedAtUtc = now;
        await database.SaveChangesAsync(cancellationToken);

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

    private async Task<MatchmakingStatusResponse?> TryAssignExistingMatchAsync(
        MatchQueueEntry entry,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var activeSlots = await (
            from slot in database.GameServerSlots.AsNoTracking()
            join match in database.Matches.AsNoTracking() on slot.CurrentMatchId equals match.Id
            where slot.Status == GameServerSlotStatuses.Occupied
                && (match.Status == MatchSessionStatuses.Reserved || match.Status == MatchSessionStatuses.TicketValidated)
            orderby slot.ServerId
            select new
            {
                slot.ServerId,
                slot.PublicHost,
                slot.PublicPort,
                MatchId = match.Id
            })
            .ToArrayAsync(cancellationToken);

        foreach (var slot in activeSlots)
        {
            var assignedPlayers = await database.MatchQueueEntries
                .AsNoTracking()
                .CountAsync(candidate => candidate.AssignedMatchId == slot.MatchId
                    && candidate.Status == MatchQueueEntryStatuses.Assigned, cancellationToken);
            if (assignedPlayers >= gameServerOptions.Value.MaxPlayersPerMatch)
            {
                continue;
            }

            var ticket = ticketService.Issue(slot.MatchId, entry.PlayerId, slot.ServerId, gameServerOptions.Value.GetTicketLifetime());
            database.MatchTickets.Add(new MatchTicket
            {
                Id = Guid.NewGuid(),
                MatchId = slot.MatchId,
                PlayerId = entry.PlayerId,
                ServerId = slot.ServerId,
                TicketHash = ticket.TicketHash,
                ExpiresAtUtc = ticket.ExpiresAtUtc,
                CreatedAtUtc = now
            });

            entry.Status = MatchQueueEntryStatuses.Assigned;
            entry.AssignedMatchId = slot.MatchId;
            entry.UpdatedAtUtc = now;
            await database.SaveChangesAsync(cancellationToken);

            return new MatchmakingStatusResponse(
                MatchQueueEntryStatuses.Assigned,
                Assignment: new MatchAssignmentResponse(
                    slot.MatchId,
                    slot.ServerId,
                    slot.PublicHost,
                    slot.PublicPort,
                    ticket.PlaintextTicket,
                    ticket.ExpiresAtUtc));
        }

        return null;
    }

    private async Task ExpireStaleAssignmentsAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var expiredAssignments = await (
            from ticket in database.MatchTickets.AsNoTracking()
            join match in database.Matches.AsNoTracking() on ticket.MatchId equals match.Id
            where ticket.ConsumedAtUtc == null
                && ticket.ExpiresAtUtc <= now
                && match.Status == MatchSessionStatuses.Reserved
            select new { ticket.MatchId, ticket.ServerId })
            .ToArrayAsync(cancellationToken);

        foreach (var expired in expiredAssignments)
        {
            await database.Matches
                .Where(match => match.Id == expired.MatchId && match.Status == MatchSessionStatuses.Reserved)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(match => match.Status, MatchSessionStatuses.Expired)
                    .SetProperty(match => match.UpdatedAtUtc, now), cancellationToken);

            await database.MatchQueueEntries
                .Where(queueEntry => queueEntry.AssignedMatchId == expired.MatchId
                    && queueEntry.Status == MatchQueueEntryStatuses.Assigned)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(queueEntry => queueEntry.Status, MatchQueueEntryStatuses.Expired)
                    .SetProperty(queueEntry => queueEntry.UpdatedAtUtc, now), cancellationToken);

            await database.GameServerSlots
                .Where(slot => slot.ServerId == expired.ServerId && slot.CurrentMatchId == expired.MatchId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(slot => slot.Status, GameServerSlotStatuses.Available)
                    .SetProperty(slot => slot.CurrentMatchId, (Guid?)null)
                    .SetProperty(slot => slot.UpdatedAtUtc, now), cancellationToken);

            var trackedMatch = database.Matches.Local.FirstOrDefault(match => match.Id == expired.MatchId);
            if (trackedMatch is not null)
            {
                trackedMatch.Status = MatchSessionStatuses.Expired;
                trackedMatch.UpdatedAtUtc = now;
            }

            var trackedEntry = database.MatchQueueEntries.Local.FirstOrDefault(queueEntry => queueEntry.AssignedMatchId == expired.MatchId);
            if (trackedEntry is not null)
            {
                trackedEntry.Status = MatchQueueEntryStatuses.Expired;
                trackedEntry.UpdatedAtUtc = now;
            }

            var trackedSlot = database.GameServerSlots.Local.FirstOrDefault(slot => slot.ServerId == expired.ServerId);
            if (trackedSlot is not null && trackedSlot.CurrentMatchId == expired.MatchId)
            {
                trackedSlot.Status = GameServerSlotStatuses.Available;
                trackedSlot.CurrentMatchId = null;
                trackedSlot.UpdatedAtUtc = now;
            }
        }
    }

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
                && ticket.PlayerId == entry.PlayerId
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
