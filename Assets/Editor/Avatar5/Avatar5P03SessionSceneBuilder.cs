using System;
using System.Collections.Generic;
using System.IO;
using Avatar5.Networking;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Avatar5.EditorTools
{
    /// <summary>
    /// Generates P03_SessionTest (NetworkManager + UnityTransport + SessionRuntime + debug lobby) and builds a
    /// Windows development player containing only that scene for multi-process session tests.
    /// Re-running overwrites the generated scene.
    /// </summary>
    public static class Avatar5P03SessionSceneBuilder
    {
        public const string ScenePath = "Assets/_Game/Scenes/P03_SessionTest.unity";
        public const string PlayerOutput = "Artifacts/P03/Avatar5_P03.exe";

        [MenuItem("Avatar5/Setup/Build P03 Session Test Scene")]
        public static void BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camera = new GameObject("Main Camera");
            camera.tag = "MainCamera";
            var cam = camera.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.13f, 0.16f);

            var session = new GameObject("Session");
            var manager = session.AddComponent<NetworkManager>();
            var transport = session.AddComponent<UnityTransport>();
            manager.NetworkConfig = new NetworkConfig { NetworkTransport = transport, ConnectionApproval = true };
            session.AddComponent<SessionRuntime>();
            session.AddComponent<SessionDebugLobby>();

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("Failed to save scene: " + ScenePath);
            AddSceneToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("Avatar5: P03 session test scene generated at " + ScenePath);
        }

        [MenuItem("Avatar5/Build/P03 Session Test Player")]
        public static void BuildPlayer()
        {
            if (!File.Exists(ScenePath)) BuildScene();
            var output = Path.GetFullPath(PlayerOutput);
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            };
            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("P03 session test build failed: " + report.summary.result);
            Debug.Log("Avatar5: P03 session test player created: " + output);
        }

        /// <summary>Batch: Unity.exe -batchmode -executeMethod Avatar5.EditorTools.Avatar5P03SessionSceneBuilder.BuildSceneFromCommandLine -quit</summary>
        public static void BuildSceneFromCommandLine()
        {
            Run(BuildScene);
        }

        /// <summary>Batch: Unity.exe -batchmode -executeMethod Avatar5.EditorTools.Avatar5P03SessionSceneBuilder.BuildPlayerFromCommandLine -quit</summary>
        public static void BuildPlayerFromCommandLine()
        {
            Run(() => { BuildScene(); BuildPlayer(); });
        }

        private static void Run(Action action)
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void AddSceneToBuildSettings(string scenePath)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Exists(s => s.path == scenePath)) return;
            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
