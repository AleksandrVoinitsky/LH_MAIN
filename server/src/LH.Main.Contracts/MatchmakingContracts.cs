namespace LH.Main.Contracts;

public sealed record MatchmakingStatusResponse(
    string Status,
    DateTimeOffset? QueuedAtUtc = null,
    MatchAssignmentResponse? Assignment = null);

public sealed record MatchAssignmentResponse(
    Guid MatchId,
    string ServerId,
    string PublicHost,
    int PublicPort,
    string Ticket,
    DateTimeOffset TicketExpiresAtUtc);

public sealed record TicketValidationRequest(
    string? Ticket,
    Guid MatchId,
    Guid PlayerId,
    string? ServerId);

public sealed record TicketValidationResponse(
    bool Valid,
    Guid? MatchId = null,
    Guid? PlayerId = null);
