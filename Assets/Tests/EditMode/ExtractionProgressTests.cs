using LH.Main.Unity.Gameplay;
using NUnit.Framework;

public sealed class ExtractionProgressTests
{
    [Test]
    public void UpdateCompletesOnlyAfterContinuousHoldTime()
    {
        var progress = new ExtractionProgress(3f);

        Assert.That(progress.Update(true, 1f), Is.False);
        Assert.That(progress.Update(false, 1f), Is.False);
        Assert.That(progress.Update(true, 2f), Is.False);
        Assert.That(progress.Update(true, 1f), Is.True);
    }
}
