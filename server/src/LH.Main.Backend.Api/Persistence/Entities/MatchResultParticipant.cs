namespace LH.Main.Backend.Api.Persistence.Entities;

public sealed class MatchResultParticipant
{
    public Guid Id { get; set; }

    public Guid MatchResultId { get; set; }

    public Guid PlayerId { get; set; }

    public string Outcome { get; set; } = string.Empty;

    public int SurvivedSeconds { get; set; }

    public int DamageTaken { get; set; }

    public int DamageApplied { get; set; }

    public string RewardCode { get; set; } = string.Empty;
}
