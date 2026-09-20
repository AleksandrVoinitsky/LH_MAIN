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
}
