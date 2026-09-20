using LH.Main.Unity.Gameplay;
using LH.Main.Unity.Networking;
using FishNet.Object;
using NUnit.Framework;
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
            Object.DestroyImmediate(gameObject);
        }
    }
}
