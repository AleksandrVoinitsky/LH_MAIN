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
        private readonly double _spawnTimeSeconds;
        private readonly GrenadeDefinition _definition;
        private readonly Vector3 _velocity;
        private double _lastSimulatedTimeSeconds;
        private bool _hasCollided;

        public GrenadeRuntime(GrenadeThrowRequest request, GrenadeDefinition definition, double spawnTimeSeconds)
        {
            if (!IsFinite(request.SpawnPosition))
                throw new ArgumentException("grenade_spawn_position_invalid", nameof(request));

            if (!IsFinite(request.ThrowDirection) || request.ThrowDirection.sqrMagnitude <= TimeEpsilon)
                throw new ArgumentException("grenade_throw_direction_invalid", nameof(request));

            _grenadeId = request.GrenadeId;
            _sourcePlayerId = request.SourcePlayerId;
            _spawnTimeSeconds = spawnTimeSeconds;
            _definition = definition ?? throw new ArgumentNullException(nameof(definition));
            CurrentPosition = request.SpawnPosition;
            _velocity = request.ThrowDirection.normalized * _definition.InitialSpeed;
            _lastSimulatedTimeSeconds = spawnTimeSeconds;
        }

        public bool HasExploded { get; private set; }
        public Vector3 CurrentPosition { get; private set; }

        public GrenadeExplosionResult Tick(double serverTimeSeconds, IReadOnlyList<GrenadeTarget> targets)
        {
            if (HasExploded)
                return new GrenadeExplosionResult(false, NoDamageEvents);

            SimulateMovement(serverTimeSeconds);

            if (serverTimeSeconds + TimeEpsilon < _spawnTimeSeconds + _definition.FuseSeconds)
                return new GrenadeExplosionResult(false, NoDamageEvents);

            HasExploded = true;
            var damageEvents = new List<DamageEvent>();
            if (targets != null)
            {
                for (int i = 0; i < targets.Count; i++)
                {
                    GrenadeTarget target = targets[i];
                    float distance = Vector3.Distance(CurrentPosition, target.Position);
                    if (distance > _definition.Radius)
                        continue;

                    double falloff = 1d - (distance / _definition.Radius);
                    int baseAmount = Math.Max(1, (int)Math.Ceiling(_definition.MaxDamage * falloff));
                    damageEvents.Add(new DamageEvent(_grenadeId, _sourcePlayerId, target.PlayerId, baseAmount, BodyZone.Torso, "grenade"));
                }
            }

            return new GrenadeExplosionResult(true, damageEvents.ToArray());
        }

        private void SimulateMovement(double serverTimeSeconds)
        {
            if (_hasCollided || serverTimeSeconds <= _lastSimulatedTimeSeconds)
                return;

            float deltaTime = (float)(serverTimeSeconds - _lastSimulatedTimeSeconds);
            Vector3 movement = _velocity * deltaTime;
            float distance = movement.magnitude;
            if (distance > 0f && Physics.Raycast(CurrentPosition, movement.normalized, out RaycastHit hit, distance))
            {
                CurrentPosition = hit.point;
                _hasCollided = true;
            }
            else
            {
                CurrentPosition += movement;
            }

            _lastSimulatedTimeSeconds = serverTimeSeconds;
        }

        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        }
    }
}
