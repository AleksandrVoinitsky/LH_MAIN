namespace LH.Main.Backend.Api.Persistence.Entities;

public sealed class PasswordCredential
{
    public Guid UserId { get; init; }

    public required string PasswordHash { get; init; }

    public User? User { get; init; }
}
