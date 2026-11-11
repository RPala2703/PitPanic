using System;
using UnityEditor;
using UnityEditor.Build.Reporting;

public static class PitPanicPresentationBuild
{
    public static void Run()
    {
        string path = Environment.GetEnvironmentVariable("PIT_PANIC_BUILD_PATH");
        if (string.IsNullOrEmpty(path))
            throw new InvalidOperationException("Set PIT_PANIC_BUILD_PATH to the output .app path.");
        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/PitPanicPrototype.unity" },
            locationPathName = path,
            target = BuildTarget.StandaloneOSX,
            options = BuildOptions.Development
        });
        if (report.summary.result != BuildResult.Succeeded)
            throw new InvalidOperationException($"Presentation build failed: {report.summary.result}.");
    }
}
