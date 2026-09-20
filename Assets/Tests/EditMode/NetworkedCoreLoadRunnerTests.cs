using System;
using System.Reflection;
using FishNet.Managing;
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
    public void InvokePhase05FinalizationCallsHookAndReportsUnavailableHook()
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
        Assert.That(missing, Is.False);
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
