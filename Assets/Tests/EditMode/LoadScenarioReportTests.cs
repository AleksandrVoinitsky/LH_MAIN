using System;
using LH.Main.Unity.Load;
using NUnit.Framework;

public sealed class LoadScenarioReportTests
{
    [Test]
    public void ToJsonIncludesRequiredLoadFieldsAndEscapesNotes()
    {
        var report = new LoadScenarioReport(
            startedAtUtc: new DateTime(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc),
            durationSeconds: 30,
            targetClients: 1,
            connectedClients: 1,
            spawnedClients: 1,
            completedClients: 1,
            failedClients: 0);
        report.ServerStatusSnapshots.Add("{\"state\":\"idle\"}");
        report.DisconnectReasons.Add("clean");
        report.MachineNotes.Add("note with \"quotes\"");
        report.MetricCollectionGaps.Add("status unavailable");

        string json = report.ToJson();

        Assert.That(json, Does.Contain("\"startedAtUtc\":\"2026-09-20T12:00:00.0000000Z\""));
        Assert.That(json, Does.Contain("\"durationSeconds\":30"));
        Assert.That(json, Does.Contain("\"targetClients\":1"));
        Assert.That(json, Does.Contain("\"connectedClients\":1"));
        Assert.That(json, Does.Contain("\"spawnedClients\":1"));
        Assert.That(json, Does.Contain("\"completedClients\":1"));
        Assert.That(json, Does.Contain("\"failedClients\":0"));
        Assert.That(json, Does.Contain("\"serverStatusSnapshots\":[{\"state\":\"idle\"}]"));
        Assert.That(json, Does.Contain("\"disconnectReasons\":[\"clean\"]"));
        Assert.That(json, Does.Contain("\"machineNotes\":[\"note with \\\"quotes\\\"\"]"));
        Assert.That(json, Does.Contain("\"metricCollectionGaps\":[\"status unavailable\"]"));
    }

    [Test]
    public void ToJsonIncludesGameplayLoopOutcomeFields()
    {
        var report = new LoadScenarioReport(DateTime.UtcNow, 30, 3)
        {
            ExtractedClients = 1,
            DeadClients = 1,
            DisconnectedOutcomeClients = 1,
            ResultSubmitted = true,
            DuplicateResultAccepted = true,
            RewardTransactions = 3
        };

        string json = report.ToJson();

        Assert.That(json, Does.Contain("\"extractedClients\":1"));
        Assert.That(json, Does.Contain("\"deadClients\":1"));
        Assert.That(json, Does.Contain("\"disconnectedOutcomeClients\":1"));
        Assert.That(json, Does.Contain("\"resultSubmitted\":true"));
        Assert.That(json, Does.Contain("\"duplicateResultAccepted\":true"));
        Assert.That(json, Does.Contain("\"rewardTransactions\":3"));
    }

    [Test]
    public void ToJsonIncludesPhase06Counters()
    {
        var report = new LoadScenarioReport(DateTime.UtcNow, 60, 64)
        {
            LootPickups = 64,
            DuplicateLootPrevented = 12,
            FireRequests = 64,
            GrenadesExploded = 64,
            ZoneDamageTicks = 10,
            MedItemsUsed = 32
        };

        string json = report.ToJson();

        Assert.That(json, Does.Contain("\"lootPickups\":64"));
        Assert.That(json, Does.Contain("\"duplicateLootPrevented\":12"));
        Assert.That(json, Does.Contain("\"fireRequests\":64"));
        Assert.That(json, Does.Contain("\"grenadesExploded\":64"));
        Assert.That(json, Does.Contain("\"zoneDamageTicks\":10"));
        Assert.That(json, Does.Contain("\"medItemsUsed\":32"));
    }
}
