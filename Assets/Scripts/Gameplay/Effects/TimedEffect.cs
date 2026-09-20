using System;

namespace LH.Main.Unity.Gameplay.Effects
{
    public readonly struct TimedEffect
    {
        public TimedEffect(Guid effectId, Guid playerId, EffectKind kind, int amountPerTick, double tickIntervalSeconds, double nextTickAtSeconds, double expiresAtSeconds)
        {
            EffectId = effectId;
            PlayerId = playerId;
            Kind = kind;
            AmountPerTick = amountPerTick;
            TickIntervalSeconds = tickIntervalSeconds;
            NextTickAtSeconds = nextTickAtSeconds;
            ExpiresAtSeconds = expiresAtSeconds;
        }

        public Guid EffectId { get; }
        public Guid PlayerId { get; }
        public EffectKind Kind { get; }
        public int AmountPerTick { get; }
        public double TickIntervalSeconds { get; }
        public double NextTickAtSeconds { get; }
        public double ExpiresAtSeconds { get; }

        public TimedEffect WithNextTick(double nextTickAtSeconds)
        {
            return new TimedEffect(EffectId, PlayerId, Kind, AmountPerTick, TickIntervalSeconds, nextTickAtSeconds, ExpiresAtSeconds);
        }
    }
}
