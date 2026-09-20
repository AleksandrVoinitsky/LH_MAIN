using System;
using UnityEngine;

namespace LH.Main.Unity.Gameplay
{
    public readonly struct GrenadeThrowRequest
    {
        public GrenadeThrowRequest(Guid grenadeId, Guid? sourcePlayerId, Vector3 spawnPosition, Vector3 throwDirection)
        {
            GrenadeId = grenadeId;
            SourcePlayerId = sourcePlayerId;
            SpawnPosition = spawnPosition;
            ThrowDirection = throwDirection;
        }

        public Guid GrenadeId { get; }
        public Guid? SourcePlayerId { get; }
        public Vector3 SpawnPosition { get; }
        public Vector3 ThrowDirection { get; }
    }
}
