namespace LH.Main.Backend.Api.Persistence.Entities;

public sealed class GameServerSlot
{
    public string ServerId { get; set; } = string.Empty;

    public string PublicHost { get; set; } = string.Empty;

    public int PublicPort { get; set; }

    public string Status { get; set; } = GameServerSlotStatuses.Available;

    public Guid? CurrentMatchId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public static class GameServerSlotStatuses
{
    public const string Available = "available";
    public const string Occupied = "occupied";
    public const string Disabled = "disabled";
}
