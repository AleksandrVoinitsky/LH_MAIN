namespace LH.Main.Backend.Api.Persistence.Entities;

public sealed class MatchQueueEntry
{
    public Guid Id { get; set; }

    public Guid PlayerId { get; set; }

    public string Status { get; set; } = MatchQueueEntryStatuses.Queued;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public Guid? AssignedMatchId { get; set; }
}

public static class MatchQueueEntryStatuses
{
    public const string Queued = "queued";
    public const string Assigned = "assigned";
    public const string Cancelled = "cancelled";
    public const string Expired = "expired";
}
