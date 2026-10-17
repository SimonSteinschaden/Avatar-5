using System;
using System.Collections.Generic;
using System.IO;
using Avatar5.Contracts;
using Avatar5.Networking;
using Avatar5.World;
using Unity.AI.Navigation;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace Avatar5.EditorTools
{
    /// <summary>
    /// Generates the Level01 greybox from the P03 network arena (session, camera, HUD and views are reused):
    /// start area with lever + gate A, brute arena, brazier puzzle + gate B, goal. Re-running overwrites Level01.
    /// </summary>
    public static class Avatar5P05Level01Builder
    {
        public const string ScenePath = "Assets/_Game/Scenes/Level01.unity";
        public const string PlayerOutput = "Artifacts/Level01/Avatar5_Level01.exe";

        public const int LeverId = 1001, GateAId = 1101, BrazierId = 2001, GateBId = 2101, GoalId = 9001;

        [MenuItem("Avatar5/Setup/Build Level01 Greybox")]
        public static void Build()
        {
            if (!File.Exists(Avatar5P03NetworkArenaBuilder.ScenePath)) Avatar5P03NetworkArenaBuilder.Build();
            var scene = EditorSceneManager.OpenScene(Avatar5P03NetworkArenaBuilder.ScenePath, OpenSceneMode.Single);
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == "Arena" || root.name == "NetworkSpawnPoints" || root.name == "TrainingDummyPoints" || root.name == "PlayerSpawn")
                    UnityEngine.Object.DestroyImmediate(root);
            }

            var materials = new Dictionary<string, Material>
            {
                { "Ground", Load("Greybox_Ground") }, { "Wall", Load("Greybox_Wall") }, { "Obstacle", Load("Greybox_Obstacle") }, { "Aim", Load("Placeholder_Aim") }
            };

            var level = new GameObject("Level01");
            level.AddComponent<LevelAuthoring>().Configure(1, "level01");
            var surface = level.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            var t = level.transform;

            Box("Floor", t, new Vector3(0f, -0.5f, 22f), new Vector3(25f, 1f, 53f), materials["Ground"]);
            Box("Wall_West", t, new Vector3(-12.5f, 1.25f, 22f), new Vector3(1f, 2.5f, 53f), materials["Wall"]);
            Box("Wall_East", t, new Vector3(12.5f, 1.25f, 22f), new Vector3(1f, 2.5f, 53f), materials["Wall"]);
            Box("Wall_South", t, new Vector3(0f, 1.25f, -4.5f), new Vector3(25f, 2.5f, 1f), materials["Wall"]);
            Box("Wall_North", t, new Vector3(0f, 1.25f, 48.5f), new Vector3(25f, 2.5f, 1f), materials["Wall"]);

            // Section 1: lever opens gate A.
            Divider("Divider_A", t, 12f, materials["Wall"]);
            Gate("GateA", t, 12f, GateAId, new[] { LeverId }, materials["Obstacle"]);
            Lever(t, new Vector3(4f, 0f, 8f), materials);

            // Section 2: the brute, some cover, the brazier that opens gate B together with gate A.
            Box("Crate_1", t, new Vector3(-6f, 0.75f, 20f), new Vector3(1.5f, 1.5f, 1.5f), materials["Obstacle"]);
            Box("Crate_2", t, new Vector3(6f, 0.75f, 18f), new Vector3(1.5f, 1.5f, 1.5f), materials["Obstacle"]);
            Brazier(t, new Vector3(7f, 0f, 32f), materials);
            Divider("Divider_B", t, 36f, materials["Wall"]);
            Gate("GateB", t, 36f, GateBId, new[] { BrazierId, GateAId }, materials["Obstacle"]);

            // Section 3: the exit.
            var goal = new GameObject("Goal");
            goal.transform.SetParent(t, false);
            goal.transform.position = new Vector3(0f, 0f, 43f);
            goal.AddComponent<WorldObjectAuthoring>().Configure(GoalId, WorldObjectKind.Goal, 2.5f, new[] { GateBId }, null);
            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "GoalMarker";
            UnityEngine.Object.DestroyImmediate(disc.GetComponent<Collider>());
            disc.transform.SetParent(goal.transform, false);
            disc.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            disc.transform.localScale = new Vector3(5f, 0.02f, 5f);
            disc.GetComponent<Renderer>().sharedMaterial = materials["Aim"];

            // Spawns and the brute.
            var spawnRoot = new GameObject("NetworkSpawnPoints").transform;
            var spawns = new List<Transform>();
            foreach (var p in new[] { new Vector3(-1.5f, 0f, 2f), new Vector3(1.5f, 0f, 2f), new Vector3(-1.5f, 0f, -0.5f), new Vector3(1.5f, 0f, -0.5f) })
            {
                var s = new GameObject("Spawn_" + (spawns.Count + 1)).transform;
                s.SetParent(spawnRoot, false);
                s.position = p;
                spawns.Add(s);
            }
            var enemyRoot = new GameObject("EnemyPoints").transform;
            var brutePoint = new GameObject("Enemy_Brute_1").transform;
            brutePoint.SetParent(enemyRoot, false);
            brutePoint.position = new Vector3(0f, 0f, 24f);
            brutePoint.rotation = Quaternion.Euler(0f, 180f, 0f);

            var session = UnityEngine.Object.FindAnyObjectByType<SessionRuntime>().gameObject;
            var spawner = session.GetComponent<NetworkPlayerSpawner>();
            spawner.Configure(spawner.PlayerPrefab, spawns.ToArray());
            var combat = session.GetComponent<CombatHost>();
            combat.Configure(combat.Abilities, null, new Transform[0]);
            combat.ConfigureEnemies(AssetDatabase.LoadAssetAtPath<GameObject>(Avatar5P03NetworkArenaBuilder.BrutePrefabPath).GetComponent<NetworkObject>(), new[] { brutePoint });
            if (session.GetComponent<WorldHost>() == null) session.AddComponent<WorldHost>();

            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new InvalidOperationException("Failed to save " + ScenePath);
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(s => s.path == ScenePath)) scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("Avatar5: Level01 greybox generated at " + ScenePath);
        }

        [MenuItem("Avatar5/Build/Level01 Player")]
        public static void BuildPlayer()
        {
            if (!File.Exists(ScenePath)) Build();
            var output = Path.GetFullPath(PlayerOutput);
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath }, locationPathName = output, target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Level01 build failed: " + report.summary.result);
            Debug.Log("Avatar5: Level01 player created: " + output);
        }

        /// <summary>Batch: -executeMethod Avatar5.EditorTools.Avatar5P05Level01Builder.BuildPlayerFromCommandLine</summary>
        public static void BuildPlayerFromCommandLine()
        {
            try
            {
                Avatar5P03NetworkArenaBuilder.Build();
                Build();
                BuildPlayer();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static Material Load(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Materials/" + name + ".mat");
        }

        private static GameObject Box(string name, Transform parent, Vector3 position, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.localScale = size;
            if (material != null) go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        private static void Divider(string name, Transform parent, float z, Material material)
        {
            Box(name + "_West", parent, new Vector3(-7f, 1.25f, z), new Vector3(10f, 2.5f, 1f), material);
            Box(name + "_East", parent, new Vector3(7f, 1.25f, z), new Vector3(10f, 2.5f, 1f), material);
        }

        private static void Gate(string name, Transform parent, float z, int id, int[] requires, Material material)
        {
            var gate = new GameObject(name);
            gate.transform.SetParent(parent, false);
            gate.transform.position = new Vector3(0f, 0f, z);
            var leaf = Box(name + "_Leaf", gate.transform, new Vector3(0f, 1.25f, z), new Vector3(4f, 2.5f, 0.8f), material);
            var obstacle = leaf.AddComponent<NavMeshObstacle>();
            obstacle.carving = true;
            obstacle.size = Vector3.one;
            gate.AddComponent<WorldObjectAuthoring>().Configure(id, WorldObjectKind.Gate, 2f, requires, leaf.transform);
            // The animated transform is the leaf itself: Apply moves its local position, so keep it at the gate origin.
            leaf.transform.localPosition = new Vector3(0f, 1.25f, 0f);
            var holder = new GameObject(name + "_LeafHolder").transform;
            holder.SetParent(gate.transform, false);
            leaf.transform.SetParent(holder, true);
            gate.GetComponent<WorldObjectAuthoring>().Configure(id, WorldObjectKind.Gate, 2f, requires, holder);
        }

        private static void Lever(Transform parent, Vector3 position, IDictionary<string, Material> materials)
        {
            var lever = new GameObject("Lever");
            lever.transform.SetParent(parent, false);
            lever.transform.position = position;
            Box("Lever_Base", lever.transform, position + new Vector3(0f, 0.3f, 0f), new Vector3(0.8f, 0.6f, 0.8f), materials["Wall"]);
            var pivot = new GameObject("Lever_Pivot").transform;
            pivot.SetParent(lever.transform, false);
            pivot.localPosition = new Vector3(0f, 0.6f, 0f);
            var handle = Box("Lever_Handle", pivot, position + new Vector3(0f, 1.1f, 0f), new Vector3(0.15f, 1f, 0.15f), materials["Aim"]);
            UnityEngine.Object.DestroyImmediate(handle.GetComponent<Collider>());
            handle.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            lever.AddComponent<WorldObjectAuthoring>().Configure(LeverId, WorldObjectKind.Lever, 2.2f, new int[0], pivot);
        }

        private static void Brazier(Transform parent, Vector3 position, IDictionary<string, Material> materials)
        {
            var brazier = new GameObject("Brazier");
            brazier.transform.SetParent(parent, false);
            brazier.transform.position = position;
            var bowl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            bowl.name = "Brazier_Bowl";
            bowl.transform.SetParent(brazier.transform, false);
            bowl.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            bowl.transform.localScale = new Vector3(1.2f, 0.6f, 1.2f);
            bowl.GetComponent<Renderer>().sharedMaterial = materials["Wall"];
            var flame = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            flame.name = "Brazier_Flame";
            UnityEngine.Object.DestroyImmediate(flame.GetComponent<Collider>());
            flame.transform.SetParent(brazier.transform, false);
            flame.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            flame.transform.localScale = new Vector3(0.8f, 1f, 0.8f);
            flame.GetComponent<Renderer>().sharedMaterial = materials["Aim"];
            brazier.AddComponent<WorldObjectAuthoring>().Configure(BrazierId, WorldObjectKind.Brazier, 1.2f, new int[0], flame.transform, ElementKind.Fire);
        }
    }
}
