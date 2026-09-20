using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FishNet.Managing;
using LH.Main.Unity.Client;
using LH.Main.Unity.Gameplay;
using LH.Main.Unity.Load;
using LH.Main.Unity.Server;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LH.Main.Unity.Editor
{
    public static class NetworkedCoreLoadRunner
    {
        private const string DefaultBackendUrl = "http://127.0.0.1:8080";
        private const string DefaultReportPath = ".superpowers/sdd/2026-09-20-phase-04-networked-core/task-6-load-report.json";
        private const string Phase05DefaultReportPath = ".superpowers/sdd/2026-09-20-phase-05-gameplay-loop/task-7-final-load-64.json";
        private const string DefaultSharedKey = "local-game-server-shared-key-change-before-deployment";
        private const string LoadRunnerScenePath = "Assets/Scenes/Server/ServerBootstrap.unity";
        private const string PendingPlayModeRunKey = "LH.Main.Unity.Editor.NetworkedCoreLoadRunner.PendingPlayModeRun";
        private const string Password = "correct-horse-battery-staple";

        [InitializeOnLoadMethod]
        private static void RegisterPlayModeHook()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        public static void Run()
        {
            if (!EditorApplication.isPlaying)
            {
                FindOrLoadNetworkManager();
                SessionState.SetBool(PendingPlayModeRunKey, true);
                EditorApplication.EnterPlaymode();
                return;
            }

            RunScenarioAsync(LoadRunnerOptions.FromCommandLine(Environment.GetCommandLineArgs()));
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(PendingPlayModeRunKey, false))
                return;

            SessionState.SetBool(PendingPlayModeRunKey, false);
            RunScenarioAsync(LoadRunnerOptions.FromCommandLine(Environment.GetCommandLineArgs()));
        }

        private static async void RunScenarioAsync(LoadRunnerOptions options)
        {
            LoadScenarioReport report = new LoadScenarioReport(DateTime.UtcNow, options.DurationSeconds, options.Clients);
            int exitCode = 1;

            try
            {
                NetworkManager networkManager = FindOrLoadNetworkManager();
                if (networkManager == null)
                {
                    report.RecordFailedClients(
                        "network_manager_required",
                        "FishNet NetworkManager was not found in the loaded Unity scene.");
                    report.MetricCollectionGaps.Add("Load smoke requires a client scene or prefab with NetworkManager and Tugboat configured.");
                    return;
                }

                using var cancellationSource = new CancellationTokenSource(TimeSpan.FromSeconds(options.DurationSeconds + 60));
                List<Task<DevAssignment>> assignmentTasks = new List<Task<DevAssignment>>();
                List<Task<BotResult>> botTasks = new List<Task<BotResult>>();

                for (int i = 0; i < options.Clients; i++)
                    assignmentTasks.Add(CreateAssignmentAsync(options.BackendUrl, i, cancellationSource.Token));

                DevAssignment[] assignments = await Task.WhenAll(assignmentTasks);
                GameServerBootstrap bootstrap = UnityEngine.Object.FindFirstObjectByType<GameServerBootstrap>();
                Action<Guid, int, string> technicalDamageSink = bootstrap == null ? null : bootstrap.ApplyTechnicalDamageForLoadRunner;
                for (int i = 0; i < assignments.Length; i++)
                {
                    NetworkManager clientNetworkManager = CreateClientNetworkManager(networkManager, i);
                    var bot = new HeadlessMatchBot();
                    botTasks.Add(bot.RunAsync(
                        new BotScenario(
                            assignments[i].Assignment,
                            assignments[i].PlayerId,
                            options.DurationSeconds,
                            clientNetworkManager,
                            i,
                            options.Phase05GameplayLoop,
                            technicalDamageSink),
                        cancellationSource.Token));
                }

                await TryCollectStatusAsync(options.BackendUrl, report, cancellationSource.Token);
                BotResult[] results = await Task.WhenAll(botTasks);
                await TryCollectStatusAsync(options.BackendUrl, report, cancellationSource.Token);

                foreach (BotResult result in results)
                {
                    if (result.Connected)
                        report.ConnectedClients++;
                    if (result.Spawned)
                        report.SpawnedClients++;
                    if (result.Extracted)
                        report.ExtractedClients++;
                    if (result.Dead)
                        report.DeadClients++;
                    if (result.DisconnectedOutcome)
                        report.DisconnectedOutcomeClients++;

                    if (IsCompletedResult(result, options.Phase05GameplayLoop))
                        report.CompletedClients++;
                    else
                    {
                        report.FailedClients++;
                        report.DisconnectReasons.Add(string.IsNullOrWhiteSpace(result.FailureReason) ? "bot_failed" : result.FailureReason);
                    }
                }

                if (options.Phase05GameplayLoop)
                {
                    if (InvokePhase05Finalization(report, bootstrap == null ? null : bootstrap.FinalizeMatchForLoadRunner))
                        await SubmitPhase05ResultAsync(options, assignments, results, report, cancellationSource.Token);
                }

                exitCode = ShouldExitSuccessfully(report, options.Clients, options.Phase05GameplayLoop) ? 0 : 1;
            }
            catch (Exception ex)
            {
                report.FailedClients = Math.Max(report.FailedClients, options.Clients - report.CompletedClients);
                report.MachineNotes.Add(FormatExceptionNote(ex));
            }
            finally
            {
                report.WriteJson(options.ReportPath);
                UnityEngine.Debug.Log($"Networked core load report written to {options.ReportPath}");
                EditorApplication.Exit(exitCode);
            }
        }

        private static NetworkManager FindOrLoadNetworkManager()
        {
            NetworkManager networkManager = UnityEngine.Object.FindFirstObjectByType<NetworkManager>();
            if (networkManager != null)
            {
                ConfigureForLoadRunner(networkManager);
                return networkManager;
            }

            EditorSceneManager.OpenScene(LoadRunnerScenePath, OpenSceneMode.Single);
            DisableServerBootstrapRoot();
            networkManager = UnityEngine.Object.FindFirstObjectByType<NetworkManager>();
            if (networkManager != null)
                ConfigureForLoadRunner(networkManager);

            return networkManager;
        }

        private static void ConfigureForLoadRunner(NetworkManager networkManager)
        {
            var serializedObject = new SerializedObject(networkManager);
            SerializedProperty persistence = serializedObject.FindProperty("_persistence");
            if (persistence != null)
            {
                persistence.enumValueIndex = (int)NetworkManager.PersistenceType.AllowMultiple;
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void DisableServerBootstrapRoot()
        {
            Scene scene = SceneManager.GetActiveScene();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == "ServerBootstrap")
                {
                    root.SetActive(false);
                    return;
                }
            }
        }

        private static async Task<DevAssignment> CreateAssignmentAsync(string backendUrl, int index, CancellationToken cancellationToken)
        {
            using var httpClient = new HttpClient { BaseAddress = new Uri(backendUrl.TrimEnd('/') + "/") };
            string username = $"load_bot_{DateTime.UtcNow:yyyyMMddHHmmss}_{index}_{Guid.NewGuid():N}";
            string registerJson = "{\"username\":\"" + EscapeJson(username) + "\",\"password\":\"" + Password + "\"}";
            using var content = new StringContent(registerJson, Encoding.UTF8, "application/json");
            using HttpResponseMessage registerResponse = await httpClient.PostAsync("v1/auth/dev-register", content, cancellationToken);
            registerResponse.EnsureSuccessStatusCode();

            string loginJson = "{\"username\":\"" + EscapeJson(username) + "\",\"password\":\"" + Password + "\"}";
            using var loginContent = new StringContent(loginJson, Encoding.UTF8, "application/json");
            using HttpResponseMessage loginResponse = await httpClient.PostAsync("v1/auth/dev-login", loginContent, cancellationToken);
            loginResponse.EnsureSuccessStatusCode();
            LoginResponse login = JsonUtility.FromJson<LoginResponse>(await loginResponse.Content.ReadAsStringAsync());

            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.accessToken);
            using HttpResponseMessage queueResponse = await httpClient.PostAsync("v1/matchmaking/queue", null, cancellationToken);
            queueResponse.EnsureSuccessStatusCode();
            MatchmakingResponse queue = JsonUtility.FromJson<MatchmakingResponse>(await queueResponse.Content.ReadAsStringAsync());

            using HttpResponseMessage profileResponse = await httpClient.GetAsync("v1/profile", cancellationToken);
            profileResponse.EnsureSuccessStatusCode();
            ProfileResponse profile = JsonUtility.FromJson<ProfileResponse>(await profileResponse.Content.ReadAsStringAsync());

            if (queue.assignment == null)
                throw new InvalidOperationException("Backend did not return a match assignment.");

            return new DevAssignment(
                Guid.Parse(profile.userId),
                new MatchAssignment(
                    Guid.Parse(queue.assignment.matchId),
                    queue.assignment.serverId,
                    queue.assignment.publicHost,
                    queue.assignment.publicPort,
                    queue.assignment.ticket));
        }

        private static NetworkManager CreateClientNetworkManager(NetworkManager template, int index)
        {
            if (index == 0)
                return template;

            GameObject clientObject = UnityEngine.Object.Instantiate(template.gameObject);
            clientObject.name = template.gameObject.name + " Load Client " + index;
            return clientObject.GetComponent<NetworkManager>();
        }

        private static async Task TryCollectStatusAsync(string backendUrl, LoadScenarioReport report, CancellationToken cancellationToken)
        {
            try
            {
                using var httpClient = new HttpClient { BaseAddress = new Uri(backendUrl.TrimEnd('/') + "/") };
                using HttpResponseMessage response = await httpClient.GetAsync("health/ready", cancellationToken);
                string body = await response.Content.ReadAsStringAsync();
                report.ServerStatusSnapshots.Add("{"
                    + $"\"capturedAtUtc\":\"{DateTime.UtcNow:O}\","
                    + "\"endpoint\":\"health/ready\","
                    + $"\"statusCode\":{(int)response.StatusCode},"
                    + $"\"body\":\"{EscapeJson(body)}\""
                    + "}");
            }
            catch (Exception ex)
            {
                report.MetricCollectionGaps.Add("Backend status collection failed: " + ex.GetType().Name);
            }
        }

        private static bool IsCompletedResult(BotResult result, bool phase05GameplayLoop)
        {
            if (!phase05GameplayLoop)
                return result.Connected && result.Spawned && result.Moved && result.DisconnectedCleanly;

            return result.Connected
                && result.Spawned
                && result.Moved
                && result.DisconnectedCleanly
                && (result.Extracted || result.Dead || result.DisconnectedOutcome);
        }

        private static bool InvokePhase05Finalization(LoadScenarioReport report, Action finalizationHook)
        {
            if (finalizationHook == null)
            {
                report?.MachineNotes.Add("Phase 05 finalization hook unavailable.");
                return false;
            }

            try
            {
                finalizationHook();
                return true;
            }
            catch (Exception ex)
            {
                report?.MachineNotes.Add("Phase 05 finalization hook failed: " + ex.GetType().Name);
                return false;
            }
        }

        private static bool ShouldExitSuccessfully(LoadScenarioReport report, int targetClients, bool phase05GameplayLoop)
        {
            bool clientsSucceeded = report.FailedClients == 0 && report.CompletedClients == targetClients;
            if (!phase05GameplayLoop)
                return clientsSucceeded;

            bool resultSucceeded = report.ResultSubmitted && report.DuplicateResultAccepted;
            if (!resultSucceeded && report.FailedClients == 0)
            {
                report.FailedClients = Math.Max(1, targetClients - report.CompletedClients);
                report.DisconnectReasons.Add("phase05_result_verification_failed");
            }

            return clientsSucceeded && resultSucceeded;
        }

        private static async Task SubmitPhase05ResultAsync(LoadRunnerOptions options, DevAssignment[] assignments, BotResult[] results, LoadScenarioReport report, CancellationToken cancellationToken)
        {
            if (assignments.Length == 0)
                return;

            MatchResultPayload payload = BuildPhase05ResultPayload(assignments, results);
            var submitter = new MatchResultSubmitter(options.BackendUrl, options.SharedKey, TimeSpan.FromSeconds(10));
            MatchResultSubmissionOutcome first = await submitter.SubmitAsync(payload, cancellationToken);
            ApplySubmissionOutcome(report, first);

            if (!first.Accepted)
                return;

            MatchResultSubmissionOutcome duplicate = await submitter.SubmitAsync(payload, cancellationToken);
            ApplySubmissionOutcome(report, duplicate);
        }

        private static MatchResultPayload BuildPhase05ResultPayload(DevAssignment[] assignments, BotResult[] results)
        {
            var participants = new List<MatchParticipantResult>(assignments.Length);
            int count = Math.Min(assignments.Length, results.Length);
            for (int i = 0; i < count; i++)
            {
                PlayerLifeState lifeState = ToLifeState(results[i]);
                string rewardCode = lifeState == PlayerLifeState.Extracted ? MatchResultBuilder.ExtractedRewardCode : string.Empty;
                participants.Add(new MatchParticipantResult(assignments[i].PlayerId, ToOutcome(lifeState), 0, 0, results[i].Dead ? 200 : 0, rewardCode));
            }

            MatchAssignment assignment = assignments[0].Assignment;
            return new MatchResultPayload(Guid.NewGuid(), assignment.MatchId, assignment.ServerId, DateTime.UtcNow, participants);
        }

        private static PlayerLifeState ToLifeState(BotResult result)
        {
            if (result.Extracted)
                return PlayerLifeState.Extracted;
            if (result.Dead)
                return PlayerLifeState.Dead;
            return PlayerLifeState.Disconnected;
        }

        private static string ToOutcome(PlayerLifeState lifeState)
        {
            switch (lifeState)
            {
                case PlayerLifeState.Extracted:
                    return "extracted";
                case PlayerLifeState.Dead:
                    return "dead";
                default:
                    return "disconnected";
            }
        }

        private static void ApplySubmissionOutcome(LoadScenarioReport report, MatchResultSubmissionOutcome outcome)
        {
            if (report == null || !outcome.Accepted)
                return;

            ApplySubmissionOutcome(report, true, outcome.NewRewardTransactions, outcome.Duplicate);
        }

        private static void ApplySubmissionOutcome(LoadScenarioReport report, bool accepted, int newRewardTransactions, bool duplicate)
        {
            if (report == null || !accepted)
                return;

            if (duplicate)
                report.DuplicateResultAccepted = newRewardTransactions == 0;
            else
            {
                report.ResultSubmitted = true;
                report.RewardTransactions = newRewardTransactions;
            }
        }

        private static string EscapeJson(string value)
        {
            return (value ?? string.Empty).Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("\"", "\\\"", StringComparison.Ordinal);
        }

        private static string FormatExceptionNote(Exception ex)
        {
            return "Runner failed: " + ex.GetType().Name + ": " + ex.Message;
        }

        private readonly struct DevAssignment
        {
            public Guid PlayerId { get; }
            public MatchAssignment Assignment { get; }

            public DevAssignment(Guid playerId, MatchAssignment assignment)
            {
                PlayerId = playerId;
                Assignment = assignment;
            }
        }

        private sealed class LoadRunnerOptions
        {
            private readonly bool _environmentReportPathSet;

            public LoadRunnerOptions()
            {
                string reportPath = Environment.GetEnvironmentVariable("LH_LOAD_REPORT_PATH");
                _environmentReportPathSet = !string.IsNullOrWhiteSpace(reportPath);
                ReportPath = _environmentReportPathSet ? reportPath : DefaultReportPath;
            }

            public int Clients { get; private set; } = 1;
            public int DurationSeconds { get; private set; } = 30;
            public string BackendUrl { get; private set; } = Environment.GetEnvironmentVariable("LH_LOAD_BACKEND_URL") ?? DefaultBackendUrl;
            public string SharedKey { get; private set; } = Environment.GetEnvironmentVariable("GAME_SERVER_SHARED_KEY") ?? DefaultSharedKey;
            public string ReportPath { get; private set; }
            public bool Phase05GameplayLoop { get; private set; }

            public static LoadRunnerOptions FromCommandLine(string[] args)
            {
                var options = new LoadRunnerOptions();
                bool reportPathFromCommandLine = false;
                for (int i = 0; i < args.Length; i++)
                {
                    if ((args[i] == "--clients" || args[i] == "-lhClients") && i + 1 < args.Length && int.TryParse(args[i + 1], out int clients))
                        options.Clients = Math.Max(1, clients);
                    if ((args[i] == "--durationSeconds" || args[i] == "-lhDurationSeconds") && i + 1 < args.Length && int.TryParse(args[i + 1], out int durationSeconds))
                        options.DurationSeconds = Math.Max(1, durationSeconds);
                    if ((args[i] == "--backendUrl" || args[i] == "-lhBackendUrl") && i + 1 < args.Length)
                        options.BackendUrl = args[i + 1];
                    if ((args[i] == "--sharedKey" || args[i] == "-lhSharedKey") && i + 1 < args.Length)
                        options.SharedKey = args[i + 1];
                    if ((args[i] == "--reportPath" || args[i] == "-lhReportPath") && i + 1 < args.Length)
                    {
                        options.ReportPath = args[i + 1];
                        reportPathFromCommandLine = true;
                    }
                    if (args[i] == "-lhPhase05GameplayLoop" && i + 1 < args.Length && bool.TryParse(args[i + 1], out bool phase05GameplayLoop))
                        options.Phase05GameplayLoop = phase05GameplayLoop;
                }

                if (options.Phase05GameplayLoop && !options._environmentReportPathSet && !reportPathFromCommandLine)
                    options.ReportPath = Phase05DefaultReportPath;

                return options;
            }
        }

        [Serializable]
        private sealed class LoginResponse
        {
            public string accessToken;
        }

        [Serializable]
        private sealed class ProfileResponse
        {
            public string userId;
        }

        [Serializable]
        private sealed class MatchmakingResponse
        {
            public AssignmentResponse assignment;
        }

        [Serializable]
        private sealed class AssignmentResponse
        {
            public string matchId;
            public string serverId;
            public string publicHost;
            public int publicPort;
            public string ticket;
        }
    }
}
