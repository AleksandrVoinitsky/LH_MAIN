using LH.Main.Contracts;
using Xunit;

namespace LH.Main.Contracts.Tests;

public sealed class ContractsAssemblyTests
{
    [Fact]
    public void ContractsAssemblyDoesNotReferenceUnityOrFishNet()
    {
        var referencedAssemblies = typeof(MatchmakingStatusResponse)
            .Assembly
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name)
            .ToArray();

        Assert.DoesNotContain("UnityEngine", referencedAssemblies);
        Assert.DoesNotContain("FishNet.Runtime", referencedAssemblies);
    }
}
