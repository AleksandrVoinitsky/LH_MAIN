using System;
using System.Collections.Generic;

namespace LH.Main.Unity.Gameplay
{
    public sealed class PlayerStateMachine
    {
        public const int MaxHealth = 100;
        public const int MaxWoundedPoints = 30;

        private readonly HashSet<Guid> _appliedDamageCorrelations = new HashSet<Guid>();

        private PlayerStateMachine(Guid playerId)
        {
            PlayerId = playerId;
            LifeState = PlayerLifeState.Alive;
        }

        public Guid PlayerId { get; }
        public PlayerLifeState LifeState { get; private set; }
        public int DamageTaken { get; private set; }
        public int Health => Math.Max(0, MaxHealth - DamageTaken);
        public int WoundedPoints => Math.Max(0, DamageTaken - MaxHealth);

        public static PlayerStateMachine Create(Guid playerId)
        {
            return new PlayerStateMachine(playerId);
        }

        public PlayerStateChange ApplyDamage(TechnicalDamageEvent damage)
        {
            PlayerLifeState oldState = LifeState;
            if (LifeState.IsTerminal())
                return PlayerStateChange.Reject("state_terminal", oldState);

            if (damage.TargetPlayerId != PlayerId)
                return PlayerStateChange.Reject("damage_target_mismatch", oldState);

            if (damage.Amount <= 0)
                return PlayerStateChange.Reject("damage_invalid", oldState);

            if (!_appliedDamageCorrelations.Add(damage.CorrelationId))
                return PlayerStateChange.Reject("damage_duplicate", oldState);

            DamageTaken += damage.Amount;
            LifeState = DamageTaken >= MaxHealth + MaxWoundedPoints
                ? PlayerLifeState.Dead
                : DamageTaken >= MaxHealth
                    ? PlayerLifeState.Wounded
                    : PlayerLifeState.Alive;

            return PlayerStateChange.Accept(oldState, LifeState);
        }

        public PlayerStateChange TryExtract()
        {
            PlayerLifeState oldState = LifeState;
            if (LifeState.IsTerminal())
                return PlayerStateChange.Reject("state_terminal", oldState);

            LifeState = PlayerLifeState.Extracted;
            return PlayerStateChange.Accept(oldState, LifeState);
        }
    }

    public readonly struct PlayerStateChange
    {
        public bool Accepted { get; }
        public string Reason { get; }
        public PlayerLifeState OldState { get; }
        public PlayerLifeState NewState { get; }

        private PlayerStateChange(bool accepted, string reason, PlayerLifeState oldState, PlayerLifeState newState)
        {
            Accepted = accepted;
            Reason = reason;
            OldState = oldState;
            NewState = newState;
        }

        public static PlayerStateChange Accept(PlayerLifeState oldState, PlayerLifeState newState)
        {
            return new PlayerStateChange(true, string.Empty, oldState, newState);
        }

        public static PlayerStateChange Reject(string reason, PlayerLifeState currentState)
        {
            return new PlayerStateChange(false, reason, currentState, currentState);
        }
    }
}
