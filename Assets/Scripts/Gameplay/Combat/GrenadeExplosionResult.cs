using System.Collections.Generic;

namespace LH.Main.Unity.Gameplay
{
    public readonly struct GrenadeExplosionResult
    {
        public GrenadeExplosionResult(bool exploded, IReadOnlyList<DamageEvent> damageEvents)
        {
            Exploded = exploded;
            DamageEvents = damageEvents;
        }

        public bool Exploded { get; }
        public IReadOnlyList<DamageEvent> DamageEvents { get; }
    }
}
