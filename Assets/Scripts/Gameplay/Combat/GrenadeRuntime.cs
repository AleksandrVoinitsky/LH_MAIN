using System;
using System.Collections.Generic;
using UnityEngine;

namespace LH.Main.Unity.Gameplay
{
    public readonly struct GrenadeTarget
    {
        public GrenadeTarget(Guid playerId, Vector3 position)
        {
            PlayerId = playerId;
            Position = position;
        }

        public Guid PlayerId { get; }
        public Vector3 Position { get; }
    }

    public sealed class GrenadeRuntime
    {
        private const double TimeEpsilon = 0.0000001d;
        private static readonly DamageEvent[] NoDamageEvents = new DamageEvent[0];

        private readonly Guid _grenadeId;
        private readonly Guid? _sourcePlayerId;
        private readonly Vector3 _spawnPosition;
        private readonly double _spawnTimeSeconds;
        private readonly GrenadeDefinition _definition;

        public GrenadeRuntime(GrenadeThrowRequest request, GrenadeDefinition definition, double spawnTimeSeconds)
        {
            _grenadeId = request.GrenadeId;
            _sourcePlayerId = request.SourcePlayerId;
            _spawnPosition = request.SpawnPosition;
            _spawnTimeSeconds = spawnTimeSeconds;
            _definition = definition ?? throw new ArgumentNullException(nameof(definition));
        }

        public bool HasExploded { get; private set; }

        public GrenadeExplosionResult Tick(double serverTimeSeconds, IReadOnlyList<GrenadeTarget> targets)
        {
            if (HasExploded || serverTimeSeconds + TimeEpsilon < _spawnTimeSeconds + _definition.FuseSeconds)
                return new GrenadeExplosionResult(false, NoDamageEvents);

            HasExploded = true;
            var damageEvents = new List<DamageEvent>();
            if (targets != null)
            {
                for (int i = 0; i < targets.Count; i++)
                {
                    GrenadeTarget target = targets[i];
                    float distance = Vector3.Distance(_spawnPosition, target.Position);
                    if (distance > _definition.Radius)
                        continue;

                    double falloff = 1d - (distance / _definition.Radius);
                    int baseAmount = Math.Max(1, (int)Math.Ceiling(_definition.MaxDamage * falloff));
                    damageEvents.Add(new DamageEvent(_grenadeId, _sourcePlayerId, target.PlayerId, baseAmount, BodyZone.Torso, "grenade"));
                }
            }

            return new GrenadeExplosionResult(true, damageEvents.ToArray());
        }
    }
}
