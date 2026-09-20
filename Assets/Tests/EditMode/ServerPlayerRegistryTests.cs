using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
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
