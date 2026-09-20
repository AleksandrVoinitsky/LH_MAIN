namespace LH.Main.Backend.Api.Persistence.Entities;

public sealed class RewardTransaction
{
    public Guid Id { get; set; }

    public Guid PlayerId { get; set; }

    public Guid MatchId { get; set; }

    public Guid MatchResultId { get; set; }

    public string RewardCode { get; set; } = string.Empty;

    public int Amount { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
