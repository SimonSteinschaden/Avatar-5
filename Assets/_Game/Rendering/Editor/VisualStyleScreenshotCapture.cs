using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avatar5.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Avatar5.EditorTools.Rendering
{
    /// <summary>
    /// Renders the P07a review screenshots (1080p, identical camera) into docs/p07a_screenshots.
    /// Menu: Avatar5/Setup/Capture P07a Screenshots, or
    /// -executeMethod Avatar5.EditorTools.Rendering.VisualStyleScreenshotCapture.CaptureFromCommandLine
    /// </summary>
    public static class VisualStyleScreenshotCapture
    {
        public const string OutputFolder = "docs/p07a_screenshots";

        [MenuItem("Avatar5/Setup/Capture P07a Screenshots")]
        public static void CaptureFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var files = CaptureAll(OutputFolder);
            EditorUtility.RevealInFinder(files.First());
        }

        public static void CaptureFromCommandLine()
        {
            try
            {
                CaptureAll(OutputFolder);
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        public sealed class SceneHandles
        {
            public Camera Camera;
            public VisualStyleCameraRig Rig;
            public GraphicsQualityController Quality;
            public LightingPresetSwitcher Lighting;
            public Transform Hero;
            public Transform Enemy;
        }

        public static SceneHandles OpenScene()
        {
            EditorSceneManager.OpenScene(VisualStyleSceneBuilder.ScenePath, OpenSceneMode.Single);
            var handles = new SceneHandles
            {
                Camera = Camera.main,
                Rig = UnityEngine.Object.FindAnyObjectByType<VisualStyleCameraRig>(),
                Quality = UnityEngine.Object.FindAnyObjectByType<GraphicsQualityController>(),
                Lighting = UnityEngine.Object.FindAnyObjectByType<LightingPresetSwitcher>(),
                Hero = GameObject.Find("Hero_Placeholder")?.transform,
                Enemy = GameObject.Find("Enemy_Placeholder")?.transform
            };
            if (handles.Camera == null || handles.Quality == null || handles.Lighting == null || handles.Rig == null)
                throw new InvalidOperationException("Avatar5: VisualStyle_Test scene is incomplete; rebuild it.");
            handles.Rig.Apply();
            handles.Quality.EnsureInitialised();
            return handles;
        }

        public static List<string> CaptureAll(string folder)
        {
            var files = new List<string>();
            var h = OpenScene();
            try
            {
                void Shot(string name, int width = 1920, int height = 1080)
                {
                    h.Rig.Apply();
                    var texture = VisualStyleRenderUtil.Render(h.Camera, width, height);
                    var path = Path.Combine(folder, name + ".jpg");
                    VisualStyleRenderUtil.SaveJpg(texture, path);
                    UnityEngine.Object.DestroyImmediate(texture);
                    files.Add(path);
                }

                h.Lighting.Apply(0);
                for (var i = 0; i < h.Quality.ProfileCount; i++)
                {
                    h.Quality.Apply(i);
                    Shot("01_quality_" + (i + 1) + "_" + h.Quality.CurrentProfile.displayName.ToLowerInvariant());
                }

                h.Quality.Apply(1);
                h.Quality.SetOutlineEnabled(false);
                Shot("02_medium_ink_off");
                h.Quality.SetOutlineEnabled(true);

                h.Quality.SetDebugView(InkOutlineDebugView.LineTypes);
                Shot("03_debug_line_types");
                h.Quality.SetDebugView(InkOutlineDebugView.LinesOnly);
                Shot("04_debug_lines_only");
                h.Quality.SetDebugView(InkOutlineDebugView.Off);

                Shot("05_medium_21x9", 2520, 1080);

                for (var p = 1; p < h.Lighting.PresetCount; p++)
                {
                    h.Lighting.Apply(p);
                    Shot("06_lighting_" + p + "_" + h.Lighting.CurrentName.Replace(" ", "_").ToLowerInvariant());
                }
                h.Lighting.Apply(0);

                // Close-up on the hero (visible height ≈ 6 m) for face/clothing/equipment lines.
                var focus = h.Rig.FocusPoint;
                var size = h.Rig.OrthographicSize;
                h.Rig.FocusPoint = h.Hero.position + Vector3.up * 1f;
                h.Rig.OrthographicSize = 3f;
                Shot("07_closeup_screen_space");

                // Inverted-hull evaluation on hero and enemy (added temporarily, not saved in the scene).
                var hull = AssetDatabase.LoadAssetAtPath<Material>(ToonMaterialLibrary.PathFor("Toon_Hull_Outline"));
                var touched = new List<(MeshRenderer renderer, Material[] materials)>();
                foreach (var root in new[] { h.Hero, h.Enemy })
                foreach (var r in root.GetComponentsInChildren<MeshRenderer>())
                {
                    touched.Add((r, r.sharedMaterials));
                    r.sharedMaterials = r.sharedMaterials.Concat(new[] { hull }).ToArray();
                }
                h.Quality.SetOutlineEnabled(false);
                Shot("08_closeup_hull_only");
                h.Quality.SetOutlineEnabled(true);
                Shot("09_closeup_hull_plus_screen_space");
                foreach (var (renderer, materials) in touched) renderer.sharedMaterials = materials;

                h.Rig.FocusPoint = focus;
                h.Rig.OrthographicSize = size;
            }
            finally
            {
                h.Quality.Shutdown();
            }

            Debug.Log("Avatar5: captured " + files.Count + " screenshots to " + folder);
            return files;
        }
    }
}
