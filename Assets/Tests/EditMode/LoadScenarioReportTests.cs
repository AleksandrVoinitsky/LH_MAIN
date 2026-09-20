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
}
