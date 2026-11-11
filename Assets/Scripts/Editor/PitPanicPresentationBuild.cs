using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;

public static class PitPanicPresentationBuild
{
    public static void Run()
    {
        Build(BuildTarget.StandaloneOSX, BuildOptions.Development);
    }

    public static void RunWindowsDemo()
    {
        Build(BuildTarget.StandaloneWindows64, BuildOptions.None);
    }

    private static void Build(BuildTarget target, BuildOptions options)
    {
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, target))
            throw new InvalidOperationException($"Install Unity {UnityEngine.Application.unityVersion} {target} build support through Unity Hub.");
        string path = Environment.GetEnvironmentVariable("PIT_PANIC_BUILD_PATH");
        if (string.IsNullOrEmpty(path))
            throw new InvalidOperationException("Set PIT_PANIC_BUILD_PATH to the output .app or .exe path.");
        if (target == BuildTarget.StandaloneWindows64 && Path.GetExtension(path) != ".exe")
            throw new InvalidOperationException("Windows demo output must end in .exe.");
        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/PitPanicPrototype.unity" },
            locationPathName = path,
            target = target,
            options = options
        });
        if (report.summary.result != BuildResult.Succeeded)
            throw new InvalidOperationException($"Presentation build failed: {report.summary.result}.");
    }
}
