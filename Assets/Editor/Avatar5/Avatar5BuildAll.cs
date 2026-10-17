using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Avatar5.EditorTools
{
    /// <summary>
    /// One batch entry for CI-style runs: regenerates the generated scenes, builds every test player used by
    /// the multi-process scripts and the playable Windows development build (Level01 as entry scene).
    /// </summary>
    public static class Avatar5BuildAll
    {
        public const string PlayableOutput = "Artifacts/Windows/Avatar5.exe";

        /// <summary>Batch: Unity.exe -batchmode -projectPath . -executeMethod Avatar5.EditorTools.Avatar5BuildAll.BuildFromCommandLine -quit</summary>
        public static void BuildFromCommandLine()
        {
            try
            {
                Avatar5P03SessionSceneBuilder.BuildScene();
                Avatar5P03SessionSceneBuilder.BuildPlayer();
                Avatar5P03NetworkArenaBuilder.Build();
                Avatar5P03NetworkArenaBuilder.BuildPlayer();
                Avatar5P05Level01Builder.Build();
                Avatar5P05Level01Builder.BuildPlayer();
                BuildPlayable();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        [MenuItem("Avatar5/Build/Playable Windows Build (Level01)")]
        public static void BuildPlayable()
        {
            if (!File.Exists(Avatar5P05Level01Builder.ScenePath)) Avatar5P05Level01Builder.Build();
            var output = Path.GetFullPath(PlayableOutput);
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { Avatar5P05Level01Builder.ScenePath, Avatar5P03NetworkArenaBuilder.ScenePath },
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Playable build failed: " + report.summary.result);
            Debug.Log("Avatar5: playable build created: " + output + " (" + report.summary.totalSize / (1024 * 1024) + " MB)");
        }
    }
}
