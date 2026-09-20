using System;

namespace LH.Main.Unity.Gameplay
{
    public readonly struct TechnicalDamageEvent
    {
        public Guid CorrelationId { get; }
        public Guid? SourcePlayerId { get; }
        public Guid TargetPlayerId { get; }
        public int Amount { get; }
        public string Kind { get; }

        public TechnicalDamageEvent(Guid correlationId, Guid? sourcePlayerId, Guid targetPlayerId, int amount, string kind)
        {
            CorrelationId = correlationId;
            SourcePlayerId = sourcePlayerId;
            TargetPlayerId = targetPlayerId;
            Amount = amount;
            Kind = kind ?? string.Empty;
        }
    }
}
