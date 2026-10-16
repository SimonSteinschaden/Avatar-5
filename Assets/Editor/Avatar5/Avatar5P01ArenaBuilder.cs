using System;
using System.Collections.Generic;
using System.IO;
using Avatar5.Input;
using Avatar5.Player;
using Avatar5.Presentation;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Avatar5.EditorTools
{
    /// <summary>
    /// Generates the P01 greybox: movement/camera data assets, placeholder materials, the player prefab and the
    /// P01_TestArena scene, built from Unity primitives only. Re-running overwrites the generated scene and prefab.
    /// </summary>
    public static class Avatar5P01ArenaBuilder
    {
        public const string ScenePath = "Assets/_Game/Scenes/P01_TestArena.unity";
        public const string PlayerPrefabPath = "Assets/_Game/Prefabs/Player_Placeholder.prefab";
        public const string MovementDefinitionPath = "Assets/_Game/Data/PlayerMovementDefinition.asset";
        public const string CameraProfilePath = "Assets/_Game/Data/IsometricCameraProfile.asset";
        public const string InputActionsPath = "Assets/_Game/Input/Avatar5PlayerInput.inputactions";
        private const string MaterialFolder = "Assets/_Game/Materials";

        [MenuItem("Avatar5/Setup/Build P01 Test Arena")]
        public static void Build()
        {
            EnsureFolder("Assets/_Game/Data");
            EnsureFolder("Assets/_Game/Prefabs");
            EnsureFolder(MaterialFolder);

            var movement = LoadOrCreate<PlayerMovementDefinition>(MovementDefinitionPath);
            var cameraProfile = LoadOrCreate<CameraProfile>(CameraProfilePath);
            var inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            if (inputActions == null) throw new InvalidOperationException("Missing input actions: " + InputActionsPath);

            var materials = new Dictionary<string, Material>
            {
                { "Ground", LoadOrCreateMaterial("Greybox_Ground", new Color(0.55f, 0.58f, 0.52f)) },
                { "Wall", LoadOrCreateMaterial("Greybox_Wall", new Color(0.36f, 0.38f, 0.42f)) },
                { "Obstacle", LoadOrCreateMaterial("Greybox_Obstacle", new Color(0.72f, 0.52f, 0.33f)) },
                { "Player", LoadOrCreateMaterial("Placeholder_Player", new Color(0.2f, 0.45f, 0.85f)) },
                { "Aim", LoadOrCreateMaterial("Placeholder_Aim", new Color(0.95f, 0.75f, 0.1f)) }
            };

            var playerPrefab = BuildPlayerPrefab(movement, inputActions, materials);
            BuildScene(playerPrefab, cameraProfile, materials);
            AddSceneToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("Avatar5: P01 test arena generated at " + ScenePath);
        }

        /// <summary>Batch entry: Unity.exe -batchmode -executeMethod Avatar5.EditorTools.Avatar5P01ArenaBuilder.BuildFromCommandLine -quit</summary>
        public static void BuildFromCommandLine()
        {
            try
            {
                Build();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static GameObject BuildPlayerPrefab(PlayerMovementDefinition movement, InputActionAsset inputActions,
            IDictionary<string, Material> materials)
        {
            var root = new GameObject("Player_Placeholder");
            root.AddComponent<CharacterController>();
            var motor = root.AddComponent<CharacterMotor>();
            SetField(motor, "definition", movement);
            root.AddComponent<FallRespawn>();

            var body = CreatePrimitive(PrimitiveType.Capsule, "Body", root.transform, new Vector3(0f, 0.9f, 0f),
                new Vector3(0.8f, 0.9f, 0.8f), materials["Player"], false);
            CreatePrimitive(PrimitiveType.Cube, "FacingNose", body.transform, new Vector3(0f, 0.35f, 0.5f),
                new Vector3(0.35f, 0.2f, 0.4f), materials["Aim"], false);

            var marker = CreatePrimitive(PrimitiveType.Cylinder, "AimMarker", root.transform, new Vector3(0f, 0.1f, 1.4f),
                new Vector3(0.35f, 0.02f, 0.35f), materials["Aim"], false);
            var aimView = root.AddComponent<AimIndicatorView>();
            SetField(aimView, "poseSourceBehaviour", motor);
            SetField(aimView, "marker", marker.transform);

            var adapter = root.AddComponent<PlayerInputAdapter>();
            SetField(adapter, "actions", inputActions);
            SetField(adapter, "commandSinkBehaviour", motor);
            SetField(adapter, "actor", root.transform);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static void BuildScene(GameObject playerPrefab, CameraProfile cameraProfile, IDictionary<string, Material> materials)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var light = new GameObject("Directional Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var arena = new GameObject("Arena").transform;
            BuildArenaGeometry(arena, materials);

            var spawn = new GameObject("PlayerSpawn").transform;
            spawn.position = new Vector3(0f, 0f, -8f);

            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, scene);
            player.transform.SetPositionAndRotation(spawn.position, Quaternion.identity);

            var mainCamera = new GameObject("Main Camera");
            mainCamera.tag = "MainCamera";
            var outputCamera = mainCamera.AddComponent<Camera>();
            outputCamera.orthographic = cameraProfile.Orthographic;
            outputCamera.nearClipPlane = 0.3f;
            outputCamera.farClipPlane = 200f;
            mainCamera.AddComponent<AudioListener>();
            mainCamera.AddComponent<CinemachineBrain>();
            mainCamera.transform.SetPositionAndRotation(
                spawn.position - cameraProfile.Rotation * Vector3.forward * cameraProfile.Distance, cameraProfile.Rotation);

            var cmObject = new GameObject("CM Isometric Camera");
            var cmCamera = cmObject.AddComponent<CinemachineCamera>();
            cmObject.AddComponent<CinemachinePositionComposer>();
            var rig = cmObject.AddComponent<IsometricCameraRig>();
            SetField(rig, "profile", cameraProfile);
            SetField(rig, "virtualCamera", cmCamera);
            SetField(rig, "target", player.transform);
            SetField(rig, "outputCamera", outputCamera);
            cmCamera.Follow = player.transform;
            cmObject.transform.SetPositionAndRotation(mainCamera.transform.position, cameraProfile.Rotation);

            SetField(player.GetComponent<PlayerInputAdapter>(), "viewCamera", outputCamera);
            SetField(player.GetComponent<PlayerInputAdapter>(), "cameraRig", rig);
            SetField(player.GetComponent<FallRespawn>(), "spawnPoint", spawn);

            var hud = new GameObject("DebugHud").AddComponent<MovementDebugHud>();
            SetField(hud, "poseSourceBehaviour", player.GetComponent<CharacterMotor>());

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("Failed to save scene: " + ScenePath);
        }

        private static void BuildArenaGeometry(Transform arena, IDictionary<string, Material> materials)
        {
            const float size = 40f;
            CreatePrimitive(PrimitiveType.Cube, "Ground", arena, new Vector3(0f, -0.5f, 0f), new Vector3(size, 1f, size), materials["Ground"]);

            // Perimeter
            CreatePrimitive(PrimitiveType.Cube, "Wall_North", arena, new Vector3(0f, 1f, size * 0.5f), new Vector3(size, 2f, 1f), materials["Wall"]);
            CreatePrimitive(PrimitiveType.Cube, "Wall_South", arena, new Vector3(0f, 1f, -size * 0.5f), new Vector3(size, 2f, 1f), materials["Wall"]);
            CreatePrimitive(PrimitiveType.Cube, "Wall_East", arena, new Vector3(size * 0.5f, 1f, 0f), new Vector3(1f, 2f, size), materials["Wall"]);
            CreatePrimitive(PrimitiveType.Cube, "Wall_West", arena, new Vector3(-size * 0.5f, 1f, 0f), new Vector3(1f, 2f, size), materials["Wall"]);

            // Crates and pillars in the open field
            CreatePrimitive(PrimitiveType.Cube, "Crate_A", arena, new Vector3(-5f, 0.75f, -3f), new Vector3(1.5f, 1.5f, 1.5f), materials["Obstacle"]);
            CreatePrimitive(PrimitiveType.Cube, "Crate_B", arena, new Vector3(4f, 0.6f, -4f), new Vector3(1.2f, 1.2f, 1.2f), materials["Obstacle"]);
            CreatePrimitive(PrimitiveType.Cube, "Crate_C", arena, new Vector3(6f, 1f, 3f), new Vector3(2f, 2f, 2f), materials["Obstacle"]);
            for (var i = 0; i < 5; i++)
            {
                CreatePrimitive(PrimitiveType.Cylinder, "Pillar_" + (i + 1), arena,
                    new Vector3(-12f + i * 2.5f, 1.25f, 4f + (i % 2) * 2f), new Vector3(0.9f, 1.25f, 0.9f), materials["Wall"]);
            }

            // Diagonal wall to test sliding along non-axis-aligned colliders
            var diagonal = CreatePrimitive(PrimitiveType.Cube, "Wall_Diagonal", arena, new Vector3(-8f, 1f, -10f), new Vector3(8f, 2f, 0.6f), materials["Wall"]);
            diagonal.transform.rotation = Quaternion.Euler(0f, 45f, 0f);

            // Test track: 2.4 m wide corridor with a gap wall at its end
            CreatePrimitive(PrimitiveType.Cube, "Corridor_Left", arena, new Vector3(10.8f, 1f, -10f), new Vector3(0.6f, 2f, 12f), materials["Wall"]);
            CreatePrimitive(PrimitiveType.Cube, "Corridor_Right", arena, new Vector3(14.2f, 1f, -10f), new Vector3(0.6f, 2f, 12f), materials["Wall"]);

            // Low step (below CharacterController step offset) and a ramp
            CreatePrimitive(PrimitiveType.Cube, "Step_Low", arena, new Vector3(0f, 0.1f, 10f), new Vector3(4f, 0.2f, 2f), materials["Obstacle"]);
            var ramp = CreatePrimitive(PrimitiveType.Cube, "Ramp", arena, new Vector3(10f, 0.6f, 12f), new Vector3(4f, 0.3f, 6f), materials["Obstacle"]);
            ramp.transform.rotation = Quaternion.Euler(-12f, 0f, 0f);
        }

        private static GameObject CreatePrimitive(PrimitiveType type, string name, Transform parent, Vector3 localPosition,
            Vector3 localScale, Material material, bool keepCollider = true)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            if (!keepCollider) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            if (keepCollider) GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            return go;
        }

        private static Material LoadOrCreateMaterial(string name, Color color)
        {
            var path = MaterialFolder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP Lit shader not found");
            material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void SetField(UnityEngine.Object target, string fieldName, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(fieldName);
            if (property == null) throw new InvalidOperationException(target.GetType().Name + " has no serialized field " + fieldName);
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
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
