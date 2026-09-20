using LH.Main.Unity.Gameplay;
using LH.Main.Unity.Networking;
using NUnit.Framework;
using UnityEngine;

public sealed class NetworkPlayerControllerTests
{
    [Test]
    public void ApplyAuthoritativeInputDoesNotMoveTerminalPlayer()
    {
        var gameObject = new GameObject("network-player-controller-test");

        try
        {
            var controller = gameObject.AddComponent<NetworkPlayerController>();
            controller.LifeState = PlayerLifeState.Dead;
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
