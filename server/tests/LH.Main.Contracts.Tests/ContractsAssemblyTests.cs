using LH.Main.Contracts;
using Xunit;

namespace LH.Main.Contracts.Tests;

public sealed class ContractsAssemblyTests
{
    [Fact]
    public void ContractsAssemblyDoesNotReferenceUnityOrFishNet()
    {
        var references = typeof(HealthStatusResponse).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name);

        Assert.DoesNotContain("UnityEngine", references);
        Assert.DoesNotContain("FishNet", references);
    }
}
