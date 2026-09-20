using System;

namespace LH.Main.Unity.Gameplay
{
    public readonly struct HealingEvent
    {
        public HealingEvent(Guid correlationId, Guid targetPlayerId, int amount, string kind)
        {
            CorrelationId = correlationId;
            TargetPlayerId = targetPlayerId;
            Amount = amount;
            Kind = kind;
        }

        public Guid CorrelationId { get; }
        public Guid TargetPlayerId { get; }
        public int Amount { get; }
        public string Kind { get; }
    }
}
