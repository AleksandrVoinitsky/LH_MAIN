using System;
using System.Collections.Generic;
using LH.Main.Unity.Gameplay;

namespace LH.Main.Unity.Networking
{
    public sealed class ServerPlayerRegistry
    {
        private readonly Dictionary<int, AcceptedPlayer> _playersByConnectionId = new Dictionary<int, AcceptedPlayer>();
        private readonly Dictionary<Guid, int> _connectionIdsByPlayerId = new Dictionary<Guid, int>();
        private readonly Dictionary<Guid, RegisteredPlayer> _playersByPlayerId = new Dictionary<Guid, RegisteredPlayer>();
        private readonly HashSet<int> _spawnedConnectionIds = new HashSet<int>();
        private readonly CoreMatchRuntime _coreMatchRuntime;

        public ServerPlayerRegistry()
        {
        }

        public ServerPlayerRegistry(CoreMatchRuntime coreMatchRuntime)
        {
            _coreMatchRuntime = coreMatchRuntime;
        }

        public int ActivePlayerCount => _playersByConnectionId.Count;
        public int SpawnedPlayerCount => _spawnedConnectionIds.Count;

        public bool RegisterAcceptedConnection(int connectionId, Guid matchId, Guid playerId)
        {
            if (connectionId < 0 || matchId == Guid.Empty || playerId == Guid.Empty)
                return false;

            if (_playersByConnectionId.ContainsKey(connectionId) || _connectionIdsByPlayerId.ContainsKey(playerId))
                return false;

            DateTime acceptedAtUtc = DateTime.UtcNow;
            if (!_playersByPlayerId.TryGetValue(playerId, out RegisteredPlayer registeredPlayer))
                registeredPlayer = new RegisteredPlayer(matchId, PlayerStateMachine.Create(playerId), acceptedAtUtc);
            else
                registeredPlayer = registeredPlayer.WithAcceptedAt(matchId, acceptedAtUtc);

            _playersByPlayerId[playerId] = registeredPlayer;
            _playersByConnectionId.Add(connectionId, new AcceptedPlayer(connectionId, matchId, playerId, acceptedAtUtc));
            _connectionIdsByPlayerId.Add(playerId, connectionId);
            _coreMatchRuntime?.RegisterPlayer(playerId, registeredPlayer.StateMachine);
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

        public bool TryGetPlayerState(Guid playerId, out PlayerStateMachine stateMachine)
        {
            if (_playersByPlayerId.TryGetValue(playerId, out RegisteredPlayer registeredPlayer))
            {
                stateMachine = registeredPlayer.StateMachine;
                return true;
            }

            stateMachine = null;
            return false;
        }

        public PlayerStateChange ApplyTechnicalDamage(TechnicalDamageEvent damage)
        {
            if (!_playersByPlayerId.TryGetValue(damage.TargetPlayerId, out RegisteredPlayer registeredPlayer))
            {
                GameServerMetrics.RecordDamageRejected("target_not_registered");
                return PlayerStateChange.Reject("target_not_registered", PlayerLifeState.Disconnected);
            }

            PlayerStateChange change = registeredPlayer.StateMachine.ApplyDamage(damage);
            if (change.Accepted)
                GameServerMetrics.RecordDamageAccepted();
            else
                GameServerMetrics.RecordDamageRejected(change.Reason);

            return change;
        }

        public IReadOnlyList<PlayerResultSnapshot> SnapshotResults(DateTime utcNow)
        {
            var results = new List<PlayerResultSnapshot>(_playersByPlayerId.Count);
            int extractedPlayers = 0;
            int deadPlayers = 0;
            foreach (RegisteredPlayer registeredPlayer in _playersByPlayerId.Values)
            {
                PlayerStateMachine stateMachine = registeredPlayer.StateMachine;
                PlayerLifeState lifeState = stateMachine.LifeState;
                if (!lifeState.IsTerminal() && !_connectionIdsByPlayerId.ContainsKey(stateMachine.PlayerId))
                    lifeState = PlayerLifeState.Disconnected;

                if (lifeState == PlayerLifeState.Extracted)
                    extractedPlayers++;
                else if (lifeState == PlayerLifeState.Dead)
                    deadPlayers++;

                int survivalSeconds = Math.Max(0, (int)(utcNow - registeredPlayer.AcceptedAtUtc).TotalSeconds);
                results.Add(new PlayerResultSnapshot(stateMachine.PlayerId, lifeState, survivalSeconds, 0, stateMachine.DamageTaken));
            }

            GameServerMetrics.SetTerminalPlayerCounts(extractedPlayers, deadPlayers);
            return results;
        }

        public bool TryGetMatchId(out Guid matchId)
        {
            foreach (RegisteredPlayer registeredPlayer in _playersByPlayerId.Values)
            {
                matchId = registeredPlayer.MatchId;
                return true;
            }

            matchId = Guid.Empty;
            return false;
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

        private readonly struct RegisteredPlayer
        {
            public Guid MatchId { get; }
            public PlayerStateMachine StateMachine { get; }
            public DateTime AcceptedAtUtc { get; }

            public RegisteredPlayer(Guid matchId, PlayerStateMachine stateMachine, DateTime acceptedAtUtc)
            {
                MatchId = matchId;
                StateMachine = stateMachine;
                AcceptedAtUtc = acceptedAtUtc;
            }

            public RegisteredPlayer WithAcceptedAt(Guid matchId, DateTime acceptedAtUtc)
            {
                return new RegisteredPlayer(matchId, StateMachine, acceptedAtUtc);
            }
        }
    }
}
