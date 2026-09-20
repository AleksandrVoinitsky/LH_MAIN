using System;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using LH.Main.Unity.Gameplay;
using LH.Main.Unity.Networking;
using LH.Main.Unity.Server;
using NUnit.Framework;
using UnityEngine;

public sealed class ServerPlayerRegistryTests
{
    private static readonly Guid MatchId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid PlayerId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Test]
    public void RegisterAcceptedConnectionStoresOnePlayerPerConnection()
    {
        var registry = new ServerPlayerRegistry();

        bool registered = registry.RegisterAcceptedConnection(7, MatchId, PlayerId);

        Assert.That(registered, Is.True);
        Assert.That(registry.ActivePlayerCount, Is.EqualTo(1));
    }

    [Test]
    public void RegisterAcceptedConnectionRejectsDuplicateConnection()
    {
        var registry = new ServerPlayerRegistry();
        registry.RegisterAcceptedConnection(7, MatchId, PlayerId);

        bool registered = registry.RegisterAcceptedConnection(7, MatchId, Guid.Parse("33333333-3333-3333-3333-333333333333"));

        Assert.That(registered, Is.False);
        Assert.That(registry.ActivePlayerCount, Is.EqualTo(1));
    }

    [Test]
    public void RegisterAcceptedConnectionRejectsDuplicatePlayer()
    {
        var registry = new ServerPlayerRegistry();
        registry.RegisterAcceptedConnection(7, MatchId, PlayerId);

        bool registered = registry.RegisterAcceptedConnection(8, MatchId, PlayerId);

        Assert.That(registered, Is.False);
        Assert.That(registry.ActivePlayerCount, Is.EqualTo(1));
    }

    [Test]
    public void RegisterAcceptedConnectionRejectsEmptyPlayerId()
    {
        var registry = new ServerPlayerRegistry();

        bool registered = registry.RegisterAcceptedConnection(7, MatchId, Guid.Empty);

        Assert.That(registered, Is.False);
        Assert.That(registry.ActivePlayerCount, Is.EqualTo(0));
    }

    [Test]
    public void RemoveConnectionRemovesAcceptedPlayerState()
    {
        var registry = new ServerPlayerRegistry();
        registry.RegisterAcceptedConnection(7, MatchId, PlayerId);

        bool removed = registry.RemoveConnection(7);
        bool registeredAgain = registry.RegisterAcceptedConnection(8, MatchId, PlayerId);

        Assert.That(removed, Is.True);
        Assert.That(registeredAgain, Is.True);
        Assert.That(registry.ActivePlayerCount, Is.EqualTo(1));
    }

    [Test]
    public void RemoveConnectionReturnsFalseForUnknownConnection()
    {
        var registry = new ServerPlayerRegistry();

        bool removed = registry.RemoveConnection(7);

        Assert.That(removed, Is.False);
    }

    [Test]
    public void SnapshotResultsMarksRemovedNonTerminalPlayersDisconnected()
    {
        var registry = new ServerPlayerRegistry();
        registry.RegisterAcceptedConnection(7, MatchId, PlayerId);
        registry.RemoveConnection(7);

        var results = registry.SnapshotResults(DateTime.UtcNow);

        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results[0].PlayerId, Is.EqualTo(PlayerId));
        Assert.That(results[0].LifeState, Is.EqualTo(PlayerLifeState.Disconnected));
    }

    [Test]
    public void SnapshotResultsReturnsTerminalOutcomesForExtractedDeadAndRemovedPlayers()
    {
        Guid extractedPlayerId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        Guid deadPlayerId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        Guid removedPlayerId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        var registry = new ServerPlayerRegistry();
        registry.RegisterAcceptedConnection(7, MatchId, extractedPlayerId);
        registry.RegisterAcceptedConnection(8, MatchId, deadPlayerId);
        registry.RegisterAcceptedConnection(9, MatchId, removedPlayerId);
        registry.TryGetPlayerState(extractedPlayerId, out PlayerStateMachine extractedState);
        registry.TryGetPlayerState(deadPlayerId, out PlayerStateMachine deadState);
        extractedState.TryExtract();
        deadState.ApplyDamage(new TechnicalDamageEvent(Guid.Parse("66666666-6666-6666-6666-666666666666"), null, deadPlayerId, 130, "test"));
        registry.RemoveConnection(9);

        var results = registry.SnapshotResults(DateTime.UtcNow);

        Assert.That(results, Has.Count.EqualTo(3));
        Assert.That(results, Has.Some.Matches<PlayerResultSnapshot>(result => result.PlayerId == extractedPlayerId && result.LifeState == PlayerLifeState.Extracted));
        Assert.That(results, Has.Some.Matches<PlayerResultSnapshot>(result => result.PlayerId == deadPlayerId && result.LifeState == PlayerLifeState.Dead));
        Assert.That(results, Has.Some.Matches<PlayerResultSnapshot>(result => result.PlayerId == removedPlayerId && result.LifeState == PlayerLifeState.Disconnected));
    }

    [Test]
    public void ApplyTechnicalDamageRecordsAcceptedAndRejectedDamageMetrics()
    {
        Guid targetPlayerId = Guid.Parse("77777777-7777-7777-7777-777777777777");
        var registry = new ServerPlayerRegistry();
        registry.RegisterAcceptedConnection(7, MatchId, targetPlayerId);
        GameServerMetrics.Snapshot before = GameServerMetrics.GetSnapshot();

        PlayerStateChange accepted = registry.ApplyTechnicalDamage(
            new TechnicalDamageEvent(Guid.Parse("88888888-8888-8888-8888-888888888888"), null, targetPlayerId, 25, "technical"));
        PlayerStateChange rejected = registry.ApplyTechnicalDamage(
            new TechnicalDamageEvent(Guid.Parse("99999999-9999-9999-9999-999999999999"), null, Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), 25, "technical"));
        GameServerMetrics.Snapshot after = GameServerMetrics.GetSnapshot();

        Assert.That(accepted.Accepted, Is.True);
        Assert.That(rejected.Accepted, Is.False);
        Assert.That(after.AcceptedDamageEvents, Is.EqualTo(before.AcceptedDamageEvents + 1));
        Assert.That(after.RejectedDamageEvents, Is.EqualTo(before.RejectedDamageEvents + 1));
    }

    [Test]
    public void RegisterAcceptedConnectionRegistersPlayerWithConfiguredCoreRuntime()
    {
        var runtime = new CoreMatchRuntime();
        var registry = (ServerPlayerRegistry)Activator.CreateInstance(typeof(ServerPlayerRegistry), runtime);

        bool registered = registry.RegisterAcceptedConnection(7, MatchId, PlayerId);
        WeaponFireResult fire = runtime.TryFire(PlayerId, new WeaponFireRequest(Guid.NewGuid()), Vector3.zero, Vector3.forward, 10d);

        Assert.That(registered, Is.True);
        Assert.That(fire.Accepted, Is.True);
    }
}

