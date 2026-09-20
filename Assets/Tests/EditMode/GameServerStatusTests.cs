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
    public void ToJsonIncludesGameplayLoopMetrics()
    {
        var status = new GameServerStatus("game-server-1", "idle", 7771, "localhost", 7771, DateTime.UtcNow)
        {
            AcceptedDamageEvents = 2,
            RejectedDamageEvents = 1,
            ExtractedPlayers = 3,
            DeadPlayers = 4,
            SubmittedMatchResults = 1,
            DuplicateMatchResults = 1,
            FailedMatchResults = 0,
            AcceptedPickupAttempts = 5,
            RejectedPickupAttempts = 6,
            DuplicateLootPickups = 7,
            AcceptedFireRequests = 8,
            RejectedFireRequests = 9,
            GrenadesExploded = 10,
            ZoneDamageTicks = 11,
            MedItemsUsed = 12
        };

        string json = status.ToJson();

        Assert.That(json, Does.Contain("\"acceptedDamageEvents\":2"));
        Assert.That(json, Does.Contain("\"rejectedDamageEvents\":1"));
        Assert.That(json, Does.Contain("\"extractedPlayers\":3"));
        Assert.That(json, Does.Contain("\"deadPlayers\":4"));
        Assert.That(json, Does.Contain("\"submittedMatchResults\":1"));
        Assert.That(json, Does.Contain("\"duplicateMatchResults\":1"));
        Assert.That(json, Does.Contain("\"failedMatchResults\":0"));
        Assert.That(json, Does.Contain("\"acceptedPickupAttempts\":5"));
        Assert.That(json, Does.Contain("\"rejectedPickupAttempts\":6"));
        Assert.That(json, Does.Contain("\"duplicateLootPickups\":7"));
        Assert.That(json, Does.Contain("\"acceptedFireRequests\":8"));
        Assert.That(json, Does.Contain("\"rejectedFireRequests\":9"));
        Assert.That(json, Does.Contain("\"grenadesExploded\":10"));
        Assert.That(json, Does.Contain("\"zoneDamageTicks\":11"));
        Assert.That(json, Does.Contain("\"medItemsUsed\":12"));
    }

    [Test]
    public void SnapshotIncludesRecordedPhase06Metrics()
    {
        GameServerMetrics.Snapshot before = GameServerMetrics.GetSnapshot();

        GameServerMetrics.RecordPickupAccepted();
        GameServerMetrics.RecordPickupRejected("loot_not_found");
        GameServerMetrics.RecordDuplicateLootPickup();
        GameServerMetrics.RecordInventoryFullRejection();
        GameServerMetrics.RecordFireAccepted(hit: true);
        GameServerMetrics.RecordFireAccepted(hit: false);
        GameServerMetrics.RecordFireRejected("fire_rejected");
        GameServerMetrics.RecordGrenadeThrown();
        GameServerMetrics.RecordGrenadeExploded();
        GameServerMetrics.RecordZoneDamageTick();
        GameServerMetrics.RecordMedItemUsed();
        GameServerMetrics.RecordMedItemRejected("item_not_owned");

        GameServerMetrics.Snapshot after = GameServerMetrics.GetSnapshot();

        Assert.That(after.AcceptedPickupAttempts, Is.EqualTo(before.AcceptedPickupAttempts + 1));
        Assert.That(after.RejectedPickupAttempts, Is.EqualTo(before.RejectedPickupAttempts + 2));
        Assert.That(after.DuplicateLootPickups, Is.EqualTo(before.DuplicateLootPickups + 1));
        Assert.That(after.InventoryFullRejections, Is.EqualTo(before.InventoryFullRejections + 1));
        Assert.That(after.AcceptedFireRequests, Is.EqualTo(before.AcceptedFireRequests + 2));
        Assert.That(after.RejectedFireRequests, Is.EqualTo(before.RejectedFireRequests + 1));
        Assert.That(after.HitscanHits, Is.EqualTo(before.HitscanHits + 1));
        Assert.That(after.HitscanMisses, Is.EqualTo(before.HitscanMisses + 1));
        Assert.That(after.GrenadesThrown, Is.EqualTo(before.GrenadesThrown + 1));
        Assert.That(after.GrenadesExploded, Is.EqualTo(before.GrenadesExploded + 1));
        Assert.That(after.ZoneDamageTicks, Is.EqualTo(before.ZoneDamageTicks + 1));
        Assert.That(after.MedItemsUsed, Is.EqualTo(before.MedItemsUsed + 1));
        Assert.That(after.MedItemsRejected, Is.EqualTo(before.MedItemsRejected + 1));
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
