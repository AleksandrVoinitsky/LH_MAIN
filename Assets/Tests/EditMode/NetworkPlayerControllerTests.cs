using LH.Main.Unity.Gameplay;
using LH.Main.Unity.Gameplay.Items;
using LH.Main.Unity.Networking;
using FishNet.Object;
using NUnit.Framework;
using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

public sealed class NetworkPlayerControllerTests
{
    private static void SetLifeStateForTest(NetworkPlayerController controller, PlayerLifeState lifeState)
    {
        typeof(NetworkPlayerController)
            .GetProperty(nameof(NetworkPlayerController.LifeState))
            ?.SetValue(controller, lifeState);
    }

    [Test]
    public void LifeStateCannotBeSetDirectlyThroughPublicApi()
    {
        var setter = typeof(NetworkPlayerController).GetProperty(nameof(NetworkPlayerController.LifeState))?.SetMethod;

        Assert.That(setter, Is.Not.Null);
        Assert.That(setter.IsPublic, Is.False);
    }

    [Test]
    public void LifeStateChangesUseServerMethod()
    {
        var method = typeof(NetworkPlayerController).GetMethod(nameof(NetworkPlayerController.ApplyServerLifeState));

        Assert.That(method, Is.Not.Null);
        Assert.That(method.GetCustomAttributes(typeof(ServerAttribute), true), Is.Not.Empty);
    }

    [Test]
    public void CoreIntentMethodsUseServerRpc()
    {
        AssertServerRpc(nameof(NetworkPlayerController.ServerPickupLoot), typeof(Guid), typeof(Guid));
        AssertServerRpc(nameof(NetworkPlayerController.ServerUseMed), typeof(string), typeof(Guid));
        AssertServerRpc(nameof(NetworkPlayerController.ServerFireWeapon), typeof(Guid), typeof(Vector3), typeof(Vector3));
        AssertServerRpc(nameof(NetworkPlayerController.ServerReloadWeapon), typeof(Guid));
        AssertServerRpc(nameof(NetworkPlayerController.ServerThrowGrenade), typeof(Guid), typeof(Vector3), typeof(Vector3));
    }

    [Test]
    public void CoreRuntimeTestHelpersHaveServerAttributeAndExactReturnTypes()
    {
        AssertServerHelper(
            nameof(NetworkPlayerController.ApplyServerPickupForTest),
            typeof(InventoryTransactionResult),
            typeof(CoreMatchRuntime),
            typeof(Guid),
            typeof(Guid));
        AssertServerHelper(
            nameof(NetworkPlayerController.ApplyServerUseMedForTest),
            typeof(InventoryTransactionResult),
            typeof(CoreMatchRuntime),
            typeof(string),
            typeof(Guid));
        AssertServerHelper(
            nameof(NetworkPlayerController.ApplyServerFireForTest),
            typeof(WeaponFireResult),
            typeof(CoreMatchRuntime),
            typeof(Guid),
            typeof(Vector3),
            typeof(Vector3));
    }

    [Test]
    public void CoreRuntimeConfigureForTestIsServerOnly()
    {
        AssertServerHelper(
            nameof(NetworkPlayerController.ConfigureCoreMatchRuntimeForTest),
            typeof(void),
            typeof(CoreMatchRuntime),
            typeof(Guid));
    }