public sealed class GameServerAuthenticatorTests
{
    private static readonly Guid MatchId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid PlayerId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Test]
    public async Task TryAuthenticateAsyncRejectsWrongLocalServerIdBeforeValidation()
    {
        bool validatorCalled = false;
        var authenticator = new GameServerAuthenticator(
            "local-server",
            (payload, cancellationToken) =>
            {
                validatorCalled = true;
                return Task.FromResult(MatchAdmissionResult.Accepted(payload.MatchId, payload.PlayerId));
            });

        MatchAdmissionResult result = await authenticator.TryAuthenticateAsync(CreatePayload("other-server").ToJson(), CancellationToken.None);

        Assert.That(result.IsAccepted, Is.False);
        Assert.That(result.Reason, Is.EqualTo("wrong_server"));
        Assert.That(validatorCalled, Is.False);
    }

    [Test]
    public async Task TryAuthenticateAsyncRejectsMalformedPayload()
    {
        bool validatorCalled = false;
        var authenticator = new GameServerAuthenticator(
            "local-server",
            (payload, cancellationToken) =>
            {
                validatorCalled = true;
                return Task.FromResult(MatchAdmissionResult.Accepted(payload.MatchId, payload.PlayerId));
            });

        MatchAdmissionResult result = await authenticator.TryAuthenticateAsync("not-json", CancellationToken.None);

        Assert.That(result.IsAccepted, Is.False);
        Assert.That(result.Reason, Is.EqualTo("payload_malformed"));
        Assert.That(validatorCalled, Is.False);
    }

    [Test]
    public async Task TryAuthenticateAsyncReturnsValidatorRejection()
    {
        var authenticator = new GameServerAuthenticator(
            "local-server",
            (payload, cancellationToken) => Task.FromResult(MatchAdmissionResult.Rejected("ticket_invalid")));

        MatchAdmissionResult result = await authenticator.TryAuthenticateAsync(CreatePayload("local-server").ToJson(), CancellationToken.None);

        Assert.That(result.IsAccepted, Is.False);
        Assert.That(result.Reason, Is.EqualTo("ticket_invalid"));
    }

    [Test]
    public async Task TryAuthenticateAsyncReturnsAcceptedMatchAndPlayerIds()
    {
        var authenticator = new GameServerAuthenticator(
            "local-server",
            (payload, cancellationToken) => Task.FromResult(MatchAdmissionResult.Accepted(payload.MatchId, payload.PlayerId)));

        MatchAdmissionResult result = await authenticator.TryAuthenticateAsync(CreatePayload("local-server").ToJson(), CancellationToken.None);

        Assert.That(result.IsAccepted, Is.True);
        Assert.That(result.MatchId, Is.EqualTo(MatchId));
        Assert.That(result.PlayerId, Is.EqualTo(PlayerId));
    }

    private static MatchAdmissionPayload CreatePayload(string serverId)
    {
        return new MatchAdmissionPayload(MatchId, PlayerId, serverId, "secret-ticket");
    }
}

