using System;
using FishNet.Managing;
using FishNet.Transporting;
using FishNet.Transporting.Tugboat;
using UnityEngine;

namespace LH.Main.Unity.Server
{
    public sealed class GameServerBootstrap : MonoBehaviour
    {
        [SerializeField] private NetworkManager? _networkManager;
        [SerializeField] private Tugboat? _transport;

        private readonly GameServerHealthServer _healthServer = new GameServerHealthServer();
        private GameServerConfig? _config;
        private GameServerState _state = GameServerState.Failed;
        private DateTime _startedAtUtc;

        private void Awake()
        {
            _startedAtUtc = DateTime.UtcNow;
            _config = GameServerConfig.ReadFromEnvironment();

            if (!_config.Validate(out string error))
            {
                _state = GameServerState.Failed;
                Debug.LogError($"Game server config invalid: {error}");
                Application.Quit(1);
                return;
            }

            if (_networkManager == null)
                _networkManager = FindFirstObjectByType<NetworkManager>();

            if (_transport == null)
                _transport = FindFirstObjectByType<Tugboat>();

            if (_networkManager == null || _transport == null)
            {
                _state = GameServerState.Failed;
                Debug.LogError("FishNet NetworkManager and Tugboat transport are required in the server scene.");
                Application.Quit(1);
                return;
            }

            _transport.SetPort(_config.NetworkPort);
            _networkManager.ServerManager.OnServerConnectionState += OnServerConnectionState;

            try
            {
                _healthServer.Start(_config, CreateStatus, IsReady);
            }
            catch (Exception ex)
            {
                _state = GameServerState.Failed;
                Debug.LogError($"Health listener failed to start: {ex}");
                Application.Quit(1);
                return;
            }

            bool started = _networkManager.ServerManager.StartConnection();
            if (!started)
            {
                _state = GameServerState.Failed;
                Debug.LogError("FishNet server failed to start.");
                Application.Quit(1);
                return;
            }

            Debug.Log($"Game server {_config.ServerId} starting on network port {_config.NetworkPort} and health port {_config.HttpPort}.");
        }

        private void OnDestroy()
        {
            if (_networkManager != null)
                _networkManager.ServerManager.OnServerConnectionState -= OnServerConnectionState;

            _healthServer.Stop();
        }

        private void OnApplicationQuit()
        {
            _healthServer.Stop();
        }

        private void OnServerConnectionState(ServerConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Started)
            {
                _state = GameServerState.Idle;
                Debug.Log($"Game server {_config?.ServerId} is idle.");
                return;
            }

            if (args.ConnectionState == LocalConnectionState.Stopped)
            {
                _state = GameServerState.Failed;
                Debug.LogWarning($"Game server {_config?.ServerId} stopped.");
            }
        }

        private bool IsReady()
        {
            return _state == GameServerState.Idle;
        }

        private GameServerStatus CreateStatus()
        {
            GameServerConfig config = _config ?? new GameServerConfig("unknown", 0, 0, "unknown", 0);
            return new GameServerStatus(
                config.ServerId,
                _state,
                config.NetworkPort,
                config.PublicHost,
                config.PublicNetworkPort,
                _startedAtUtc);
        }
    }
}
