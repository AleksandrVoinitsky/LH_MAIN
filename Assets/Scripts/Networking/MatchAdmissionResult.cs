using System;

namespace LH.Main.Unity.Networking
{
    public readonly struct MatchAdmissionResult
    {
        public bool IsAccepted { get; }
        public Guid MatchId { get; }
        public Guid PlayerId { get; }
        public string Reason { get; }

        private MatchAdmissionResult(bool isAccepted, Guid matchId, Guid playerId, string reason)
        {
            IsAccepted = isAccepted;
            MatchId = matchId;
            PlayerId = playerId;
            Reason = reason ?? string.Empty;
        }

        public static MatchAdmissionResult Accepted(Guid matchId, Guid playerId)
        {
            return new MatchAdmissionResult(true, matchId, playerId, string.Empty);
        }

        public static MatchAdmissionResult Rejected(string reason)
        {
            return new MatchAdmissionResult(false, Guid.Empty, Guid.Empty, reason);
        }
    }
}