public sealed class GameServerAdmissionTransportTests
{
    [Test]
    public void PublicAdmissionTransportDtoDoesNotImplementFishNetBroadcast()
    {
        Type dtoType = Type.GetType("LH.Main.Unity.Networking.GameServerAdmissionBroadcast, LH.Main.Unity.Networking");
        Type broadcastType = Type.GetType("FishNet.Broadcast.IBroadcast, FishNet.Runtime");

        Assert.That(broadcastType, Is.Not.Null);
        Assert.That(dtoType == null || !broadcastType.IsAssignableFrom(dtoType), Is.True);
    }
}

public sealed class GameServerBootstrapAdmissionReflectionTests
{
    [Test]
    public async Task FinalizeMatchForLoadRunnerSubmitsMixedRuntimeSnapshotWithSupportedRewards()
    {
        Guid matchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        Guid extractedPlayerId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        Guid deadPlayerId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        Guid disconnectedPlayerId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        var registry = new ServerPlayerRegistry();
        registry.RegisterAcceptedConnection(7, matchId, extractedPlayerId);
        registry.RegisterAcceptedConnection(8, matchId, deadPlayerId);
        registry.RegisterAcceptedConnection(9, matchId, disconnectedPlayerId);
        registry.TryGetPlayerState(extractedPlayerId, out PlayerStateMachine extractedState);
        registry.TryGetPlayerState(deadPlayerId, out PlayerStateMachine deadState);
        extractedState.TryExtract();
        deadState.ApplyDamage(new TechnicalDamageEvent(Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"), null, deadPlayerId, 130, "test"));
        registry.RemoveConnection(9);
        var sender = new BackendRuleRecordingSender();
        var submitter = new MatchResultSubmitter("http://backend:8080", "shared-key", TimeSpan.FromSeconds(3), sender);
        GameObject gameObject = new GameObject("bootstrap-finalize-test");
        gameObject.SetActive(false);

        try
        {
            var bootstrap = gameObject.AddComponent<GameServerBootstrap>();
            SetPrivateField(bootstrap, "_config", new GameServerConfig("game-server-1", 8081, 7771, "localhost", 7771, "http://backend:8080", "shared-key", 3));
            SetPrivateField(bootstrap, "_playerRegistry", registry);
            SetPrivateField(bootstrap, "_matchResultSubmitter", submitter);

            bootstrap.FinalizeMatchForLoadRunner();
            Task completed = await Task.WhenAny(sender.BodyReceived, Task.Delay(TimeSpan.FromSeconds(3)));

            Assert.That(completed, Is.SameAs(sender.BodyReceived));
            Assert.That(sender.AcceptedResponseIssued, Is.True);
            Assert.That(sender.SharedKeyHeader, Is.EqualTo("shared-key"));
            Assert.That(sender.Body, Does.Contain("\"outcome\":\"extracted\""));
            Assert.That(sender.Body, Does.Contain("\"outcome\":\"dead\""));
            Assert.That(sender.Body, Does.Contain("\"outcome\":\"disconnected\""));
            Assert.That(sender.RewardCodes.Length, Is.EqualTo(3));
            foreach (string rewardCode in sender.RewardCodes)
                Assert.That(rewardCode, Is.EqualTo(MatchResultBuilder.ExtractedRewardCode));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    [Test]
    public void ApplyTechnicalDamageForLoadRunnerRecordsMetricsThroughRegistry()
    {
        Guid matchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        Guid targetPlayerId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var registry = new ServerPlayerRegistry();
        registry.RegisterAcceptedConnection(7, matchId, targetPlayerId);
        GameObject gameObject = new GameObject("bootstrap-damage-test");
        gameObject.SetActive(false);

        try
        {
            var bootstrap = gameObject.AddComponent<GameServerBootstrap>();
            FieldInfo registryField = typeof(GameServerBootstrap).GetField("_playerRegistry", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(registryField, Is.Not.Null);
            registryField.SetValue(bootstrap, registry);
            GameServerMetrics.Snapshot before = GameServerMetrics.GetSnapshot();

            bootstrap.ApplyTechnicalDamageForLoadRunner(targetPlayerId, 25, "load_runner");
            bootstrap.ApplyTechnicalDamageForLoadRunner(Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"), 25, "load_runner");
            GameServerMetrics.Snapshot after = GameServerMetrics.GetSnapshot();

            Assert.That(after.AcceptedDamageEvents, Is.EqualTo(before.AcceptedDamageEvents + 1));
            Assert.That(after.RejectedDamageEvents, Is.EqualTo(before.RejectedDamageEvents + 1));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    [Test]
    public void ConfigureAdmissionAuthenticatorFailsWhenConfigureMethodIsMissing()
    {
        GameObject gameObject = new GameObject("bootstrap-reflection-test");
        try
        {
            Component component = gameObject.AddComponent<MissingConfigureComponent>();

            bool configured = InvokeTryConfigure(component, new object(), new object(), out string error);

            Assert.That(configured, Is.False);
            Assert.That(error, Does.Contain("Configure"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    [Test]
    public void ConfigureAdmissionAuthenticatorCatchesConfigureInvocationFailure()
    {
        GameObject gameObject = new GameObject("bootstrap-reflection-test");
        try
        {
            Component component = gameObject.AddComponent<ThrowingConfigureComponent>();

            bool configured = InvokeTryConfigure(component, new object(), new object(), out string error);

            Assert.That(configured, Is.False);
            Assert.That(error, Does.Contain("Configure"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    private static bool InvokeTryConfigure(Component component, object authenticator, object registry, out string error)
    {
        MethodInfo method = typeof(GameServerBootstrap).GetMethod(
            "TryConfigureAdmissionAuthenticator",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(method, Is.Not.Null);

        object[] arguments = { component, authenticator, registry, null };
        bool configured = (bool)method.Invoke(null, arguments);
        error = (string)arguments[3];
        return configured;
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = typeof(GameServerBootstrap).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(field, Is.Not.Null);
        field.SetValue(target, value);
    }

    private sealed class BackendRuleRecordingSender : IMatchResultSender
    {
        private readonly TaskCompletionSource<string> _bodyReceived = new TaskCompletionSource<string>();

        public Task<string> BodyReceived => _bodyReceived.Task;
        public string Body { get; private set; }
        public string SharedKeyHeader { get; private set; }
        public bool AcceptedResponseIssued { get; private set; }
        public string[] RewardCodes { get; private set; }

        public Task<MatchResultSubmissionResponse> SendAsync(Uri endpoint, string sharedKey, string body, CancellationToken cancellationToken)
        {
            Body = body;
            SharedKeyHeader = sharedKey;
            RewardCodes = ReadRewardCodes(body);
            AcceptedResponseIssued = RewardCodes.Length == 3 && AllRewardCodesSupported(RewardCodes);
            _bodyReceived.TrySetResult(body);

            string resultId = ReadJsonString(body, "resultId");
            string response = AcceptedResponseIssued
                ? "{\"accepted\":true,\"resultId\":\"" + resultId + "\",\"newRewardTransactions\":3,\"duplicate\":false}"
                : "{\"accepted\":false}";
            return Task.FromResult(new MatchResultSubmissionResponse(AcceptedResponseIssued ? 200 : 400, response));
        }

        private static string[] ReadRewardCodes(string body)
        {
            MatchCollection matches = Regex.Matches(body ?? string.Empty, "\\\"rewardCode\\\"\\s*:\\s*\\\"([^\\\"]*)\\\"");
            var rewardCodes = new string[matches.Count];
            for (int index = 0; index < matches.Count; index++)
                rewardCodes[index] = matches[index].Groups[1].Value;

            return rewardCodes;
        }

        private static bool AllRewardCodesSupported(string[] rewardCodes)
        {
            foreach (string rewardCode in rewardCodes)
            {
                if (rewardCode != MatchResultBuilder.ExtractedRewardCode)
                    return false;
            }

            return true;
        }

        private static string ReadJsonString(string body, string name)
        {
            Match match = Regex.Match(body ?? string.Empty, "\\\"" + Regex.Escape(name) + "\\\"\\s*:\\s*\\\"([^\\\"]*)\\\"");
            return match.Success ? match.Groups[1].Value : string.Empty;
        }
    }

    private sealed class MissingConfigureComponent : MonoBehaviour
    {
    }

    private sealed class ThrowingConfigureComponent : MonoBehaviour
    {
        public void Configure(object authenticator, object registry)
        {
            throw new InvalidOperationException("boom");
        }
    }
}
