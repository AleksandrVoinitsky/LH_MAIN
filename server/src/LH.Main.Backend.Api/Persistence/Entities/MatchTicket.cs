namespace LH.Main.Backend.Api.Persistence.Entities;

public sealed class MatchTicket
{
    public Guid Id { get; set; }

    public Guid MatchId { get; set; }

    public Guid PlayerId { get; set; }

    public string ServerId { get; set; } = string.Empty;

    public string TicketHash { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAtUtc { get; set; }

    public DateTimeOffset? ConsumedAtUtc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
