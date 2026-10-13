using System;
using System.IO;
using Avatar5.Foundation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Avatar5.EditorTools
{
    /// <summary>Idempotent starter-scene creation after importing this repository into a Unity URP template.</summary>
    public static class Avatar5ProjectSetup
    {
        private const string SceneFolder = "Assets/_Game/Scenes";
        private static readonly string[] SceneNames = { "Bootstrap", "MainMenu", "Lobby", "Gameplay_Test" };

        [MenuItem("Avatar5/Setup/Generate Starter Scenes")]
        public static void GenerateStarterScenes()
        {
            Directory.CreateDirectory(SceneFolder);
            foreach (var sceneName in SceneNames)
            {
                var scenePath = SceneFolder + "/" + sceneName + ".unity";
                if (File.Exists(scenePath))
                {
                    Debug.Log("Avatar5: scene already exists, preserving " + scenePath);
                    continue;
                }

                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                SetupScene(sceneName);
                if (!EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), scenePath))
                    throw new InvalidOperationException("Failed to save scene: " + scenePath);
            }

            var scenes = new EditorBuildSettingsScene[SceneNames.Length];
            for (var i = 0; i < SceneNames.Length; i++)
                scenes[i] = new EditorBuildSettingsScene(SceneFolder + "/" + SceneNames[i] + ".unity", true);
            EditorBuildSettings.scenes = scenes;
            AssetDatabase.Refresh();
            EditorSceneManager.OpenScene(SceneFolder + "/Bootstrap.unity");
            Debug.Log("Avatar5: starter scenes configured. Run Bootstrap scene to open MainMenu.");
        }

        private static void SetupScene(string sceneName)
        {
            var cameraObject = new GameObject("Main Camera");
            var camera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
            camera.orthographic = true;
            camera.orthographicSize = 9f;
            cameraObject.transform.position = new Vector3(13f, 17f, -13f);
            cameraObject.transform.LookAt(Vector3.zero);
            cameraObject.AddComponent<AudioListener>();

            var lightObject = new GameObject("Directional Light");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.25f;
            lightObject.transform.rotation = Quaternion.Euler(45, -30, 0);

            if (sceneName == "Bootstrap")
            {
                new GameObject("GameBootstrap").AddComponent<GameBootstrap>();
            }
            else if (sceneName == "MainMenu" || sceneName == "Lobby")
            {
                new GameObject(sceneName + "_DebugUI").AddComponent<DebugMainMenu>();
            }
            else if (sceneName == "Gameplay_Test")
            {
                new GameObject("DebugUI").AddComponent<DebugMainMenu>();
                var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
                ground.name = "Greybox Ground";
                ground.transform.localScale = new Vector3(3, 1, 3);

                var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                player.name = "Player Placeholder – no movement yet";
                player.transform.position = new Vector3(-2, 1, 0);

                var target = GameObject.CreatePrimitive(PrimitiveType.Cube);
                target.name = "Enemy Placeholder – no AI yet";
                target.transform.position = new Vector3(3, 0.5f, 1);

                for (var i = 0; i < 3; i++)
                {
                    var rock = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    rock.name = "World Obstacle " + (i + 1);
                    rock.transform.position = new Vector3(i * 2f - 2f, 0.6f, 4f);
                    rock.transform.localScale = new Vector3(1f, 1.2f, 1f);
                }
            }
        }
    }
}
