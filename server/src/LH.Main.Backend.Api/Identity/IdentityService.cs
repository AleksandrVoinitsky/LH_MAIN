using System.Text.RegularExpressions;
using LH.Main.Backend.Api.Persistence;
using LH.Main.Backend.Api.Persistence.Entities;
using LH.Main.Contracts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace LH.Main.Backend.Api.Identity;

public sealed partial class IdentityService(AppDbContext database, IPasswordHasher<User> passwordHasher)
{
    private static readonly User UnknownUser = new()
    {
        Id = Guid.Empty,
        NormalizedUsername = "UNKNOWN",
        CreatedAtUtc = DateTimeOffset.UnixEpoch
    };

    private static readonly string UnknownUserPasswordHash = new PasswordHasher<User>().HashPassword(UnknownUser, string.Empty);

    public async Task<RegistrationResult> RegisterAsync(DevCredentialsRequest request, CancellationToken cancellationToken)
    {
        if (!IsValid(request, out var username, out var password))
        {
            return RegistrationResult.Invalid;
        }

        var now = DateTimeOffset.UtcNow;
        var user = new User { Id = Guid.NewGuid(), NormalizedUsername = username.ToUpperInvariant(), CreatedAtUtc = now };
        var profile = new PlayerProfile { UserId = user.Id, Username = username, CreatedAtUtc = now };
        var credential = new PasswordCredential { UserId = user.Id, PasswordHash = passwordHasher.HashPassword(user, password) };

        database.Users.Add(user);
        database.PlayerProfiles.Add(profile);
        database.PasswordCredentials.Add(credential);

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return RegistrationResult.Duplicate;
        }

        return RegistrationResult.Success(new PlayerProfileResponse(user.Id, profile.Username, profile.CreatedAtUtc));
    }

    public async Task<LoginResult> LoginAsync(DevCredentialsRequest request, CancellationToken cancellationToken)
    {
        var normalizedUsername = request.Username?.Trim().ToUpperInvariant() ?? string.Empty;
        var user = await database.Users
            .Include(candidate => candidate.PasswordCredential)
            .SingleOrDefaultAsync(candidate => candidate.NormalizedUsername == normalizedUsername, cancellationToken);

        var password = request.Password ?? string.Empty;
        var credential = user?.PasswordCredential;
        var verificationResult = passwordHasher.VerifyHashedPassword(
            user ?? UnknownUser,
            credential?.PasswordHash ?? UnknownUserPasswordHash,
            password);
        return verificationResult is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded
            && user is not null
            ? LoginResult.Success(user.Id)
            : LoginResult.Invalid;
    }

    public async Task<PlayerProfileResponse?> GetProfileAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await database.PlayerProfiles
            .Where(profile => profile.UserId == userId)
            .Select(profile => new PlayerProfileResponse(profile.UserId, profile.Username, profile.CreatedAtUtc))
            .SingleOrDefaultAsync(cancellationToken);
    }

    private static bool IsValid(DevCredentialsRequest request, out string username, out string password)
    {
        username = request.Username?.Trim() ?? string.Empty;
        password = request.Password?.Trim() ?? string.Empty;
        return username.Length is >= 3 and <= 64
            && UsernamePattern().IsMatch(username)
            && password.Length is >= 12 and <= 128
            && password == request.Password;
    }

    [GeneratedRegex("^[A-Za-z0-9_]+$")]
    private static partial Regex UsernamePattern();
}

public sealed record RegistrationResult(RegistrationStatus Status, PlayerProfileResponse? Profile)
{
    public static RegistrationResult Invalid { get; } = new(RegistrationStatus.Invalid, null);

    public static RegistrationResult Duplicate { get; } = new(RegistrationStatus.Duplicate, null);

    public static RegistrationResult Success(PlayerProfileResponse profile) => new(RegistrationStatus.Success, profile);
}

public enum RegistrationStatus
{
    Success,
    Invalid,
    Duplicate
}

public sealed record LoginResult(Guid? UserId)
{
    public static LoginResult Invalid { get; } = new((Guid?)null);

    public static LoginResult Success(Guid userId) => new(userId);
}
