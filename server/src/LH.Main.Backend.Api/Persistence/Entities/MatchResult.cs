namespace LH.Main.Backend.Api.Persistence.Entities;

public sealed class MatchResult
{
    public Guid Id { get; set; }

    public Guid MatchId { get; set; }

    public string ServerId { get; set; } = string.Empty;

    public DateTimeOffset CompletedAtUtc { get; set; }

    public DateTimeOffset ReceivedAtUtc { get; set; }

    public string PayloadHash { get; set; } = string.Empty;
}
