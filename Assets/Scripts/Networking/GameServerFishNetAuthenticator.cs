using System;
using System.Threading;
using FishNet.Authenticating;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;

namespace LH.Main.Unity.Networking
{
    public sealed class GameServerFishNetAuthenticator : Authenticator
    {
        [SerializeField] private NetworkObject _playerPrefab;

        private GameServerAuthenticator _authenticator;
        private ServerPlayerRegistry _playerRegistry;
        private CancellationTokenSource _shutdownSource;

        public override event Action<NetworkConnection, bool> OnAuthenticationResult;

        public void Configure(GameServerAuthenticator authenticator, ServerPlayerRegistry playerRegistry)
        {
            _authenticator = authenticator ?? throw new ArgumentNullException(nameof(authenticator));
            _playerRegistry = playerRegistry ?? throw new ArgumentNullException(nameof(playerRegistry));
        }

        public override void InitializeOnce(NetworkManager networkManager)
        {
            base.InitializeOnce(networkManager);
            _shutdownSource = new CancellationTokenSource();
            NetworkManager.ServerManager.RegisterBroadcast<GameServerAdmissionFishNetBroadcast>(OnAdmissionBroadcast, false);
            NetworkManager.ServerManager.OnRemoteConnectionState += OnRemoteConnectionState;
        }

        private void OnDestroy()
        {
            if (NetworkManager != null && NetworkManager.ServerManager != null)
            {
                NetworkManager.ServerManager.UnregisterBroadcast<GameServerAdmissionFishNetBroadcast>(OnAdmissionBroadcast);
                NetworkManager.ServerManager.OnRemoteConnectionState -= OnRemoteConnectionState;
            }

            if (_shutdownSource != null)
            {
                _shutdownSource.Cancel();
                _shutdownSource.Dispose();
                _shutdownSource = null;
            }
        }

        private void OnRemoteConnectionState(NetworkConnection connection, RemoteConnectionStateArgs args)
        {
            if (args.ConnectionState != RemoteConnectionState.Stopped)
                return;

            GameServerMetrics.RecordDisconnect();
            if (_playerRegistry != null)
                _playerRegistry.RemoveConnection(connection.ClientId);
        }

        private async void OnAdmissionBroadcast(NetworkConnection connection, GameServerAdmissionFishNetBroadcast broadcast, Channel channel)
        {
            if (connection.IsAuthenticated)
            {
                GameServerMetrics.RecordAdmissionRejected("already_authenticated");
                connection.Disconnect(true);
                return;
            }

            bool accepted = false;
            MatchAdmissionResult result = MatchAdmissionResult.Rejected("admission_not_configured");

            if (_authenticator != null && _playerRegistry != null)
            {
                CancellationToken cancellationToken = _shutdownSource != null ? _shutdownSource.Token : CancellationToken.None;
                result = await _authenticator.TryAuthenticateAsync(broadcast.PayloadJson, cancellationToken);
                accepted = result.IsAccepted
                    && _playerRegistry.RegisterAcceptedConnection(connection.ClientId, result.MatchId, result.PlayerId);
            }

            if (!accepted)
            {
                GameServerMetrics.RecordAdmissionRejected(result.Reason);
                Debug.LogWarning($"Connection {connection.ClientId} admission rejected: {result.Reason}");
            }
            else
            {
                GameServerMetrics.RecordAdmissionAccepted();
                SpawnAcceptedPlayer(connection);
            }

            NetworkManager.ServerManager.Broadcast(connection, new GameServerAdmissionResultFishNetBroadcast
            {
                Accepted = accepted,
                Reason = result.Reason ?? string.Empty
            }, false);
            OnAuthenticationResult?.Invoke(connection, accepted);
        }

        private void SpawnAcceptedPlayer(NetworkConnection connection)
        {
            if (_playerPrefab == null || NetworkManager == null || NetworkManager.ServerManager == null)
                return;

            NetworkObject player = Instantiate(_playerPrefab);
            NetworkManager.ServerManager.Spawn(player, connection);
            _playerRegistry.MarkConnectionSpawned(connection.ClientId, player.IsSpawned);
        }
    }
}
