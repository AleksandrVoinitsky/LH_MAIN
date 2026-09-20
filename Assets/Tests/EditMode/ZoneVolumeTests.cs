using LH.Main.Unity.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class ZoneVolumeTests
{
    [Test]
    public void ContainsUsesSceneColliderBoundsForWorldPositions()
    {
        var gameObject = new GameObject("zone-volume-test");
        try
        {
            var collider = gameObject.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.size = new Vector3(4f, 2f, 6f);
            gameObject.transform.position = new Vector3(10f, 1f, -3f);
            var volume = gameObject.AddComponent<ZoneVolume>();
            Physics.SyncTransforms();

            Assert.That(volume.Contains(new Vector3(10f, 1f, -3f)), Is.True);
            Assert.That(volume.Contains(new Vector3(11.9f, 1.9f, -0.1f)), Is.True);
            Assert.That(volume.Contains(new Vector3(12.1f, 1f, -3f)), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(gameObject);
        }
    }

    [Test]
    public void ContainsReturnsFalseAndWarnsOnceWhenColliderIsMissing()
    {
        var gameObject = new GameObject("zone-volume-missing-collider-test");
        try
        {
            var volume = gameObject.AddComponent<ZoneVolume>();
            LogAssert.Expect(LogType.Warning, "ZoneVolume requires a Collider to evaluate containment.");

            Assert.That(volume.Contains(Vector3.zero), Is.False);
            Assert.That(volume.Contains(Vector3.one), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(gameObject);
        }
    }
}
