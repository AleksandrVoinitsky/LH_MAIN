using System;
using System.Collections.Generic;
using System.Text;

namespace LH.Main.Unity.Gameplay
{
    public readonly struct PlayerResultSnapshot
    {
        public Guid PlayerId { get; }
        public PlayerLifeState LifeState { get; }
        public int SurvivalSeconds { get; }
        public int DamageDealt { get; }
        public int DamageTaken { get; }

        public PlayerResultSnapshot(Guid playerId, PlayerLifeState lifeState, int survivalSeconds, int damageDealt, int damageTaken)
        {
            PlayerId = playerId;
            LifeState = lifeState;
            SurvivalSeconds = survivalSeconds;
            DamageDealt = damageDealt;
            DamageTaken = damageTaken;
        }
    }

    public sealed class MatchParticipantResult
    {
        public MatchParticipantResult(Guid playerId, string outcome, int survivalSeconds, int damageDealt, int damageTaken, string rewardCode)
        {
            PlayerId = playerId;
            Outcome = outcome ?? string.Empty;
            SurvivalSeconds = survivalSeconds;
            DamageDealt = damageDealt;
            DamageTaken = damageTaken;
            RewardCode = rewardCode ?? string.Empty;
        }

        public Guid PlayerId { get; }
        public string Outcome { get; }
        public int SurvivalSeconds { get; }
        public int DamageDealt { get; }
        public int DamageTaken { get; }
        public string RewardCode { get; }
    }

    public sealed class MatchResultPayload
    {
        public MatchResultPayload(Guid resultId, Guid matchId, string serverId, DateTime completedAtUtc, IReadOnlyList<MatchParticipantResult> participants)
        {
            ResultId = resultId;
            MatchId = matchId;
            ServerId = serverId ?? string.Empty;
            CompletedAtUtc = completedAtUtc.Kind == DateTimeKind.Utc ? completedAtUtc : completedAtUtc.ToUniversalTime();
            Participants = participants ?? Array.Empty<MatchParticipantResult>();
        }

        public Guid ResultId { get; }
        public Guid MatchId { get; }
        public string ServerId { get; }
        public DateTime CompletedAtUtc { get; }
        public IReadOnlyList<MatchParticipantResult> Participants { get; }

        public static MatchResultPayload EmptyForTests(Guid matchId, string serverId)
        {
            return new MatchResultPayload(Guid.NewGuid(), matchId, serverId, DateTime.UtcNow, Array.Empty<MatchParticipantResult>());
        }

        public string ToJson()
        {
            var builder = new StringBuilder();
            builder.Append('{');
            AppendJsonProperty(builder, "resultId", ResultId.ToString());
            builder.Append(',');
            AppendJsonProperty(builder, "matchId", MatchId.ToString());
            builder.Append(',');
            AppendJsonProperty(builder, "serverId", ServerId);
            builder.Append(',');
            AppendJsonProperty(builder, "completedAtUtc", CompletedAtUtc.ToString("O"));
            builder.Append(",\"participants\":[");

            for (int index = 0; index < Participants.Count; index++)
            {
                if (index > 0)
                    builder.Append(',');

                MatchParticipantResult participant = Participants[index];
                builder.Append('{');
                AppendJsonProperty(builder, "playerId", participant.PlayerId.ToString());
                builder.Append(',');
                AppendJsonProperty(builder, "outcome", participant.Outcome);
                builder.Append(",\"survivalSeconds\":");
                builder.Append(participant.SurvivalSeconds);
                builder.Append(",\"damageDealt\":");
                builder.Append(participant.DamageDealt);
                builder.Append(",\"damageTaken\":");
                builder.Append(participant.DamageTaken);
                builder.Append(',');
                AppendJsonProperty(builder, "rewardCode", participant.RewardCode);
                builder.Append('}');
            }

            builder.Append("]}");
            return builder.ToString();
        }

        private static void AppendJsonProperty(StringBuilder builder, string name, string value)
        {
            builder.Append('"');
            builder.Append(name);
            builder.Append("\":\"");
            builder.Append(EscapeJson(value));
            builder.Append('"');
        }

        private static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            var builder = new StringBuilder(value.Length);
            foreach (char character in value)
            {
                switch (character)
                {
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '"':
                        builder.Append("\\\"");
                        break;
                    case '\b':
                        builder.Append("\\b");
                        break;
                    case '\f':
                        builder.Append("\\f");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    default:
                        if (character < ' ')
                            builder.Append("\\u" + ((int)character).ToString("x4"));
                        else
                            builder.Append(character);
                        break;
                }
            }

            return builder.ToString();
        }
    }

    public readonly struct MatchResultSubmissionOutcome
    {
        public bool Accepted { get; }
        public Guid ResultId { get; }
        public int NewRewardTransactions { get; }
        public bool Duplicate { get; }
        public string Reason { get; }

        private MatchResultSubmissionOutcome(bool accepted, Guid resultId, int newRewardTransactions, bool duplicate, string reason)
        {
            Accepted = accepted;
            ResultId = resultId;
            NewRewardTransactions = newRewardTransactions;
            Duplicate = duplicate;
            Reason = reason ?? string.Empty;
        }

        public static MatchResultSubmissionOutcome AcceptedResult(Guid resultId, int newRewardTransactions, bool duplicate)
        {
            return new MatchResultSubmissionOutcome(true, resultId, newRewardTransactions, duplicate, string.Empty);
        }

        public static MatchResultSubmissionOutcome Rejected(string reason)
        {
            return new MatchResultSubmissionOutcome(false, Guid.Empty, 0, false, reason);
        }
    }

    public readonly struct MatchResultSubmissionResponse
    {
        public int StatusCode { get; }
        public string Body { get; }

        public MatchResultSubmissionResponse(int statusCode, string body)
        {
            StatusCode = statusCode;
            Body = body ?? string.Empty;
        }
    }
}
