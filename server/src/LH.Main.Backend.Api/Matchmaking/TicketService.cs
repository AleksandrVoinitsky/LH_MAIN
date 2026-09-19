using System.Security.Cryptography;
using System.Text;

namespace LH.Main.Backend.Api.Matchmaking;

public sealed record TicketIssueResult(string PlaintextTicket, string TicketHash, DateTimeOffset ExpiresAtUtc);

public sealed class TicketService(ISystemClock clock)
{
    public TicketIssueResult Issue(Guid matchId, Guid playerId, string serverId, TimeSpan lifetime)
    {
        var plaintextTicket = EncodeBase64Url(RandomNumberGenerator.GetBytes(32));
        var ticketHash = Hash(plaintextTicket);
        var expiresAtUtc = clock.UtcNow.Add(lifetime);

        return new TicketIssueResult(plaintextTicket, ticketHash, expiresAtUtc);
    }

    public string Hash(string plaintextTicket)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(plaintextTicket));

        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string EncodeBase64Url(byte[] bytes) => Convert.ToBase64String(bytes)
        .TrimEnd('=')
        .Replace('+', '-')
        .Replace('/', '_');
}
