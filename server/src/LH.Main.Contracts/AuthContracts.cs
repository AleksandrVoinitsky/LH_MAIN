namespace LH.Main.Contracts;

public sealed record DevCredentialsRequest(string? Username, string? Password);

public sealed record PlayerProfileResponse(Guid UserId, string Username, DateTimeOffset CreatedAtUtc);
