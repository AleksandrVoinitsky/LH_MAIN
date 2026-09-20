using System;
using System.Collections.Generic;

namespace LH.Main.Unity.Gameplay
{
    public sealed class ZoneRuntime
    {
        private readonly List<ZonePhase> _phases;
        private readonly Dictionary<Guid, double> _nextDamageTickByPlayer = new Dictionary<Guid, double>();

        public ZoneRuntime(IReadOnlyList<ZonePhase> phases)
        {
            if (phases == null)
                throw new ArgumentNullException(nameof(phases));

            if (phases.Count == 0)
                throw new ArgumentException("At least one zone phase is required.", nameof(phases));

            _phases = new List<ZonePhase>(phases);
            _phases.Sort((left, right) => left.StartsAtSeconds.CompareTo(right.StartsAtSeconds));
        }

        public ZonePhase GetActivePhase(double elapsedSeconds)
        {
            for (int i = 0; i < _phases.Count; i++)
            {
                ZonePhase phase = _phases[i];
                if (elapsedSeconds >= phase.StartsAtSeconds && elapsedSeconds < phase.EndsAtSeconds)
                    return phase;
            }

            return null;
        }

        public bool ShouldApplyDamage(Guid playerId, bool isInsideSafeVolume, double elapsedSeconds)
        {
            if (isInsideSafeVolume)
                return false;

            ZonePhase activePhase = GetActivePhase(elapsedSeconds);
            if (activePhase == null)
                return false;

            if (_nextDamageTickByPlayer.TryGetValue(playerId, out double nextDamageTick) && elapsedSeconds < nextDamageTick)
                return false;

            _nextDamageTickByPlayer[playerId] = elapsedSeconds + activePhase.TickIntervalSeconds;
            return true;
        }
    }
}
