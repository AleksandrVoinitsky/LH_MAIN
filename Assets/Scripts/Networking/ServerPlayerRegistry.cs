using System;
using System.Collections.Generic;

namespace LH.Main.Unity.Networking
{
    public sealed class ServerPlayerRegistry
    {
        private readonly Dictionary<int, AcceptedPlayer> _playersByConnectionId = new Dictionary<int, AcceptedPlayer>();
        private readonly Dictionary<Guid, int> _connectionIdsByPlayerId = new Dictionary<Guid, int>();
        private readonly HashSet<int> _spawnedConnectionIds = new HashSet<int>();

        public int ActivePlayerCount => _playersByConnectionId.Count;
        public int SpawnedPlayerCount => _spawnedConnectionIds.Count;

        public bool RegisterAcceptedConnection(int connectionId, Guid matchId, Guid playerId)
        {
            if (connectionId < 0 || matchId == Guid.Empty || playerId == Guid.Empty)
                return false;

            if (_playersByConnectionId.ContainsKey(connectionId) || _connectionIdsByPlayerId.ContainsKey(playerId))
                return false;

            _playersByConnectionId.Add(connectionId, new AcceptedPlayer(connectionId, matchId, playerId, DateTime.UtcNow));
            _connectionIdsByPlayerId.Add(playerId, connectionId);
            GameServerMetrics.SetActiveConnectionCount(ActivePlayerCount);
            return true;
        }

        public bool MarkConnectionSpawned(int connectionId, bool spawnSucceeded)
        {
            if (!spawnSucceeded)
                return false;

            if (!_playersByConnectionId.ContainsKey(connectionId))
                return false;

            if (!_spawnedConnectionIds.Add(connectionId))
                return false;

            GameServerMetrics.SetSpawnedPlayerCount(SpawnedPlayerCount);
            return true;
        }

        public bool RemoveConnection(int connectionId)
        {
            if (!_playersByConnectionId.TryGetValue(connectionId, out AcceptedPlayer acceptedPlayer))
                return false;

            _playersByConnectionId.Remove(connectionId);
            _connectionIdsByPlayerId.Remove(acceptedPlayer.PlayerId);
            _spawnedConnectionIds.Remove(connectionId);
            GameServerMetrics.SetConnectionCounts(ActivePlayerCount, SpawnedPlayerCount);
            return true;
        }

        public bool TryGetAcceptedPlayer(int connectionId, out AcceptedPlayer acceptedPlayer)
        {
            return _playersByConnectionId.TryGetValue(connectionId, out acceptedPlayer);
        }

        public readonly struct AcceptedPlayer
        {
            public int ConnectionId { get; }
            public Guid MatchId { get; }
            public Guid PlayerId { get; }
            public DateTime AcceptedAtUtc { get; }

            public AcceptedPlayer(int connectionId, Guid matchId, Guid playerId, DateTime acceptedAtUtc)
            {
                ConnectionId = connectionId;
                MatchId = matchId;
                PlayerId = playerId;
                AcceptedAtUtc = acceptedAtUtc;
            }
        }
    }
}
