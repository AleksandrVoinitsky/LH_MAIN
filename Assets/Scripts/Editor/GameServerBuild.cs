using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace LH.Main.Unity.Editor
{
    public static class GameServerBuild
    {
        private const string ScenePath = "Assets/Scenes/Server/ServerBootstrap.unity";
        private const string OutputDirectory = "Builds/GameServer/LinuxHeadless";
        private const string OutputPath = OutputDirectory + "/LH.Main.GameServer.x86_64";

        [MenuItem("LH Main/Build/Linux Headless Game Server")]
        public static void BuildLinuxHeadless()
        {
            Directory.CreateDirectory(OutputDirectory);

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = OutputPath,
                target = BuildTarget.StandaloneLinux64,
                subtarget = (int)StandaloneBuildSubtarget.Server,
                options = BuildOptions.EnableHeadlessMode
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException($"Game server build failed: {report.summary.result}");
        }
    }
}
