using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LH.Main.Backend.Api.Matchmaking;
using LH.Main.Backend.Api.Persistence;
using LH.Main.Backend.Api.Persistence.Entities;
using LH.Main.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LH.Main.Backend.Api.MatchResults;

public sealed class MatchResultService(
    AppDbContext database,
    ISystemClock clock,
    IOptions<GameServerOptions> options)
{
    private const string RewardCode = "phase05_test_reward";

    public async Task<MatchResultSubmissionResult> SubmitAsync(
        MatchResultSubmissionRequest request,
        string serverKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.Value.SharedKey) || serverKey != options.Value.SharedKey)
            return MatchResultSubmissionResult.RejectedUnauthorized();

        string? validationError = ValidateRequest(request);
        if (validationError is not null)
            return MatchResultSubmissionResult.Conflict(validationError);

        var match = await database.Matches.SingleOrDefaultAsync(match => match.Id == request.MatchId, cancellationToken);
        if (match is null)
            return MatchResultSubmissionResult.Conflict("match_unknown");
        if (!string.Equals(match.ServerId, request.ServerId, StringComparison.Ordinal))
            return MatchResultSubmissionResult.Conflict("match_server_mismatch");

        string payloadHash = ComputePayloadHash(request);
        var existing = await database.MatchResults.SingleOrDefaultAsync(result => result.Id == request.ResultId, cancellationToken);
        if (existing is not null)
        {
            if (existing.MatchId == request.MatchId && string.Equals(existing.PayloadHash, payloadHash, StringComparison.Ordinal))
            {
                return MatchResultSubmissionResult.Accepted(new MatchResultSubmissionResponse(true, request.ResultId, 0, true));
            }

            return MatchResultSubmissionResult.Conflict("result_conflict");
        }

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        database.MatchResults.Add(new MatchResult
        {
            Id = request.ResultId,
            MatchId = request.MatchId,
            ServerId = request.ServerId,
            CompletedAtUtc = request.CompletedAtUtc,
            ReceivedAtUtc = clock.UtcNow,
            PayloadHash = payloadHash
        });

        foreach (var participant in request.Participants)
        {
            database.MatchResultParticipants.Add(new MatchResultParticipant
            {
                Id = Guid.NewGuid(),
                MatchResultId = request.ResultId,
                PlayerId = participant.PlayerId,
                Outcome = participant.Outcome,
                SurvivedSeconds = participant.SurvivedSeconds,
                DamageTaken = participant.DamageTaken,
                DamageApplied = participant.DamageApplied,
                RewardCode = participant.RewardCode
            });
            database.RewardTransactions.Add(new RewardTransaction
            {
                Id = Guid.NewGuid(),
                PlayerId = participant.PlayerId,
                MatchId = request.MatchId,
                MatchResultId = request.ResultId,
                RewardCode = participant.RewardCode,
                Amount = RewardAmount(participant.Outcome),
                CreatedAtUtc = clock.UtcNow
            });
        }

        match.Status = MatchSessionStatuses.Completed;
        match.UpdatedAtUtc = clock.UtcNow;
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return MatchResultSubmissionResult.Accepted(new MatchResultSubmissionResponse(true, request.ResultId, request.Participants.Count, false));
    }

    private static string? ValidateRequest(MatchResultSubmissionRequest request)
    {
        if (request.MatchId == Guid.Empty || request.ResultId == Guid.Empty || string.IsNullOrWhiteSpace(request.ServerId))
            return "result_invalid";
        if (request.Participants.Count == 0)
            return "participants_required";
        foreach (var participant in request.Participants)
        {
            if (participant.PlayerId == Guid.Empty)
                return "participant_invalid";
            if (participant.Outcome is not ("extracted" or "dead" or "disconnected"))
                return "outcome_unsupported";
            if (!string.Equals(participant.RewardCode, RewardCode, StringComparison.Ordinal))
                return "reward_code_unsupported";
        }

        return null;
    }

    private static int RewardAmount(string outcome) => string.Equals(outcome, "extracted", StringComparison.Ordinal) ? 10 : 1;

    private static string ComputePayloadHash(MatchResultSubmissionRequest request)
    {
        string canonical = JsonSerializer.Serialize(request, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }
}

public sealed record MatchResultSubmissionResult(
    bool Unauthorized,
    string? ConflictCode,
    MatchResultSubmissionResponse? Response)
{
    public static MatchResultSubmissionResult RejectedUnauthorized() => new(true, null, null);

    public static MatchResultSubmissionResult Conflict(string code) => new(false, code, null);

    public static MatchResultSubmissionResult Accepted(MatchResultSubmissionResponse response) => new(false, null, response);
}
