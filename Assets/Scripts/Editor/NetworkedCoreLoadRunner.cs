using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FishNet.Managing;
using LH.Main.Unity.Client;
using LH.Main.Unity.Load;
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
                for (int i = 0; i < assignments.Length; i++)
                {
                    NetworkManager clientNetworkManager = CreateClientNetworkManager(networkManager, i);
                    var bot = new HeadlessMatchBot();
                    botTasks.Add(bot.RunAsync(
                        new BotScenario(assignments[i].Assignment, assignments[i].PlayerId, options.DurationSeconds, clientNetworkManager),
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
                    if (result.Connected && result.Spawned && result.Moved && result.DisconnectedCleanly)
                        report.CompletedClients++;
                    else
                    {
                        report.FailedClients++;
                        report.DisconnectReasons.Add(string.IsNullOrWhiteSpace(result.FailureReason) ? "bot_failed" : result.FailureReason);
                    }
                }

                exitCode = report.FailedClients == 0 && report.CompletedClients == options.Clients ? 0 : 1;
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
            public int Clients { get; private set; } = 1;
            public int DurationSeconds { get; private set; } = 30;
            public string BackendUrl { get; private set; } = Environment.GetEnvironmentVariable("LH_LOAD_BACKEND_URL") ?? DefaultBackendUrl;
            public string ReportPath { get; private set; } = Environment.GetEnvironmentVariable("LH_LOAD_REPORT_PATH") ?? DefaultReportPath;

            public static LoadRunnerOptions FromCommandLine(string[] args)
            {
                var options = new LoadRunnerOptions();
                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i] == "--clients" && i + 1 < args.Length && int.TryParse(args[i + 1], out int clients))
                        options.Clients = Math.Max(1, clients);
                    if (args[i] == "--durationSeconds" && i + 1 < args.Length && int.TryParse(args[i + 1], out int durationSeconds))
                        options.DurationSeconds = Math.Max(1, durationSeconds);
                    if (args[i] == "--backendUrl" && i + 1 < args.Length)
                        options.BackendUrl = args[i + 1];
                    if (args[i] == "--reportPath" && i + 1 < args.Length)
                        options.ReportPath = args[i + 1];
                }

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
