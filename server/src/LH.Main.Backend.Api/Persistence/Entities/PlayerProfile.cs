namespace LH.Main.Backend.Api.Persistence.Entities;

public sealed class PlayerProfile
{
    public Guid UserId { get; init; }

    public required string Username { get; init; }

    public DateTimeOffset CreatedAtUtc { get; init; }

    public User? User { get; init; }
}
