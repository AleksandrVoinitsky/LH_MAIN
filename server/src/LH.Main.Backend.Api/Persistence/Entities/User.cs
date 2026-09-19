namespace LH.Main.Backend.Api.Persistence.Entities;

public sealed class User
{
    public Guid Id { get; init; }

    public required string NormalizedUsername { get; init; }

    public DateTimeOffset CreatedAtUtc { get; init; }

    public PasswordCredential? PasswordCredential { get; init; }

    public PlayerProfile? Profile { get; init; }
}
