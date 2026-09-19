namespace LH.Main.Backend.Api.Persistence.Entities;

public sealed class MatchSession
{
    public Guid Id { get; set; }

    public Guid PlayerId { get; set; }

    public string ServerId { get; set; } = string.Empty;

    public string Status { get; set; } = MatchSessionStatuses.Reserved;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public static class MatchSessionStatuses
{
    public const string Reserved = "reserved";
    public const string TicketValidated = "ticket_validated";
    public const string Expired = "expired";
}
