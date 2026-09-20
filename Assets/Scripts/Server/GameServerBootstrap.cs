using System;
using System.Reflection;
using FishNet.Authenticating;
using FishNet.Managing;
using FishNet.Transporting;
using FishNet.Transporting.Tugboat;
using LH.Main.Unity.Gameplay;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace LH.Main.Unity.Server
{
    public sealed class GameServerBootstrap : MonoBehaviour
    {
        [SerializeField] private NetworkManager? _networkManager;
        [SerializeField] private Tugboat? _transport;

        private readonly GameServerHealthServer _healthServer = new GameServerHealthServer();
        private GameServerConfig? _config;
        private object? _ticketValidator;
        private object? _admissionAuthenticator;
        private object? _playerRegistry;
        private MatchResultSubmitter? _matchResultSubmitter;
        private Type? _metricsType;
        private GameServerState _state = GameServerState.Failed;
        private DateTime _startedAtUtc;
        private bool _subscribedToServerState;
        private bool _subscribedToPostTick;
        private long _lastPostTickTimestamp;

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

            try
            {
                Type ticketValidatorType = RequireNetworkingType("LH.Main.Unity.Networking.MatchTicketValidator");
                Type admissionAuthenticatorType = RequireNetworkingType("LH.Main.Unity.Networking.GameServerAuthenticator");
                Type playerRegistryType = RequireNetworkingType("LH.Main.Unity.Networking.ServerPlayerRegistry");
                _metricsType = RequireNetworkingType("LH.Main.Unity.Networking.GameServerMetrics");

                _ticketValidator = Activator.CreateInstance(ticketValidatorType, _config);
                _admissionAuthenticator = Activator.CreateInstance(admissionAuthenticatorType, _config.ServerId, _ticketValidator);
                _playerRegistry = Activator.CreateInstance(playerRegistryType);
                _matchResultSubmitter = new MatchResultSubmitter(
                    _config.BackendBaseUrl,
                    _config.SharedKey,
                    TimeSpan.FromSeconds(_config.TicketValidationTimeoutSeconds),
                    new HttpClientMatchResultSender(),
                    Debug.LogWarning);
            }
            catch (Exception ex)
            {
                _state = GameServerState.Failed;
                Debug.LogError($"Game server admission dependencies failed to initialize: {ex.GetType().Name}");
                Application.Quit(1);
                return;
            }
        }

        private void Start()
        {
            GameServerConfig? config = _config;
            if (config == null || !config.Validate(out _))
                return;

            if (_networkManager == null)
                _networkManager = FindFirstObjectByType<NetworkManager>();

            if (_transport == null)
                _transport = FindFirstObjectByType<Tugboat>();

            if (_networkManager == null || _networkManager.ServerManager == null || _transport == null)
            {
                _state = GameServerState.Failed;
                Debug.LogError("Initialized FishNet NetworkManager and Tugboat transport are required in the server scene.");
                Application.Quit(1);
                return;
            }

            if (_admissionAuthenticator == null || _playerRegistry == null)
            {
                _state = GameServerState.Failed;
                Debug.LogError("Game server admission dependencies are required before server startup.");
                Application.Quit(1);
                return;
            }

            try
            {
                Type fishNetAuthenticatorType = RequireNetworkingType("LH.Main.Unity.Networking.GameServerFishNetAuthenticator");
                if (!typeof(Authenticator).IsAssignableFrom(fishNetAuthenticatorType))
                    throw new InvalidOperationException("GameServerFishNetAuthenticator must derive from FishNet Authenticator.");

                Component fishNetAuthenticatorComponent = _networkManager.GetComponent(fishNetAuthenticatorType);
                if (fishNetAuthenticatorComponent == null)
                    fishNetAuthenticatorComponent = _networkManager.gameObject.AddComponent(fishNetAuthenticatorType);

                if (!TryConfigureAdmissionAuthenticator(fishNetAuthenticatorComponent, _admissionAuthenticator, _playerRegistry, out string configureError))
                    throw new InvalidOperationException(configureError);

                _networkManager.ServerManager.SetAuthenticator((Authenticator)fishNetAuthenticatorComponent);
            }
            catch (Exception ex)
            {
                _state = GameServerState.Failed;
                Debug.LogError($"Game server admission dependencies failed to initialize: {ex.GetType().Name}");
                Application.Quit(1);
                return;
            }

            _transport.SetPort(config.NetworkPort);
            _networkManager.ServerManager.OnServerConnectionState += OnServerConnectionState;
            _subscribedToServerState = true;
            if (_networkManager.TimeManager != null)
            {
                _networkManager.TimeManager.OnPostTick += OnPostTick;
                _subscribedToPostTick = true;
            }

            try
            {
                _healthServer.Start(config, CreateStatus, IsReady);
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

            Debug.Log($"Game server {config.ServerId} starting on network port {config.NetworkPort} and health port {config.HttpPort}.");
        }

        private void OnDestroy()
        {
            if (_subscribedToServerState && _networkManager != null && _networkManager.ServerManager != null)
                _networkManager.ServerManager.OnServerConnectionState -= OnServerConnectionState;

            if (_subscribedToPostTick && _networkManager != null && _networkManager.TimeManager != null)
                _networkManager.TimeManager.OnPostTick -= OnPostTick;

            _subscribedToServerState = false;
            _subscribedToPostTick = false;
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
            GameServerConfig config = _config ?? new GameServerConfig("unknown", 0, 0, "unknown", 0, string.Empty, string.Empty, 0);
            object? metricsSnapshot = GetMetricsSnapshot();
            return new GameServerStatus(
                config.ServerId,
                _state,
                config.NetworkPort,
                config.PublicHost,
                config.PublicNetworkPort,
                _startedAtUtc,
                ReadIntMetric(metricsSnapshot, "ActiveConnections"),
                ReadIntMetric(metricsSnapshot, "SpawnedPlayers"),
                ReadLongMetric(metricsSnapshot, "AcceptedAdmissions"),
                ReadLongMetric(metricsSnapshot, "RejectedAdmissions"),
                ReadLongMetric(metricsSnapshot, "InvalidInputCommands"),
                ReadLongMetric(metricsSnapshot, "Disconnects"),
                ReadIntMetric(metricsSnapshot, "ServerTickRate"),
                ReadDoubleMetric(metricsSnapshot, "ServerTickP95Ms"),
                ReadLongMetric(metricsSnapshot, "ProcessMemoryMb"),
                ReadDoubleMetric(metricsSnapshot, "InboundKbps"),
                ReadDoubleMetric(metricsSnapshot, "OutboundKbps"),
                ReadLongMetric(metricsSnapshot, "AcceptedDamageEvents"),
                ReadLongMetric(metricsSnapshot, "RejectedDamageEvents"),
                ReadLongMetric(metricsSnapshot, "ExtractedPlayers"),
                ReadLongMetric(metricsSnapshot, "DeadPlayers"),
                ReadLongMetric(metricsSnapshot, "SubmittedMatchResults"),
                ReadLongMetric(metricsSnapshot, "DuplicateMatchResults"),
                ReadLongMetric(metricsSnapshot, "FailedMatchResults"));
        }

        public void FinalizeMatchForLoadRunner()
        {
            _ = FinalizeMatchForLoadRunnerAsync();
        }

        private async System.Threading.Tasks.Task FinalizeMatchForLoadRunnerAsync()
        {
            try
            {
                GameServerConfig config = _config;
                MatchResultSubmitter submitter = _matchResultSubmitter;
                if (config == null || submitter == null || _playerRegistry == null)
                    return;

                MethodInfo matchIdMethod = _playerRegistry.GetType().GetMethod("TryGetMatchId", BindingFlags.Public | BindingFlags.Instance);
                object[] matchIdArguments = { Guid.Empty };
                if (matchIdMethod == null || !(bool)matchIdMethod.Invoke(_playerRegistry, matchIdArguments))
                    return;

                var matchId = (Guid)matchIdArguments[0];
                MethodInfo snapshotMethod = _playerRegistry.GetType().GetMethod("SnapshotResults", BindingFlags.Public | BindingFlags.Instance);
                var players = snapshotMethod?.Invoke(_playerRegistry, new object[] { DateTime.UtcNow }) as System.Collections.Generic.IReadOnlyList<PlayerResultSnapshot>;
                if (players == null)
                    return;

                MatchResultPayload payload = MatchResultBuilder.Build(matchId, config.ServerId, players, DateTime.UtcNow);
                MatchResultSubmissionOutcome outcome = await submitter.SubmitAsync(payload, System.Threading.CancellationToken.None);
                if (outcome.Accepted)
                    InvokeMetrics("RecordMatchResultSubmitted", outcome.Duplicate);
                else
                    InvokeMetrics("RecordMatchResultFailed");
            }
            catch (Exception ex)
            {
                InvokeMetrics("RecordMatchResultFailed");
                Debug.LogWarning($"Load-runner match finalization failed: {ex.GetType().Name}");
            }
        }

        private void OnPostTick()
        {
            long timestamp = Stopwatch.GetTimestamp();
            long previousTimestamp = _lastPostTickTimestamp;
            _lastPostTickTimestamp = timestamp;

            if (previousTimestamp == 0)
                return;

            double elapsedMs = (timestamp - previousTimestamp) * 1000d / Stopwatch.Frequency;
            InvokeMetrics("RecordServerTickSample", elapsedMs);
        }

        private object? GetMetricsSnapshot()
        {
            if (_metricsType == null)
                return null;

            if (_networkManager != null && _networkManager.TimeManager != null)
                InvokeMetrics("SetServerTickRate", (int)_networkManager.TimeManager.TickRate);

            MethodInfo? method = _metricsType.GetMethod("GetSnapshot", BindingFlags.Public | BindingFlags.Static);
            return method?.Invoke(null, null);
        }

        private void InvokeMetrics(string methodName, params object[] arguments)
        {
            MethodInfo? method = _metricsType?.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
            method?.Invoke(null, arguments);
        }

        private static int ReadIntMetric(object? snapshot, string propertyName)
        {
            object? value = ReadMetric(snapshot, propertyName);
            return value is int typedValue ? typedValue : 0;
        }

        private static long ReadLongMetric(object? snapshot, string propertyName)
        {
            object? value = ReadMetric(snapshot, propertyName);
            return value is long typedValue ? typedValue : 0L;
        }

        private static double ReadDoubleMetric(object? snapshot, string propertyName)
        {
            object? value = ReadMetric(snapshot, propertyName);
            return value is double typedValue ? typedValue : 0d;
        }

        private static object? ReadMetric(object? snapshot, string propertyName)
        {
            return snapshot?.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)?.GetValue(snapshot);
        }

        private static Type RequireNetworkingType(string typeName)
        {
            Type type = Type.GetType(typeName + ", LH.Main.Unity.Networking");
            if (type == null)
                throw new InvalidOperationException($"Required networking type was not found: {typeName}");

            return type;
        }

        private static bool TryConfigureAdmissionAuthenticator(Component component, object admissionAuthenticator, object playerRegistry, out string error)
        {
            error = string.Empty;

            MethodInfo configureMethod = component.GetType().GetMethod(
                "Configure",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { admissionAuthenticator.GetType(), playerRegistry.GetType() },
                null);

            if (configureMethod == null)
            {
                error = "GameServerFishNetAuthenticator Configure method was not found for admission dependencies.";
                return false;
            }

            try
            {
                configureMethod.Invoke(component, new[] { admissionAuthenticator, playerRegistry });
                return true;
            }
            catch (Exception ex) when (ex is TargetInvocationException || ex is ArgumentException || ex is MethodAccessException)
            {
                error = $"GameServerFishNetAuthenticator Configure failed: {ex.GetType().Name}";
                return false;
            }
        }
    }
}
