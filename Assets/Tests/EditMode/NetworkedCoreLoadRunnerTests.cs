using System;
using System.Reflection;
using FishNet.Managing;
using LH.Main.Unity.Client;
using LH.Main.Unity.Gameplay;
using LH.Main.Unity.Load;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class NetworkedCoreLoadRunnerTests
{
    [Test]
    public void RecordFailedClientsMarksEveryTargetClientFailed()
    {
        var report = new LoadScenarioReport(DateTime.UtcNow, 30, 3);

        report.RecordFailedClients("network_manager_required", "FishNet NetworkManager was not found in the loaded Unity scene.");

        Assert.That(report.FailedClients, Is.EqualTo(3));
        Assert.That(report.DisconnectReasons, Does.Contain("network_manager_required"));
        Assert.That(report.MachineNotes, Does.Contain("FishNet NetworkManager was not found in the loaded Unity scene."));
    }

    [Test]
    public void LoadRunnerOpensConfiguredSceneWhenNetworkManagerIsMissing()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        Type runnerType = Type.GetType("LH.Main.Unity.Editor.NetworkedCoreLoadRunner, Assembly-CSharp-Editor");
        MethodInfo method = runnerType?.GetMethod("FindOrLoadNetworkManager", BindingFlags.Static | BindingFlags.NonPublic);

        Assert.That(method, Is.Not.Null);
        var networkManager = method.Invoke(null, Array.Empty<object>()) as NetworkManager;

        Assert.That(networkManager, Is.Not.Null);
        Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo("Assets/Scenes/Server/ServerBootstrap.unity"));
        Assert.That(GameObject.Find("ServerBootstrap"), Is.Null);
    }

    [Test]
    public void LoadRunnerExceptionNoteIncludesTypeAndMessage()
    {
        Type runnerType = Type.GetType("LH.Main.Unity.Editor.NetworkedCoreLoadRunner, Assembly-CSharp-Editor");
        MethodInfo method = runnerType?.GetMethod("FormatExceptionNote", BindingFlags.Static | BindingFlags.NonPublic);

        Assert.That(method, Is.Not.Null);

        string note = (string)method.Invoke(null, new object[] { new InvalidOperationException("example failure") });

        Assert.That(note, Does.Contain("InvalidOperationException"));
        Assert.That(note, Does.Contain("example failure"));
    }

    [Test]
    public void LoadRunnerConfiguresNetworkManagerToAllowMultipleInstances()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Server/ServerBootstrap.unity", OpenSceneMode.Single);
        NetworkManager networkManager = UnityEngine.Object.FindFirstObjectByType<NetworkManager>();
        Type runnerType = Type.GetType("LH.Main.Unity.Editor.NetworkedCoreLoadRunner, Assembly-CSharp-Editor");
        MethodInfo method = runnerType?.GetMethod("ConfigureForLoadRunner", BindingFlags.Static | BindingFlags.NonPublic);

        Assert.That(networkManager, Is.Not.Null);
        Assert.That(method, Is.Not.Null);

        method.Invoke(null, new object[] { networkManager });

        var serializedObject = new SerializedObject(networkManager);
        SerializedProperty persistence = serializedObject.FindProperty("_persistence");
        Assert.That(persistence.enumValueIndex, Is.EqualTo((int)NetworkManager.PersistenceType.AllowMultiple));
    }

    [Test]
    public void Phase05GameplayLoopOptionSwitchesDefaultReportPath()
    {
        string previousReportPath = Environment.GetEnvironmentVariable("LH_LOAD_REPORT_PATH");
        Environment.SetEnvironmentVariable("LH_LOAD_REPORT_PATH", null);
        try
        {
            object options = ParseOptions("-lhPhase05GameplayLoop", "true");

            Assert.That(ReadProperty<bool>(options, "Phase05GameplayLoop"), Is.True);
            Assert.That(ReadProperty<string>(options, "ReportPath"), Is.EqualTo(".superpowers/sdd/2026-09-20-phase-05-gameplay-loop/task-7-final-load-64.json"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("LH_LOAD_REPORT_PATH", previousReportPath);
        }
    }

    [Test]
    public void EnvironmentReportPathOverridesPhase05DefaultReportPath()
    {
        string previousReportPath = Environment.GetEnvironmentVariable("LH_LOAD_REPORT_PATH");
        Environment.SetEnvironmentVariable("LH_LOAD_REPORT_PATH", "custom/report.json");
        try
        {
            object options = ParseOptions("-lhPhase05GameplayLoop", "true");

            Assert.That(ReadProperty<string>(options, "ReportPath"), Is.EqualTo("custom/report.json"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("LH_LOAD_REPORT_PATH", previousReportPath);
        }
    }

    [Test]
    public void Phase06CoreMatchOptionSwitchesDefaultReportPath()
    {
        string previousReportPath = Environment.GetEnvironmentVariable("LH_LOAD_REPORT_PATH");
        Environment.SetEnvironmentVariable("LH_LOAD_REPORT_PATH", null);
        try
        {
            object options = ParseOptions("--phase06CoreMatch", "true");

            Assert.That(ReadProperty<bool>(options, "Phase06CoreMatch"), Is.True);
            Assert.That(ReadProperty<string>(options, "ReportPath"), Is.EqualTo(".superpowers/sdd/2026-09-21-phase-06-core-match/task-9-final-load-64.json"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("LH_LOAD_REPORT_PATH", previousReportPath);
        }
    }

    [Test]
    public void ApplySubmissionOutcomeRequiresDuplicateWithNoNewRewardTransactions()
    {
        var report = new LoadScenarioReport(DateTime.UtcNow, 30, 3);
        Type runnerType = Type.GetType("LH.Main.Unity.Editor.NetworkedCoreLoadRunner, Assembly-CSharp-Editor");
        MethodInfo method = runnerType?.GetMethod(
            "ApplySubmissionOutcome",
            BindingFlags.Static | BindingFlags.NonPublic,
            null,
            new[] { typeof(LoadScenarioReport), typeof(bool), typeof(int), typeof(bool) },
            null);

        Assert.That(method, Is.Not.Null);

        method.Invoke(null, new object[] { report, true, 3, false });
        method.Invoke(null, new object[] { report, true, 0, true });

        Assert.That(report.ResultSubmitted, Is.True);
        Assert.That(report.RewardTransactions, Is.EqualTo(3));
        Assert.That(report.DuplicateResultAccepted, Is.True);
    }

    [Test]
    public void InvokePhase05FinalizationAllowsSubmissionWhenHookIsUnavailable()
    {
        var report = new LoadScenarioReport(DateTime.UtcNow, 30, 1);
        int calls = 0;
        MethodInfo method = GetPrivateStaticMethod(
            "InvokePhase05Finalization",
            typeof(LoadScenarioReport),
            typeof(Action));

        Assert.That(method, Is.Not.Null);

        bool invoked = (bool)method.Invoke(null, new object[] { report, new Action(() => calls++) });
        bool missing = (bool)method.Invoke(null, new object[] { report, null });

        Assert.That(invoked, Is.True);
        Assert.That(calls, Is.EqualTo(1));
        Assert.That(missing, Is.True);
        Assert.That(report.MachineNotes, Does.Contain("Phase 05 finalization hook unavailable."));
    }

    [Test]
    public void Phase05RunSuccessRequiresSubmittedResultAndAcceptedDuplicate()
    {
        var report = new LoadScenarioReport(DateTime.UtcNow, 30, 1)
        {
            CompletedClients = 1,
            FailedClients = 0,
            ResultSubmitted = true,
            DuplicateResultAccepted = false
        };
        MethodInfo method = GetPrivateStaticMethod(
            "ShouldExitSuccessfully",
            typeof(LoadScenarioReport),
            typeof(int),
            typeof(bool));

        Assert.That(method, Is.Not.Null);

        bool success = (bool)method.Invoke(null, new object[] { report, 1, true });

        Assert.That(success, Is.False);
        Assert.That(report.FailedClients, Is.EqualTo(1));
        Assert.That(report.DisconnectReasons, Does.Contain("phase05_result_verification_failed"));
    }

    [Test]
    public void Phase06RunSuccessRequiresCompletedClientsAndCoverageWithoutDuplicateLootSuccess()
    {
        var report = new LoadScenarioReport(DateTime.UtcNow, 60, 64)
        {
            CompletedClients = 64,
            FailedClients = 0,
            LootPickups = 64,
            DuplicateLootPrevented = 1,
            FireRequests = 64,
            GrenadesExploded = 1,
            ZoneDamageTicks = 1,
            MedItemsUsed = 1,
            DuplicateLootSucceeded = false
        };
        MethodInfo method = GetPrivateStaticMethod(
            "ShouldExitSuccessfully",
            typeof(LoadScenarioReport),
            typeof(int),
            typeof(bool),
            typeof(bool));

        Assert.That(method, Is.Not.Null);

        bool success = (bool)method.Invoke(null, new object[] { report, 64, false, true });

        Assert.That(success, Is.True);
    }

    [Test]
    public void Phase06RunFailsWhenDuplicateLootSucceededOrCoverageIsMissing()
    {
        var report = new LoadScenarioReport(DateTime.UtcNow, 60, 64)
        {
            CompletedClients = 64,
            FailedClients = 0,
            LootPickups = 64,
            DuplicateLootPrevented = 1,
            FireRequests = 64,
            GrenadesExploded = 1,
            ZoneDamageTicks = 0,
            MedItemsUsed = 1,
            DuplicateLootSucceeded = true
        };
        MethodInfo method = GetPrivateStaticMethod(
            "ShouldExitSuccessfully",
            typeof(LoadScenarioReport),
            typeof(int),
            typeof(bool),
            typeof(bool));

        Assert.That(method, Is.Not.Null);

        bool success = (bool)method.Invoke(null, new object[] { report, 64, false, true });

        Assert.That(success, Is.False);
        Assert.That(report.FailedClients, Is.EqualTo(1));
        Assert.That(report.DisconnectReasons, Does.Contain("phase06_core_match_verification_failed"));
    }

    [Test]
    public void BuildPhase05ResultPayloadAssignsRewardCodeToEveryOutcome()
    {
        Type runnerType = Type.GetType("LH.Main.Unity.Editor.NetworkedCoreLoadRunner, Assembly-CSharp-Editor");
        Type devAssignmentType = runnerType?.GetNestedType("DevAssignment", BindingFlags.NonPublic);
        MethodInfo method = GetPrivateStaticMethod(
            "BuildPhase05ResultPayload",
            devAssignmentType?.MakeArrayType(),
            typeof(BotResult[]));

        Assert.That(devAssignmentType, Is.Not.Null);
        Assert.That(method, Is.Not.Null);

        Guid matchId = Guid.NewGuid();
        var assignment = new MatchAssignment(matchId, "game-server-1", "localhost", 7771, "ticket");
        Array assignments = Array.CreateInstance(devAssignmentType, 3);
        assignments.SetValue(Activator.CreateInstance(devAssignmentType, Guid.NewGuid(), assignment), 0);
        assignments.SetValue(Activator.CreateInstance(devAssignmentType, Guid.NewGuid(), assignment), 1);
        assignments.SetValue(Activator.CreateInstance(devAssignmentType, Guid.NewGuid(), assignment), 2);
        var results = new[]
        {
            new BotResult { Extracted = true },
            new BotResult { Dead = true },
            new BotResult { DisconnectedOutcome = true }
        };

        var payload = (MatchResultPayload)method.Invoke(null, new object[] { assignments, results });

        Assert.That(payload.Participants, Has.Count.EqualTo(3));
        foreach (MatchParticipantResult participant in payload.Participants)
            Assert.That(participant.RewardCode, Is.EqualTo(MatchResultBuilder.ExtractedRewardCode));
    }

    private static object ParseOptions(params string[] args)
    {
        Type runnerType = Type.GetType("LH.Main.Unity.Editor.NetworkedCoreLoadRunner, Assembly-CSharp-Editor");
        Type optionsType = runnerType?.GetNestedType("LoadRunnerOptions", BindingFlags.NonPublic);
        MethodInfo method = optionsType?.GetMethod("FromCommandLine", BindingFlags.Static | BindingFlags.Public);

        Assert.That(method, Is.Not.Null);
        return method.Invoke(null, new object[] { args });
    }

    private static T ReadProperty<T>(object target, string propertyName)
    {
        PropertyInfo property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        Assert.That(property, Is.Not.Null);
        return (T)property.GetValue(target);
    }

    private static MethodInfo GetPrivateStaticMethod(string name, params Type[] parameterTypes)
    {
        Type runnerType = Type.GetType("LH.Main.Unity.Editor.NetworkedCoreLoadRunner, Assembly-CSharp-Editor");
        return runnerType?.GetMethod(
            name,
            BindingFlags.Static | BindingFlags.NonPublic,
            null,
            parameterTypes,
            null);
    }
}
