using System;
using System.Threading;
using System.Threading.Tasks;
using FishNet.Managing;
using FishNet.Transporting;
using LH.Main.Unity.Networking;
using UnityEngine;

namespace LH.Main.Unity.Client
{
    public readonly struct MatchAssignment
    {
        public Guid MatchId { get; }
        public string ServerId { get; }
        public string PublicHost { get; }
        public ushort PublicPort { get; }
        public string Ticket { get; }

        public MatchAssignment(Guid matchId, string serverId, string publicHost, int publicPort, string ticket)
        {
            MatchId = matchId;
            ServerId = serverId ?? string.Empty;
            PublicHost = publicHost ?? string.Empty;
            PublicPort = publicPort > 0 && publicPort <= ushort.MaxValue ? (ushort)publicPort : (ushort)0;
            Ticket = ticket ?? string.Empty;
        }
    }

    public sealed class MatchConnectionClient
    {
        public const int AuthenticationTimeoutSeconds = 15;

        private readonly NetworkManager _networkManager;
        private TaskCompletionSource<bool> _connectionResult;
        private TaskCompletionSource<bool> _authenticationResult;
        private CancellationTokenRegistration _cancellationRegistration;

        public MatchConnectionClient(NetworkManager networkManager)
        {
            _networkManager = networkManager != null ? networkManager : throw new ArgumentNullException(nameof(networkManager));
        }

        public static MatchAdmissionPayload BuildAdmissionPayload(MatchAssignment assignment, Guid playerId)
        {
            return new MatchAdmissionPayload(assignment.MatchId, playerId, assignment.ServerId, assignment.Ticket);
        }

        public async Task<bool> ConnectAsync(MatchAssignment assignment, Guid playerId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(assignment.PublicHost) || assignment.PublicPort == 0)
                return false;

            if (_networkManager.ClientManager == null)
                return false;

            MatchAdmissionPayload payload = BuildAdmissionPayload(assignment, playerId);
            _connectionResult = new TaskCompletionSource<bool>();
            _authenticationResult = new TaskCompletionSource<bool>();
            _networkManager.ClientManager.OnClientConnectionState += OnClientConnectionState;
            _networkManager.ClientManager.RegisterBroadcast<GameServerAdmissionResultFishNetBroadcast>(OnAdmissionResultBroadcast);
            using CancellationTokenSource authenticationTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            authenticationTimeout.CancelAfter(TimeSpan.FromSeconds(AuthenticationTimeoutSeconds));
            _cancellationRegistration = authenticationTimeout.Token.Register(() =>
            {
                _connectionResult.TrySetCanceled();
                _authenticationResult.TrySetCanceled();
            });

            try
            {
                if (!_networkManager.ClientManager.StartConnection(assignment.PublicHost, assignment.PublicPort))
                    return false;

                bool connected = await _connectionResult.Task;
                if (!connected)
                    return false;

                _networkManager.ClientManager.Broadcast(new GameServerAdmissionFishNetBroadcast
                {
                    PayloadJson = payload.ToJson()
                });

                bool authenticated = await _authenticationResult.Task;
                if (!authenticated)
                    _networkManager.ClientManager.StopConnection();

                return authenticated;
            }
            catch (OperationCanceledException)
            {
                _networkManager.ClientManager.StopConnection();
                return false;
            }
            finally
            {
                _cancellationRegistration.Dispose();
                _networkManager.ClientManager.UnregisterBroadcast<GameServerAdmissionResultFishNetBroadcast>(OnAdmissionResultBroadcast);
                _networkManager.ClientManager.OnClientConnectionState -= OnClientConnectionState;
            }
        }

        public bool Disconnect()
        {
            return _networkManager.ClientManager != null && _networkManager.ClientManager.StopConnection();
        }

        private void OnClientConnectionState(ClientConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Started)
                _connectionResult.TrySetResult(true);
            else if (args.ConnectionState == LocalConnectionState.Stopped)
            {
                _connectionResult.TrySetResult(false);
                _authenticationResult.TrySetResult(false);
            }
        }

        private void OnAdmissionResultBroadcast(GameServerAdmissionResultFishNetBroadcast broadcast, Channel channel)
        {
            _authenticationResult.TrySetResult(broadcast.Accepted);
        }
    }
}
