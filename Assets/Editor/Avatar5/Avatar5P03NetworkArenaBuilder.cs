using System;
using System.Collections.Generic;
using System.IO;
using Avatar5.Combat;
using Avatar5.Contracts;
using Avatar5.Gameplay.Combat;
using Avatar5.Gameplay.Enemies;
using Unity.AI.Navigation;
using UnityEngine.AI;
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
        public const string DummyPrefabPath = "Assets/_Game/Prefabs/TrainingDummy.prefab";
        public const string BrutePrefabPath = "Assets/_Game/Prefabs/Enemy_Brute.prefab";
        public const string AbilityFolder = "Assets/_Game/Data/Abilities";
        private static readonly int[] PlayerLoadout = { StarterAbilities.FireballId, StarterAbilities.WaterWaveId };
        public const string NetworkPrefabsListPath = "Assets/DefaultNetworkPrefabs.asset";
        public const string PlayerOutput = "Artifacts/P03Arena/Avatar5_P03Arena.exe";
        private const int NetworkTickRate = 20;

        [MenuItem("Avatar5/Setup/Build P03 Network Arena")]
        public static void Build()
        {
            if (!File.Exists(Avatar5P01ArenaBuilder.ScenePath) || !File.Exists(Avatar5P01ArenaBuilder.PlayerPrefabPath))
                Avatar5P01ArenaBuilder.Build();

            var abilities = EnsureAbilityAssets();
            var prefab = BuildPlayerPrefab();
            var dummy = BuildDummyPrefab();
            RegisterNetworkPrefab(dummy);
            var brute = BuildBrutePrefab();
            RegisterNetworkPrefab(brute);
            var prefabsList = RegisterNetworkPrefab(prefab);
            BuildScene(prefab, dummy, brute, abilities, prefabsList);
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
            root.AddComponent<NetworkCombatActor>().Configure(TeamKind.Players, 100f, 100f, 15f, PlayerLoadout, 5f);
            var bar = root.AddComponent<HealthBarView>();
            var body = root.transform.Find("Body");
            if (body != null) SetField(bar, "tintTarget", body.GetComponent<Renderer>());

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

        private static AbilityDefinitionAsset[] EnsureAbilityAssets()
        {
            if (!AssetDatabase.IsValidFolder(AbilityFolder))
            {
                if (!AssetDatabase.IsValidFolder("Assets/_Game/Data")) AssetDatabase.CreateFolder("Assets/_Game", "Data");
                AssetDatabase.CreateFolder("Assets/_Game/Data", "Abilities");
            }
            var definitions = new[] { StarterAbilities.Fireball(), StarterAbilities.WaterWave(), StarterEnemies.BruteSlam() };
            var assets = new AbilityDefinitionAsset[definitions.Length];
            for (var i = 0; i < definitions.Length; i++)
            {
                var path = AbilityFolder + "/" + definitions[i].Key + ".asset";
                var asset = AssetDatabase.LoadAssetAtPath<AbilityDefinitionAsset>(path);
                if (asset == null)
                {
                    asset = ScriptableObject.CreateInstance<AbilityDefinitionAsset>();
                    asset.CopyFrom(definitions[i]);
                    AssetDatabase.CreateAsset(asset, path);
                }
                assets[i] = asset;
            }
            return assets;
        }

        private static GameObject BuildDummyPrefab()
        {
            var root = new GameObject("TrainingDummy");
            var controller = root.AddComponent<CharacterController>();
            controller.radius = 0.5f;
            controller.height = 1.8f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.minMoveDistance = 0f;

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            UnityEngine.Object.DestroyImmediate(body.GetComponent<Collider>());
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            body.transform.localScale = new Vector3(0.9f, 0.9f, 0.9f);
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Materials/Greybox_Obstacle.mat");
            if (material != null) body.GetComponent<Renderer>().sharedMaterial = material;

            root.AddComponent<NetworkObject>();
            var netTransform = root.AddComponent<NetworkTransform>();
            netTransform.AuthorityMode = NetworkTransform.AuthorityModes.Server;
            netTransform.Interpolate = true;
            netTransform.SyncRotAngleX = netTransform.SyncRotAngleY = netTransform.SyncRotAngleZ = false;
            netTransform.SyncScaleX = netTransform.SyncScaleY = netTransform.SyncScaleZ = false;
            root.AddComponent<NetworkCombatActor>().Configure(TeamKind.Enemies, 5000f, 0f, 0f, new int[0], 3f);
            var bar = root.AddComponent<HealthBarView>();
            SetField(bar, "tintTarget", body.GetComponent<Renderer>());

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, DummyPrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject BuildBrutePrefab()
        {
            var definition = StarterEnemies.Brute();
            var root = new GameObject("Enemy_Brute");
            var agent = root.AddComponent<NavMeshAgent>();
            agent.radius = 0.6f;
            agent.height = 2f;
            agent.speed = definition.MoveSpeed;
            agent.angularSpeed = 540f;
            agent.acceleration = 20f;

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            UnityEngine.Object.DestroyImmediate(body.GetComponent<Collider>());
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 1f, 0f);
            body.transform.localScale = new Vector3(1.2f, 1f, 1.2f);
            var tint = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Materials/Greybox_Wall.mat");
            if (tint != null) body.GetComponent<Renderer>().sharedMaterial = tint;
            var horn = GameObject.CreatePrimitive(PrimitiveType.Cube);
            horn.name = "FacingMarker";
            UnityEngine.Object.DestroyImmediate(horn.GetComponent<Collider>());
            horn.transform.SetParent(body.transform, false);
            horn.transform.localPosition = new Vector3(0f, 0.4f, 0.5f);
            horn.transform.localScale = new Vector3(0.5f, 0.2f, 0.3f);

            // Hit volume so projectiles and walls checks see the brute like any other actor.
            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 1f, 0f);
            capsule.radius = 0.6f;
            capsule.height = 2f;

            root.AddComponent<NetworkObject>();
            var netTransform = root.AddComponent<NetworkTransform>();
            netTransform.AuthorityMode = NetworkTransform.AuthorityModes.Server;
            netTransform.Interpolate = true;
            netTransform.SyncRotAngleX = netTransform.SyncRotAngleZ = false;
            netTransform.SyncScaleX = netTransform.SyncScaleY = netTransform.SyncScaleZ = false;
            root.AddComponent<NetworkCombatActor>().Configure(TeamKind.Enemies, definition.MaxHealth, 0f, 0f,
                new[] { StarterEnemies.BruteSlamAbilityId }, 8f);
            root.AddComponent<EnemyBrainHost>();
            var bar = root.AddComponent<HealthBarView>();
            SetField(bar, "tintTarget", body.GetComponent<Renderer>());

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, BrutePrefabPath);
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

        private static void BuildScene(GameObject prefab, GameObject dummy, GameObject brute, AbilityDefinitionAsset[] abilities, NetworkPrefabsList prefabsList)
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

            var dummyRoot = new GameObject("TrainingDummyPoints").transform;
            var dummyPoints = new List<Transform>();
            foreach (var offset in new[] { new Vector3(-1.5f, 0f, 4f), new Vector3(1.5f, 0f, 4f) })
            {
                var point = new GameObject("Dummy_" + (dummyPoints.Count + 1)).transform;
                point.SetParent(dummyRoot, false);
                point.position = center + offset;
                point.rotation = Quaternion.Euler(0f, 180f, 0f);
                dummyPoints.Add(point);
            }
            var combatHost = session.AddComponent<CombatHost>();
            combatHost.Configure(abilities, dummy.GetComponent<NetworkObject>(), dummyPoints.ToArray());
            var enemyPoint = new GameObject("Enemy_Brute_1").transform;
            enemyPoint.SetParent(dummyRoot, false);
            // 12 m from the spawn points: outside aggro range, so the brute waits until players walk up.
            enemyPoint.position = center + new Vector3(0f, 0f, 12f);
            enemyPoint.rotation = Quaternion.Euler(0f, 180f, 0f);
            combatHost.ConfigureEnemies(brute.GetComponent<NetworkObject>(), new[] { enemyPoint });

            // Walkable area is baked by the host on load (CombatHost); the surface only describes what to collect.
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name != "Arena") continue;
                var surface = root.GetComponent<NavMeshSurface>() ?? root.AddComponent<NavMeshSurface>();
                surface.collectObjects = CollectObjects.Children;
                surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            }

            var views = new GameObject("CombatViews");
            views.AddComponent<CombatCueView>().Configure(new[]
            {
                new CueVisual { CueId = StarterAbilities.FireballCue, Kind = CueVisualKind.Projectile, Color = new Color(1f, 0.45f, 0.05f, 1f),
                    Speed = StarterAbilities.Fireball().ProjectileSpeed, Range = StarterAbilities.Fireball().Range },
                new CueVisual { CueId = StarterAbilities.WaterWaveCue, Kind = CueVisualKind.Cone, Color = new Color(0.2f, 0.55f, 1f, 0.8f),
                    Range = StarterAbilities.WaterWave().Range, ConeHalfAngleDegrees = StarterAbilities.WaterWave().ConeHalfAngleDegrees },
                new CueVisual { CueId = StarterEnemies.BruteTelegraphCue, Kind = CueVisualKind.Telegraph, Color = new Color(0.95f, 0.15f, 0.1f, 1f),
                    Range = StarterEnemies.BruteSlam().Range, Duration = StarterEnemies.Brute().TelegraphDuration },
                new CueVisual { CueId = StarterEnemies.BruteSlamCue, Kind = CueVisualKind.Burst, Color = new Color(0.55f, 0.35f, 0.15f, 0.9f),
                    Range = StarterEnemies.BruteSlam().Range }
            });
            views.AddComponent<CombatFeedbackView>();

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
