using LH.Main.Unity.Networking;
using NUnit.Framework;
using UnityEditor;

public sealed class GameServerFishNetAuthenticatorAssetTests
{
    [Test]
    public void FishNetAuthenticatorHasDedicatedMonoScriptAsset()
    {
        var gameObject = new UnityEngine.GameObject();
        try
        {
            MonoScript script = MonoScript.FromMonoBehaviour(gameObject.AddComponent<GameServerFishNetAuthenticator>());
            string path = AssetDatabase.GetAssetPath(script);

            Assert.That(path, Is.EqualTo("Assets/Scripts/Networking/GameServerFishNetAuthenticator.cs"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }
}