    [Test]
    public void ApplyAuthoritativeInputDoesNotMoveTerminalPlayer()
    {
        var gameObject = new GameObject("network-player-controller-test");

        try
        {
            var controller = gameObject.AddComponent<NetworkPlayerController>();
            SetLifeStateForTest(controller, PlayerLifeState.Dead);
            var command = new MovementCommand(1, 1f, 0f, 1d);
            long invalidBefore = GameServerMetrics.GetSnapshot().InvalidInputCommands;

            controller.ApplyAuthoritativeInput(command);

            long invalidAfter = GameServerMetrics.GetSnapshot().InvalidInputCommands;

            Assert.That(controller.AuthoritativeState.PositionX, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(invalidAfter, Is.EqualTo(invalidBefore + 1));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    [Test]
    public void RuntimeHelpersRejectTerminalPlayersBeforeMutatingInventoryOrCombat()
    {
        var gameObject = new GameObject("network-player-controller-runtime-test");

        try
        {
            NetworkObject networkObject = gameObject.AddComponent<NetworkObject>();
            var controller = gameObject.AddComponent<NetworkPlayerController>();
            InitializeServerNetworkObjectForTest(controller, networkObject);
            var runtime = new CoreMatchRuntime();
            Guid playerId = Guid.NewGuid();
            runtime.RegisterPlayer(playerId);
            controller.ConfigureCoreMatchRuntimeForTest(runtime, playerId);
            controller.ApplyServerLifeState(PlayerLifeState.Dead);

            InventoryTransactionResult pickup = controller.ApplyServerPickupForTest(runtime, Guid.NewGuid(), Guid.NewGuid());
            InventoryTransactionResult med = controller.ApplyServerUseMedForTest(runtime, "med_basic", Guid.NewGuid());
            WeaponFireResult fire = controller.ApplyServerFireForTest(runtime, Guid.NewGuid(), Vector3.zero, Vector3.forward);

            Assert.That(pickup.Accepted, Is.False);
            Assert.That(pickup.Reason, Is.EqualTo("state_terminal"));
            Assert.That(med.Accepted, Is.False);
            Assert.That(med.Reason, Is.EqualTo("state_terminal"));
            Assert.That(fire.Accepted, Is.False);
            Assert.That(fire.Reason, Is.EqualTo("state_terminal"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    [Test]
    public void ApplyServerFireReturnsHitscanHitWhenRuntimeFireHitsTarget()
    {
        var gameObject = new GameObject("network-player-controller-fire-metric-test");
        GameObject target = null;

        try
        {
            NetworkObject networkObject = gameObject.AddComponent<NetworkObject>();
            var controller = gameObject.AddComponent<NetworkPlayerController>();
            InitializeServerNetworkObjectForTest(controller, networkObject);
            var runtime = new CoreMatchRuntime();
            Guid sourcePlayerId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
            Guid targetPlayerId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
            runtime.RegisterPlayer(sourcePlayerId);
            runtime.RegisterPlayer(targetPlayerId);
            controller.ConfigureCoreMatchRuntimeForTest(runtime, sourcePlayerId);
            target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            target.transform.position = new Vector3(0f, 0f, 5f);
            target.AddComponent<BodyZoneHitbox>().ConfigureForTest(targetPlayerId, BodyZone.Head);
            Physics.SyncTransforms();
            WeaponFireResult fire = controller.ApplyServerFireForTest(runtime, Guid.NewGuid(), Vector3.zero, Vector3.forward);

            Assert.That(fire.Accepted, Is.True);
            Assert.That(fire.Hit, Is.True);
        }
        finally
        {
            if (target != null)
                UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    private static void AssertServerRpc(string methodName, params Type[] parameterTypes)
    {
        MethodInfo method = typeof(NetworkPlayerController).GetMethod(methodName, parameterTypes);

        Assert.That(method, Is.Not.Null, methodName);
        Assert.That(method.GetCustomAttributes(typeof(ServerRpcAttribute), true), Is.Not.Empty, methodName);
    }

    private static void AssertServerHelper(string methodName, Type returnType, params Type[] parameterTypes)
    {
        MethodInfo method = typeof(NetworkPlayerController).GetMethod(methodName, parameterTypes);

        Assert.That(method, Is.Not.Null, methodName);
        Assert.That(method.ReturnType, Is.EqualTo(returnType), methodName);
        Assert.That(method.GetCustomAttributes(typeof(ServerAttribute), true), Is.Not.Empty, methodName);
        Assert.That(method.GetParameters().Select(parameter => parameter.ParameterType).ToArray(), Is.EqualTo(parameterTypes), methodName);
    }

    private static void InitializeServerNetworkObjectForTest(NetworkPlayerController controller, NetworkObject networkObject)
    {
        typeof(NetworkBehaviour)
            .GetField("_networkObjectCache", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.SetValue(controller, networkObject);
        typeof(NetworkObject)
            .GetField("<IsServerInitialized>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.SetValue(networkObject, true);
    }
}
