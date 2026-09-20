using System;
using System.Collections.Generic;

namespace LH.Main.Unity.Gameplay
{
    public static class MatchResultBuilder
    {
        public const string ExtractedRewardCode = "phase05_test_reward";

        public static MatchResultPayload Build(Guid matchId, string serverId, IReadOnlyList<PlayerResultSnapshot> players, DateTime completedAtUtc)
        {
            if (matchId == Guid.Empty)
                throw new ArgumentException("Match id is required.", nameof(matchId));

            var participants = new MatchParticipantResult[players == null ? 0 : players.Count];
            for (int index = 0; index < participants.Length; index++)
            {
                PlayerResultSnapshot player = players[index];
                string outcome = ToOutcome(player.LifeState);
                participants[index] = new MatchParticipantResult(
                    player.PlayerId,
                    outcome,
                    player.SurvivalSeconds,
                    player.DamageDealt,
                    player.DamageTaken,
                    ExtractedRewardCode);
            }

            return new MatchResultPayload(Guid.NewGuid(), matchId, serverId, completedAtUtc, participants);
        }

        private static string ToOutcome(PlayerLifeState lifeState)
        {
            switch (lifeState)
            {
                case PlayerLifeState.Extracted:
                    return "extracted";
                case PlayerLifeState.Dead:
                    return "dead";
                case PlayerLifeState.Disconnected:
                    return "disconnected";
                case PlayerLifeState.Wounded:
                    return "wounded";
                default:
                    return "alive";
            }
        }
    }
}
