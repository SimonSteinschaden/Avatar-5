using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Avatar5.EditorTools
{
    public static class Avatar5WindowsBuild
    {
        [MenuItem("Avatar5/Build/Windows x64 Development")]
        public static void BuildDevelopment()
        {
            var scenes = EditorBuildSettings.scenes;
            if (scenes.Length == 0 || !File.Exists("Assets/_Game/Scenes/Bootstrap.unity"))
                throw new InvalidOperationException("Run Avatar5/Setup/Generate Starter Scenes first.");

            var output = Path.GetFullPath("Artifacts/Windows/Avatar5.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var options = new BuildPlayerOptions
            {
                scenes = Array.ConvertAll(scenes, s => s.path),
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            };
            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Avatar5 Windows build failed: " + report.summary.result);
            Debug.Log("Avatar5 build created: " + output);
        }
    }
}
