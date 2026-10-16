using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Avatar5.EditorTools.Rendering
{
    /// <summary>
    /// Builds a Windows x64 development player containing only VisualStyle_Test for frame-time
    /// measurements (see VisualStyleBenchmark). Output goes to Artifacts/ (git-ignored).
    /// Run the result with: Avatar5_P07a.exe -screen-width 1920 -screen-height 1080 -screen-fullscreen 1 -a5benchmark result.csv
    /// </summary>
    public static class VisualStyleBenchmarkBuild
    {
        public const string OutputPath = "Artifacts/P07aBenchmark/Avatar5_P07a.exe";

        [MenuItem("Avatar5/Build/P07a Visual Style Benchmark (Windows x64)")]
        public static void Build()
        {
            // GPU frame times need FrameTimingManager; enabled only for this build and restored afterwards
            // so the project settings stay untouched.
            var previousTiming = PlayerSettings.enableFrameTimingStats;
            PlayerSettings.enableFrameTimingStats = true;
            var previousBackground = PlayerSettings.runInBackground;
            PlayerSettings.runInBackground = true;
            try
            {
                var output = Path.GetFullPath(OutputPath);
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { VisualStyleSceneBuilder.ScenePath },
                    locationPathName = output,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.Development
                });
                if (report.summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException("Avatar5 P07a benchmark build failed: " + report.summary.result);
                Debug.Log("Avatar5: P07a benchmark player built at " + output);
            }
            finally
            {
                PlayerSettings.enableFrameTimingStats = previousTiming;
                PlayerSettings.runInBackground = previousBackground;
            }
        }

        public static void BuildFromCommandLine()
        {
            try
            {
                Build();
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }
    }
}
