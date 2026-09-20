using System;
using LH.Main.Unity.Networking;
using LH.Main.Unity.Server;
using NUnit.Framework;

public sealed class GameServerStatusTests
{
    [Test]
    public void ToJsonIncludesHealthFieldsAndMetrics()
    {
        var status = new GameServerStatus(
            "server-1",
            GameServerState.Idle,
            7770,
            "localhost",
            7771,
            new DateTime(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc),
            activeConnections: 3,
            spawnedPlayers: 2,
            acceptedAdmissions: 11,
            rejectedAdmissions: 5,
            invalidInputCommands: 7,
            disconnects: 13,
            serverTickRate: 60,
            serverTickP95Ms: 2.5,
            processMemoryMb: 128,
            inboundKbps: 1.25,
            outboundKbps: 2.5);

        string json = status.ToJson();

        Assert.That(json, Does.Contain("\"serverId\":\"server-1\""));
        Assert.That(json, Does.Contain("\"state\":\"idle\""));
        Assert.That(json, Does.Contain("\"networkPort\":7770"));
        Assert.That(json, Does.Contain("\"publicHost\":\"localhost\""));
        Assert.That(json, Does.Contain("\"publicNetworkPort\":7771"));
        Assert.That(json, Does.Contain("\"startedAtUtc\":\"2026-09-20T12:00:00.0000000Z\""));
        Assert.That(json, Does.Contain("\"activeConnections\":3"));
        Assert.That(json, Does.Contain("\"spawnedPlayers\":2"));
        Assert.That(json, Does.Contain("\"acceptedAdmissions\":11"));
        Assert.That(json, Does.Contain("\"rejectedAdmissions\":5"));
        Assert.That(json, Does.Contain("\"invalidInputCommands\":7"));
        Assert.That(json, Does.Contain("\"disconnects\":13"));
        Assert.That(json, Does.Contain("\"serverTickRate\":60"));
        Assert.That(json, Does.Contain("\"serverTickP95Ms\":2.5"));
        Assert.That(json, Does.Contain("\"processMemoryMb\":128"));
        Assert.That(json, Does.Contain("\"inboundKbps\":1.25"));
        Assert.That(json, Does.Contain("\"outboundKbps\":2.5"));
    }

    [Test]
    public void RegistryAcceptedConnectionsDoNotCountAsSpawnedPlayers()
    {
        GameServerMetrics.SetConnectionCounts(0, 0);
        var registry = new ServerPlayerRegistry();

        bool accepted = registry.RegisterAcceptedConnection(42, Guid.NewGuid(), Guid.NewGuid());
        GameServerMetrics.Snapshot acceptedSnapshot = GameServerMetrics.GetSnapshot();

        Assert.That(accepted, Is.True);
        Assert.That(acceptedSnapshot.ActiveConnections, Is.EqualTo(1));
        Assert.That(acceptedSnapshot.SpawnedPlayers, Is.EqualTo(0));
    }

    [Test]
    public void RegistryDoesNotCountNoOpSpawnAsSpawnedPlayer()
    {
        GameServerMetrics.SetConnectionCounts(0, 0);
        var registry = new ServerPlayerRegistry();
        registry.RegisterAcceptedConnection(42, Guid.NewGuid(), Guid.NewGuid());

        bool markedSpawned = registry.MarkConnectionSpawned(42, spawnSucceeded: false);
        GameServerMetrics.Snapshot snapshot = GameServerMetrics.GetSnapshot();

        Assert.That(markedSpawned, Is.False);
        Assert.That(snapshot.ActiveConnections, Is.EqualTo(1));
        Assert.That(snapshot.SpawnedPlayers, Is.EqualTo(0));
    }

    [Test]
    public void RegistryCountsSpawnedPlayerOnlyAfterExplicitSpawnSuccess()
    {
        GameServerMetrics.SetConnectionCounts(0, 0);
        var registry = new ServerPlayerRegistry();
        registry.RegisterAcceptedConnection(42, Guid.NewGuid(), Guid.NewGuid());

        bool markedSpawned = registry.MarkConnectionSpawned(42, spawnSucceeded: true);
        GameServerMetrics.Snapshot snapshot = GameServerMetrics.GetSnapshot();

        Assert.That(markedSpawned, Is.True);
        Assert.That(snapshot.ActiveConnections, Is.EqualTo(1));
        Assert.That(snapshot.SpawnedPlayers, Is.EqualTo(1));
    }

    [Test]
    public void SnapshotIncludesRecordedTrafficKbps()
    {
        GameServerMetrics.SetTrafficKbps(12.5d, 34.75d);

        GameServerMetrics.Snapshot snapshot = GameServerMetrics.GetSnapshot();

        Assert.That(snapshot.InboundKbps, Is.EqualTo(12.5d));
        Assert.That(snapshot.OutboundKbps, Is.EqualTo(34.75d));
    }
}
