using System;
using System.Collections.Generic;
using System.IO;
using Avatar5.Networking;
using Avatar5.Presentation;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Avatar5.EditorTools
{
    /// <summary>
    /// Builds the networked player prefab (from the P01 placeholder) and P03_NetworkArena: the P01 arena with
    /// the local player removed, a session object (NetworkManager, SessionRuntime, debug lobby, spawner) and four
    /// spawn points. Re-running overwrites the generated prefab and scene; P01 files are not modified.
    /// </summary>
    public static class Avatar5P03NetworkArenaBuilder
    {
        public const string ScenePath = "Assets/_Game/Scenes/P03_NetworkArena.unity";
        public const string PrefabPath = "Assets/_Game/Prefabs/NetworkPlayer.prefab";
        public const string NetworkPrefabsListPath = "Assets/DefaultNetworkPrefabs.asset";
        public const string PlayerOutput = "Artifacts/P03Arena/Avatar5_P03Arena.exe";
        private const int NetworkTickRate = 20;

        [MenuItem("Avatar5/Setup/Build P03 Network Arena")]
        public static void Build()
        {
            if (!File.Exists(Avatar5P01ArenaBuilder.ScenePath) || !File.Exists(Avatar5P01ArenaBuilder.PlayerPrefabPath))
                Avatar5P01ArenaBuilder.Build();

            var prefab = BuildPlayerPrefab();
            var prefabsList = RegisterNetworkPrefab(prefab);
            BuildScene(prefab, prefabsList);
            AssetDatabase.SaveAssets();
            Debug.Log("Avatar5: P03 network arena generated at " + ScenePath);
        }

        [MenuItem("Avatar5/Build/P03 Network Arena Player")]
        public static void BuildPlayer()
        {
            if (!File.Exists(ScenePath)) Build();
            var output = Path.GetFullPath(PlayerOutput);
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("P03 network arena build failed: " + report.summary.result);
            Debug.Log("Avatar5: P03 network arena player created: " + output);
        }

        /// <summary>Batch: -executeMethod Avatar5.EditorTools.Avatar5P03NetworkArenaBuilder.BuildPlayerFromCommandLine</summary>
        public static void BuildPlayerFromCommandLine()
        {
            Run(() => { Build(); BuildPlayer(); });
        }

        /// <summary>Batch: -executeMethod Avatar5.EditorTools.Avatar5P03NetworkArenaBuilder.BuildFromCommandLine</summary>
        public static void BuildFromCommandLine()
        {
            Run(Build);
        }

        private static void Run(Action action)
        {
            try { action(); }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static GameObject BuildPlayerPrefab()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Avatar5P01ArenaBuilder.PlayerPrefabPath);
            var root = (GameObject)PrefabUtility.InstantiatePrefab(source);
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            root.name = "NetworkPlayer";

            root.AddComponent<NetworkObject>();
            var netTransform = root.AddComponent<NetworkTransform>();
            netTransform.AuthorityMode = NetworkTransform.AuthorityModes.Server;
            netTransform.Interpolate = true;
            // Only yaw rotates; scale never changes.
            netTransform.SyncRotAngleX = false;
            netTransform.SyncRotAngleZ = false;
            netTransform.SyncScaleX = netTransform.SyncScaleY = netTransform.SyncScaleZ = false;
            root.AddComponent<NetworkPlayerAvatar>();

            // The input adapter must not drive the motor directly any more; the avatar wires it for the owner.
            var adapter = root.GetComponent<Avatar5.Input.PlayerInputAdapter>();
            if (adapter != null)
            {
                SetField(adapter, "commandSinkBehaviour", null);
                adapter.enabled = false;
            }

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static NetworkPrefabsList RegisterNetworkPrefab(GameObject prefab)
        {
            var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(NetworkPrefabsListPath);
            if (list == null)
            {
                list = ScriptableObject.CreateInstance<NetworkPrefabsList>();
                AssetDatabase.CreateAsset(list, NetworkPrefabsListPath);
            }
            if (!list.Contains(prefab)) list.Add(new NetworkPrefab { Prefab = prefab });
            EditorUtility.SetDirty(list);
            return list;
        }

        private static void BuildScene(GameObject prefab, NetworkPrefabsList prefabsList)
        {
            var scene = EditorSceneManager.OpenScene(Avatar5P01ArenaBuilder.ScenePath, OpenSceneMode.Single);

            Transform p01Spawn = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == "PlayerSpawn") p01Spawn = root.transform;
                if (root.name.StartsWith("Player_Placeholder", StringComparison.Ordinal)) UnityEngine.Object.DestroyImmediate(root);
            }
            var center = p01Spawn != null ? p01Spawn.position : new Vector3(0f, 0f, -8f);

            var spawnRoot = new GameObject("NetworkSpawnPoints").transform;
            var offsets = new[] { new Vector3(-1.5f, 0f, 0f), new Vector3(1.5f, 0f, 0f), new Vector3(-1.5f, 0f, -2.5f), new Vector3(1.5f, 0f, -2.5f) };
            var points = new List<Transform>();
            for (var i = 0; i < offsets.Length; i++)
            {
                var point = new GameObject("Spawn_" + (i + 1)).transform;
                point.SetParent(spawnRoot, false);
                point.position = center + offsets[i];
                points.Add(point);
            }

            var session = new GameObject("Session");
            var manager = session.AddComponent<NetworkManager>();
            var transport = session.AddComponent<UnityTransport>();
            manager.NetworkConfig = new NetworkConfig { NetworkTransport = transport, ConnectionApproval = true, TickRate = NetworkTickRate };
            manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(prefabsList);
            session.AddComponent<SessionRuntime>();
            session.AddComponent<SessionDebugLobby>();
            var spawner = session.AddComponent<NetworkPlayerSpawner>();
            spawner.Configure(prefab.GetComponent<NetworkObject>(), points.ToArray());

            var hud = UnityEngine.Object.FindAnyObjectByType<MovementDebugHud>();
            if (hud != null)
            {
                SetField(hud, "poseSourceBehaviour", null);
                hud.transform.position = Vector3.zero;
            }

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("Failed to save scene: " + ScenePath);
            AddSceneToBuildSettings(ScenePath);
        }

        private static void SetField(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field);
            if (property == null) throw new InvalidOperationException("Missing field " + field + " on " + target.GetType().Name);
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
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
