using System;

namespace LH.Main.Unity.Gameplay
{
    public readonly struct DamageEvent
    {
        public DamageEvent(Guid correlationId, Guid? sourcePlayerId, Guid targetPlayerId, int baseAmount, BodyZone bodyZone, string kind)
        {
            CorrelationId = correlationId;
            SourcePlayerId = sourcePlayerId;
            TargetPlayerId = targetPlayerId;
            BaseAmount = baseAmount;
            BodyZone = bodyZone;
            Kind = kind;
        }

        public Guid CorrelationId { get; }
        public Guid? SourcePlayerId { get; }
        public Guid TargetPlayerId { get; }
        public int BaseAmount { get; }
        public BodyZone BodyZone { get; }
        public string Kind { get; }
    }
}
