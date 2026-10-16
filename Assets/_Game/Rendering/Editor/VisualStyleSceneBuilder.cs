using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avatar5.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Avatar5.EditorTools.Rendering
{
    /// <summary>
    /// Generates every P07a asset idempotently: toon materials, procedural meshes, quality profiles,
    /// the ink-outline volume profile, the renderer-feature entry on PC_Renderer and the scene
    /// Assets/_Game/Scenes/VisualStyle_Test.unity. Existing assets are updated in place (stable GUIDs);
    /// the scene itself is regenerated, so manual edits there are lost on the next build.
    /// Menu: Avatar5/Setup/Build P07a Visual Style Test, or
    /// -executeMethod Avatar5.EditorTools.Rendering.VisualStyleSceneBuilder.BuildFromCommandLine
    /// </summary>
    public static class VisualStyleSceneBuilder
    {
        public const string ScenePath = "Assets/_Game/Scenes/VisualStyle_Test.unity";
        public const string SettingsFolder = "Assets/_Game/Rendering/Settings";
        public const string MeshFolder = "Assets/_Game/Art/VisualStyleTest/Meshes";
        public const string VolumeProfilePath = SettingsFolder + "/VisualStyle_InkVolume.asset";
        public const string RendererDataPath = "Assets/Settings/PC_Renderer.asset";
        public const string InkShaderName = "Hidden/Avatar5/InkOutline";
        public static readonly string[] ProfileNames = { "Low", "Medium", "High" };

        private static Dictionary<string, Material> _materials;
        private static Dictionary<string, Mesh> _meshes;

        [MenuItem("Avatar5/Setup/Build P07a Visual Style Test")]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildAll();
            EditorSceneManager.OpenScene(ScenePath);
        }

        public static void BuildFromCommandLine()
        {
            try
            {
                BuildAll();
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        public static void BuildAll()
        {
            Directory.CreateDirectory(SettingsFolder);
            Directory.CreateDirectory(MeshFolder);
            AssetDatabase.Refresh();

            _materials = ToonMaterialLibrary.CreateOrUpdateAll();
            _meshes = CreateOrUpdateMeshes();
            var profiles = CreateOrUpdateQualityProfiles();
            var volumeProfile = CreateOrUpdateVolumeProfile();
            InstallRendererFeature();
            AssetDatabase.SaveAssets();

            BuildScene(profiles, volumeProfile);
            AssetDatabase.SaveAssets();
            Debug.Log("Avatar5: P07a visual style test generated at " + ScenePath);
        }

        // ------------------------------------------------------------------------------------------
        // Assets
        // ------------------------------------------------------------------------------------------

        private static Dictionary<string, Mesh> CreateOrUpdateMeshes()
        {
            var meshes = new Dictionary<string, Mesh>();
            for (var i = 0; i < 6; i++)
            {
                var seed = 101 + i * 37;
                meshes["Rock" + i] = SaveMesh("A5_Rock_" + i, m => ToonProceduralMeshes.BuildRock(m, seed, i % 2 == 0 ? 1 : 0, 0.22f));
            }
            meshes["Crystal"] = SaveMesh("A5_Crystal", m => ToonProceduralMeshes.BuildCrystal(m, 6, 0.32f, 1.1f, 0.45f));
            meshes["Roof"] = SaveMesh("A5_Roof", m => ToonProceduralMeshes.BuildCone(m, 4, 1.9f, 1.3f, true));
            meshes["Horn"] = SaveMesh("A5_Horn", m => ToonProceduralMeshes.BuildCone(m, 10, 0.12f, 0.5f, false));
            meshes["Spike"] = SaveMesh("A5_Spike", m => ToonProceduralMeshes.BuildCone(m, 5, 0.18f, 0.7f, true));
            meshes["Torus"] = SaveMesh("A5_Torus", m => ToonProceduralMeshes.BuildTorus(m, 0.45f, 0.12f, 32, 12));
            meshes["Ring"] = SaveMesh("A5_Ring", m => ToonProceduralMeshes.BuildTorus(m, 0.6f, 0.05f, 40, 8));
            meshes["WaterGrid"] = SaveMesh("A5_WaterGrid", m => ToonProceduralMeshes.BuildGrid(m, 28f, 3.2f, 112, 16));
            return meshes;
        }

        private static Mesh SaveMesh(string name, Action<Mesh> build)
        {
            var path = MeshFolder + "/" + name + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            var isNew = mesh == null;
            if (isNew) mesh = new Mesh();
            build(mesh);
            mesh.name = name;
            if (isNew) AssetDatabase.CreateAsset(mesh, path);
            else EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static ToonQualityProfile[] CreateOrUpdateQualityProfiles()
        {
            var result = new ToonQualityProfile[ProfileNames.Length];
            for (var i = 0; i < ProfileNames.Length; i++)
            {
                var path = SettingsFolder + "/ToonQuality_" + ProfileNames[i] + ".asset";
                var profile = AssetDatabase.LoadAssetAtPath<ToonQualityProfile>(path);
                if (profile == null)
                {
                    profile = ScriptableObject.CreateInstance<ToonQualityProfile>();
                    AssetDatabase.CreateAsset(profile, path);
                }

                profile.displayName = ProfileNames[i];
                switch (i)
                {
                    case 0: // Low: silhouettes only (depth texture), 4 taps, simplified toon, no AA, no shadows.
                        profile.outlineEnabled = true;
                        profile.silhouetteThicknessPx = 2.6f;
                        profile.innerThicknessPx = 1f;
                        profile.innerLines = false;
                        profile.innerIntensity = 0f;
                        profile.highQualitySampling = false;
                        profile.innerFadeEndMetres = 30f;
                        profile.simplifiedToon = true;
                        profile.antialiasing = AntialiasingMode.None;
                        profile.antialiasingQuality = AntialiasingQuality.Low;
                        profile.renderShadows = false;
                        break;
                    case 1: // Medium: full cel shading, silhouettes + inner lines, 4 taps, FXAA.
                        profile.outlineEnabled = true;
                        profile.silhouetteThicknessPx = 3f;
                        profile.innerThicknessPx = 1.6f;
                        profile.innerLines = true;
                        profile.innerIntensity = 0.85f;
                        profile.highQualitySampling = false;
                        profile.innerFadeEndMetres = 60f;
                        profile.simplifiedToon = false;
                        profile.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
                        profile.antialiasingQuality = AntialiasingQuality.Medium;
                        profile.renderShadows = true;
                        break;
                    default: // High: 8-tap lines, slightly finer inner lines, SMAA High.
                        profile.outlineEnabled = true;
                        profile.silhouetteThicknessPx = 3.2f;
                        profile.innerThicknessPx = 1.5f;
                        profile.innerLines = true;
                        profile.innerIntensity = 0.9f;
                        profile.highQualitySampling = true;
                        profile.innerFadeEndMetres = 80f;
                        profile.simplifiedToon = false;
                        profile.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                        profile.antialiasingQuality = AntialiasingQuality.High;
                        profile.renderShadows = true;
                        break;
                }
                EditorUtility.SetDirty(profile);
                result[i] = profile;
            }
            return result;
        }

        private static VolumeProfile CreateOrUpdateVolumeProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, VolumeProfilePath);
            }

            if (!profile.TryGet<InkOutlineVolume>(out var ink))
            {
                ink = profile.Add<InkOutlineVolume>();
                ink.name = nameof(InkOutlineVolume);
                AssetDatabase.AddObjectToAsset(ink, profile);
            }

            // Scene defaults = Medium profile values; GraphicsQualityController overrides at runtime.
            ink.active = true;
            ink.enableInk.Override(true);
            ink.color.Override(new Color(0.02f, 0.02f, 0.035f, 1f));
            ink.silhouetteThickness.Override(3f);
            ink.innerThickness.Override(1.6f);
            ink.silhouetteIntensity.Override(1f);
            ink.innerIntensity.Override(0.85f);
            ink.innerLines.Override(true);
            ink.depthThreshold.Override(0.18f);
            ink.depthDistanceScale.Override(0.006f);
            ink.grazingCorrection.Override(0.6f);
            ink.normalThreshold.Override(0.35f);
            ink.edgeSoftness.Override(0.5f);
            ink.innerFadeStart.Override(36f);
            ink.innerFadeEnd.Override(60f);
            ink.highQualitySampling.Override(false);
            ink.debugView.Override(InkOutlineDebugView.Off);
            EditorUtility.SetDirty(ink);

            // The project-wide URP volume (SampleSceneProfile) applies Neutral tonemapping, vignette and
            // motion blur. They grey out the ink black and the flat cel colours, so the comic look turns them off.
            GetOrAdd<Tonemapping>(profile).mode.Override(TonemappingMode.None);
            GetOrAdd<Vignette>(profile).intensity.Override(0f);
            GetOrAdd<MotionBlur>(profile).intensity.Override(0f);
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet<T>(out var component)) return component;
            component = profile.Add<T>();
            component.name = typeof(T).Name;
            AssetDatabase.AddObjectToAsset(component, profile);
            EditorUtility.SetDirty(component);
            return component;
        }

        /// <summary>Adds the ink feature to the PC renderer once (idempotent) with a serialized shader reference.</summary>
        public static InkOutlineRendererFeature InstallRendererFeature()
        {
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererDataPath);
            if (data == null) throw new InvalidOperationException("Avatar5: renderer data not found at " + RendererDataPath);

            var shader = ToonMaterialLibrary.FindShader(InkShaderName);
            var feature = data.rendererFeatures.OfType<InkOutlineRendererFeature>().FirstOrDefault();
            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance<InkOutlineRendererFeature>();
                feature.name = "Avatar5InkOutline";
                feature.OutlineShader = shader;
                AssetDatabase.AddObjectToAsset(feature, data);
                data.rendererFeatures.Add(feature);
                AssetDatabase.SaveAssets();

                // Keep URP's feature map (local file ids) in sync, as its own "Add Renderer Feature" does.
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);
                var serialized = new SerializedObject(data);
                var map = serialized.FindProperty("m_RendererFeatureMap");
                map.arraySize = data.rendererFeatures.Count;
                map.GetArrayElementAtIndex(data.rendererFeatures.Count - 1).longValue = localId;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            else if (feature.OutlineShader != shader)
            {
                feature.OutlineShader = shader;
            }

            feature.SetActive(true);
            EditorUtility.SetDirty(feature);
            EditorUtility.SetDirty(data);
            return feature;
        }

        // ------------------------------------------------------------------------------------------
        // Scene
        // ------------------------------------------------------------------------------------------

        private static void BuildScene(ToonQualityProfile[] profiles, VolumeProfile volumeProfile)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var presets = LightingPresetSwitcher.DefaultPresets();
            var day = presets[0];

            // Lighting.
            var lightingRoot = new GameObject("Lighting");
            var sunObject = new GameObject("Sun");
            sunObject.transform.SetParent(lightingRoot.transform, false);
            var sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 1f;
            sun.shadowBias = 0.05f;
            sun.shadowNormalBias = 0.4f;
            sun.color = day.sunColor;
            sun.intensity = day.sunIntensity;
            sunObject.transform.rotation = Quaternion.Euler(day.sunEuler);
            RenderSettings.sun = sun;
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = day.ambientSky;
            RenderSettings.ambientEquatorColor = day.ambientEquator;
            RenderSettings.ambientGroundColor = day.ambientGround;
            RenderSettings.ambientProbe = LightingPresetSwitcher.TrilightProbe(day.ambientSky, day.ambientEquator, day.ambientGround);
            RenderSettings.fog = false;

            // Camera.
            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = day.background;
            var cameraData = cameraObject.AddComponent<UniversalAdditionalCameraData>();
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            cameraData.renderShadows = true;
            var rig = cameraObject.AddComponent<VisualStyleCameraRig>();
            rig.Configure(camera, new Vector3(0.5f, 0f, -2.5f), 40f, 45f, 30f, 8f);

            // Volume with the ink settings (scene opts in to ink lines).
            var volumeObject = new GameObject("Ink Outline Volume");
            var volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.sharedProfile = volumeProfile;

            // Environment, characters, props.
            var environment = new GameObject("Environment").transform;
            BuildTerrain(environment);
            BuildRiver(environment);
            BuildBridge(environment);
            BuildWatchPavilion(environment);
            BuildRuins(environment);
            BuildRocks(environment);
            BuildTrees(environment);
            var brazierLight = BuildElementProps(environment);
            BuildEdgeTests(environment);

            var characters = new GameObject("Characters").transform;
            BuildHero(characters, new Vector3(0.4f, 0f, -1.3f), 205f);
            BuildEnemy(characters, new Vector3(3.6f, 0f, -8.6f), 330f);

            // Runtime tools (presentation only).
            var tools = new GameObject("P07a Graphics Tools");
            var quality = tools.AddComponent<GraphicsQualityController>();
            quality.Configure(profiles, new[] { camera }, 1);
            var lighting = tools.AddComponent<LightingPresetSwitcher>();
            lighting.Configure(sun, camera, new[] { brazierLight }, presets);
            var overlay = tools.AddComponent<GraphicsDebugOverlay>();
            overlay.Configure(quality, lighting, rig);
            var benchmark = tools.AddComponent<VisualStyleBenchmark>();
            benchmark.Configure(quality, rig);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("Avatar5: failed to save " + ScenePath);
        }

        private static void BuildTerrain(Transform root)
        {
            var t = Group(root, "Terrain");
            // Banks: top surface at y = 0. River channel between z = -6 and z = -3.
            Part(t, "Ground_NorthBank", PrimitiveType.Cube, new Vector3(0f, -0.6f, 4f), new Vector3(26f, 1.2f, 14f), Vector3.zero, "Toon_Env_Grass");
            Part(t, "Ground_SouthBank", PrimitiveType.Cube, new Vector3(0f, -0.6f, -9.5f), new Vector3(26f, 1.2f, 7f), Vector3.zero, "Toon_Env_Grass");
            Part(t, "Ground_RiverBed", PrimitiveType.Cube, new Vector3(0f, -1.45f, -4.5f), new Vector3(26f, 0.5f, 3.2f), Vector3.zero, "Toon_Env_Riverbed");
            // Soil skirt below the grass so the plateau edge reads as layered earth.
            Part(t, "Ground_SoilSkirt_South", PrimitiveType.Cube, new Vector3(0f, -1.9f, -9.5f), new Vector3(26.4f, 1.4f, 7.4f), Vector3.zero, "Toon_Env_Soil");
            Part(t, "Ground_SoilSkirt_North", PrimitiveType.Cube, new Vector3(0f, -1.9f, 4f), new Vector3(26.4f, 1.4f, 14.4f), Vector3.zero, "Toon_Env_Soil");
            // Dirt path from the bridge to the pavilion.
            for (var i = 0; i < 6; i++)
                Part(t, "Path_Tile_" + i, PrimitiveType.Cube, new Vector3(2.2f + i * 0.55f, 0.02f, -2.2f + i * 1.1f), new Vector3(1.4f, 0.06f, 1.1f), new Vector3(0f, 25f + i * 4f, 0f), "Toon_Env_Soil");
            // Back cliff.
            Part(t, "Cliff_Back_A", PrimitiveType.Cube, new Vector3(-4f, 1.2f, 10.2f), new Vector3(10f, 3.6f, 1.6f), new Vector3(0f, 3f, 0f), "Toon_Env_Cliff");
            Part(t, "Cliff_Back_B", PrimitiveType.Cube, new Vector3(6f, 0.8f, 10.4f), new Vector3(8f, 2.8f, 1.4f), new Vector3(0f, -4f, 0f), "Toon_Env_Cliff");
            Part(t, "Cliff_Back_Ledge", PrimitiveType.Cube, new Vector3(-1f, 3.1f, 10.6f), new Vector3(5f, 0.4f, 1.8f), new Vector3(0f, 3f, 0f), "Toon_Env_Grass");
        }

        private static void BuildRiver(Transform root)
        {
            var t = Group(root, "River");
            var water = MeshPart(t, "Water_River", _meshes["WaterGrid"], new Vector3(0f, -0.32f, -4.5f), Vector3.one, Vector3.zero, "Toon_Water_Surface");
            water.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            // Stepping stones and a submerged rock to show the depth bands.
            MeshPart(t, "Rock_RiverStone_A", _meshes["Rock1"], new Vector3(-6f, -0.55f, -4.2f), new Vector3(0.7f, 0.5f, 0.6f), new Vector3(0f, 30f, 0f), "Toon_Neutral_Stone");
            MeshPart(t, "Rock_RiverStone_B", _meshes["Rock3"], new Vector3(-4.8f, -0.6f, -5f), new Vector3(0.6f, 0.45f, 0.55f), new Vector3(0f, 80f, 0f), "Toon_Neutral_Stone");
            MeshPart(t, "Rock_Submerged", _meshes["Rock5"], new Vector3(7f, -0.95f, -4.4f), new Vector3(0.9f, 0.5f, 0.8f), Vector3.zero, "Toon_Neutral_Stone");
        }

        private static void BuildBridge(Transform root)
        {
            var t = Group(root, "Wood_Bridge");
            t.localPosition = new Vector3(1.6f, 0f, -4.5f);
            const float length = 5.2f;
            Part(t, "Wood_Beam_L", PrimitiveType.Cube, new Vector3(-0.8f, -0.08f, 0f), new Vector3(0.22f, 0.26f, length), Vector3.zero, "Toon_Neutral_Wood");
            Part(t, "Wood_Beam_R", PrimitiveType.Cube, new Vector3(0.8f, -0.08f, 0f), new Vector3(0.22f, 0.26f, length), Vector3.zero, "Toon_Neutral_Wood");
            for (var i = 0; i < 9; i++)
            {
                var z = -length * 0.5f + 0.3f + i * 0.575f;
                Part(t, "Wood_Plank_" + i, PrimitiveType.Cube, new Vector3(0f, 0.1f, z), new Vector3(2f, 0.1f, 0.48f), new Vector3(0f, (i % 3 - 1) * 1.5f, 0f), "Toon_Neutral_Wood");
            }
            for (var side = -1; side <= 1; side += 2)
            {
                for (var i = 0; i < 3; i++)
                {
                    var z = -2f + i * 2f;
                    Part(t, "Wood_Post_" + side + "_" + i, PrimitiveType.Cube, new Vector3(side * 0.95f, 0.45f, z), new Vector3(0.16f, 1.1f, 0.16f), Vector3.zero, "Toon_Neutral_Wood");
                }
                Part(t, "Wood_Rail_" + side, PrimitiveType.Cube, new Vector3(side * 0.95f, 0.9f, 0f), new Vector3(0.1f, 0.1f, 4.4f), Vector3.zero, "Toon_Neutral_Wood");
                Part(t, "Rope_Wrap_" + side, PrimitiveType.Cylinder, new Vector3(side * 0.95f, 0.9f, 0f), new Vector3(0.14f, 0.06f, 0.14f), new Vector3(90f, 0f, 0f), "Toon_Earth_Ochre");
            }
        }

        private static void BuildWatchPavilion(Transform root)
        {
            var t = Group(root, "Wood_Pavilion");
            t.localPosition = new Vector3(6.5f, 0f, 3.2f);
            t.localRotation = Quaternion.Euler(0f, 12f, 0f);
            Part(t, "Wood_Floor", PrimitiveType.Cube, new Vector3(0f, 0.2f, 0f), new Vector3(3f, 0.2f, 3f), Vector3.zero, "Toon_Neutral_Wood");
            for (var x = -1; x <= 1; x += 2)
            for (var z = -1; z <= 1; z += 2)
                Part(t, "Wood_Pillar_" + x + "_" + z, PrimitiveType.Cylinder, new Vector3(x * 1.3f, 1.5f, z * 1.3f), new Vector3(0.22f, 1.25f, 0.22f), Vector3.zero, "Toon_Neutral_Wood");
            Part(t, "Wood_RoofBeam", PrimitiveType.Cube, new Vector3(0f, 2.75f, 0f), new Vector3(3.2f, 0.18f, 3.2f), Vector3.zero, "Toon_Neutral_Wood");
            MeshPart(t, "Roof_Cloth", _meshes["Roof"], new Vector3(0f, 2.84f, 0f), Vector3.one, new Vector3(0f, 0f, 0f), "Toon_Fire_Red");
            Part(t, "Banner_Pole", PrimitiveType.Cylinder, new Vector3(1.9f, 1.6f, -1.6f), new Vector3(0.08f, 1.6f, 0.08f), Vector3.zero, "Toon_Neutral_Wood");
            Part(t, "Banner_Cloth", PrimitiveType.Cube, new Vector3(2.25f, 2.5f, -1.6f), new Vector3(0.7f, 1f, 0.04f), new Vector3(0f, 0f, 4f), "Toon_Neutral_Cloth");
            Part(t, "Banner_Emblem", PrimitiveType.Sphere, new Vector3(2.25f, 2.55f, -1.63f), new Vector3(0.32f, 0.32f, 0.04f), Vector3.zero, "Toon_Fire_Yellow");
            Part(t, "Crate_A", PrimitiveType.Cube, new Vector3(-0.6f, 0.65f, 0.5f), new Vector3(0.7f, 0.7f, 0.7f), new Vector3(0f, 20f, 0f), "Toon_Neutral_Wood");
            Part(t, "Crate_B", PrimitiveType.Cube, new Vector3(-0.4f, 1.25f, 0.45f), new Vector3(0.5f, 0.5f, 0.5f), new Vector3(0f, 50f, 0f), "Toon_Earth_Brown");
            Part(t, "Barrel", PrimitiveType.Cylinder, new Vector3(0.5f, 0.75f, 0.8f), new Vector3(0.6f, 0.45f, 0.6f), Vector3.zero, "Toon_Neutral_Wood");
            Part(t, "Barrel_Hoop", PrimitiveType.Cylinder, new Vector3(0.5f, 0.75f, 0.8f), new Vector3(0.64f, 0.05f, 0.64f), Vector3.zero, "Toon_Neutral_Metal");
        }

        private static void BuildRuins(Transform root)
        {
            var t = Group(root, "Stone_Ruins");
            t.localPosition = new Vector3(-6.5f, 0f, -9.2f);
            t.localRotation = Quaternion.Euler(0f, -15f, 0f);
            var blocks = new[]
            {
                (new Vector3(-1.6f, 0.4f, 0f), new Vector3(1.2f, 0.8f, 0.8f), 2f),
                (new Vector3(-0.35f, 0.4f, 0f), new Vector3(1.2f, 0.8f, 0.8f), -1f),
                (new Vector3(0.9f, 0.4f, 0f), new Vector3(1.2f, 0.8f, 0.8f), 3f),
                (new Vector3(-1f, 1.2f, 0.02f), new Vector3(1.2f, 0.8f, 0.8f), -2f),
                (new Vector3(0.25f, 1.2f, -0.03f), new Vector3(1.2f, 0.8f, 0.8f), 4f),
                (new Vector3(-0.4f, 2f, 0f), new Vector3(1f, 0.8f, 0.8f), 7f)
            };
            for (var i = 0; i < blocks.Length; i++)
                Part(t, "StoneBlock_" + i, PrimitiveType.Cube, blocks[i].Item1, blocks[i].Item2, new Vector3(0f, blocks[i].Item3, 0f), i % 2 == 0 ? "Toon_Neutral_Stone" : "Toon_Earth_Sandstone");
            Part(t, "StoneBlock_Fallen", PrimitiveType.Cube, new Vector3(2.2f, 0.3f, 0.9f), new Vector3(1.1f, 0.6f, 0.75f), new Vector3(8f, 38f, 12f), "Toon_Neutral_Stone");
            Part(t, "Column_Base", PrimitiveType.Cube, new Vector3(3.4f, 0.2f, -0.6f), new Vector3(1f, 0.4f, 1f), Vector3.zero, "Toon_Earth_Sandstone");
            Part(t, "Column_Shaft", PrimitiveType.Cylinder, new Vector3(3.4f, 1.1f, -0.6f), new Vector3(0.62f, 0.7f, 0.62f), Vector3.zero, "Toon_Earth_Sandstone");
            Part(t, "Column_Broken_Top", PrimitiveType.Cylinder, new Vector3(3.45f, 1.95f, -0.6f), new Vector3(0.6f, 0.15f, 0.6f), new Vector3(9f, 0f, 6f), "Toon_Earth_Sandstone");
        }

        private static void BuildRocks(Transform root)
        {
            var t = Group(root, "Rocks");
            var rocks = new[]
            {
                (new Vector3(-9f, 0.35f, -2.2f), new Vector3(1.3f, 1f, 1.1f), 10f, 0),
                (new Vector3(-10.5f, 0.3f, 1.5f), new Vector3(0.9f, 0.7f, 1f), 70f, 2),
                (new Vector3(9.5f, 0.45f, -2.3f), new Vector3(1.6f, 1.2f, 1.3f), 140f, 4),
                (new Vector3(10.4f, 0.25f, -10f), new Vector3(0.9f, 0.7f, 0.8f), 200f, 1),
                (new Vector3(-2.5f, 0.3f, -11f), new Vector3(1.1f, 0.8f, 0.9f), 260f, 3),
                (new Vector3(-6.5f, 0.6f, 8.6f), new Vector3(2f, 1.7f, 1.6f), 300f, 4),
                (new Vector3(3.5f, 0.55f, 8.8f), new Vector3(1.8f, 1.5f, 1.4f), 20f, 0),
                (new Vector3(-4.4f, 0.22f, 0.8f), new Vector3(0.55f, 0.45f, 0.5f), 45f, 5),
                (new Vector3(5.6f, 0.2f, -11.4f), new Vector3(0.6f, 0.45f, 0.55f), 95f, 5)
            };
            for (var i = 0; i < rocks.Length; i++)
            {
                var r = rocks[i];
                MeshPart(t, "Rock_" + i, _meshes["Rock" + r.Item4], r.Item1, r.Item2, new Vector3(0f, r.Item3, 0f), i % 3 == 2 ? "Toon_Env_Cliff" : "Toon_Neutral_Stone");
            }
        }

        private static void BuildTrees(Transform root)
        {
            var t = Group(root, "Trees");
            var trees = new[] { new Vector3(-9.5f, 0f, 6f), new Vector3(0.5f, 0f, 7.5f), new Vector3(10.5f, 0f, 7f), new Vector3(-11f, 0f, -9f) };
            for (var i = 0; i < trees.Length; i++)
            {
                var tree = Group(t, "Tree_" + i);
                tree.localPosition = trees[i];
                var scale = 0.9f + i * 0.12f;
                tree.localScale = Vector3.one * scale;
                Part(tree, "Trunk", PrimitiveType.Cylinder, new Vector3(0f, 1f, 0f), new Vector3(0.38f, 1f, 0.38f), Vector3.zero, "Toon_Earth_Brown");
                Part(tree, "Canopy_Low", PrimitiveType.Sphere, new Vector3(0f, 2.3f, 0f), new Vector3(2.2f, 1.6f, 2.2f), Vector3.zero, "Toon_Env_Foliage");
                Part(tree, "Canopy_Mid", PrimitiveType.Sphere, new Vector3(0.3f, 3.1f, -0.2f), new Vector3(1.6f, 1.3f, 1.6f), Vector3.zero, "Toon_Env_Foliage");
                Part(tree, "Canopy_Top", PrimitiveType.Sphere, new Vector3(-0.15f, 3.7f, 0.15f), new Vector3(1f, 0.9f, 1f), Vector3.zero, "Toon_Env_Foliage");
            }
        }

        private static Light BuildElementProps(Transform root)
        {
            var t = Group(root, "Element_Props");

            // Fire: brazier with flames and a warm point light.
            var fire = Group(t, "Fire_Brazier");
            fire.localPosition = new Vector3(-2.4f, 0f, 1.6f);
            Part(fire, "Brazier_Foot", PrimitiveType.Cylinder, new Vector3(0f, 0.3f, 0f), new Vector3(0.3f, 0.3f, 0.3f), Vector3.zero, "Toon_Neutral_Metal");
            Part(fire, "Brazier_Bowl", PrimitiveType.Cylinder, new Vector3(0f, 0.68f, 0f), new Vector3(0.9f, 0.1f, 0.9f), Vector3.zero, "Toon_Neutral_Metal");
            MeshPart(fire, "Flame_Outer", _meshes["Spike"], new Vector3(0f, 0.75f, 0f), new Vector3(2.2f, 1.2f, 2.2f), Vector3.zero, "Toon_Fire_Orange");
            MeshPart(fire, "Flame_Inner", _meshes["Spike"], new Vector3(0.02f, 0.75f, 0f), new Vector3(1.3f, 0.95f, 1.3f), new Vector3(0f, 36f, 0f), "Toon_Fire_Flame");
            var lightObject = new GameObject("Brazier_Light");
            lightObject.transform.SetParent(fire, false);
            lightObject.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            var point = lightObject.AddComponent<Light>();
            point.type = LightType.Point;
            point.color = new Color(1f, 0.62f, 0.3f);
            point.range = 6f;
            point.intensity = 2.2f;
            point.shadows = LightShadows.None;

            // Water: crystal cluster on the riverbank.
            var water = Group(t, "Water_Crystals");
            water.localPosition = new Vector3(-8.6f, 0f, -1.4f);
            MeshPart(water, "Crystal_Big", _meshes["Crystal"], new Vector3(0f, 0.55f, 0f), Vector3.one, new Vector3(0f, 0f, 6f), "Toon_Water_Turquoise");
            MeshPart(water, "Crystal_Small_A", _meshes["Crystal"], new Vector3(0.45f, 0.3f, 0.2f), Vector3.one * 0.55f, new Vector3(0f, 30f, -22f), "Toon_Water_Blue");
            MeshPart(water, "Crystal_Small_B", _meshes["Crystal"], new Vector3(-0.35f, 0.25f, -0.25f), Vector3.one * 0.45f, new Vector3(14f, 70f, 18f), "Toon_Water_Blue");

            // Earth: monolith with ochre bands.
            var earth = Group(t, "Earth_Monolith");
            earth.localPosition = new Vector3(8.4f, 0f, -8.6f);
            earth.localRotation = Quaternion.Euler(0f, -20f, 0f);
            Part(earth, "Monolith", PrimitiveType.Cube, new Vector3(0f, 1.3f, 0f), new Vector3(1f, 2.6f, 0.7f), new Vector3(0f, 0f, 3f), "Toon_Earth_Sandstone");
            Part(earth, "Monolith_Band_A", PrimitiveType.Cube, new Vector3(0.02f, 1.0f, 0f), new Vector3(1.06f, 0.16f, 0.76f), new Vector3(0f, 0f, 3f), "Toon_Earth_Ochre");
            Part(earth, "Monolith_Band_B", PrimitiveType.Cube, new Vector3(0.06f, 1.9f, 0f), new Vector3(1.06f, 0.16f, 0.76f), new Vector3(0f, 0f, 3f), "Toon_Earth_Brown");
            Part(earth, "Monolith_Cap", PrimitiveType.Cube, new Vector3(0.1f, 2.7f, 0f), new Vector3(1.25f, 0.25f, 0.9f), new Vector3(0f, 0f, 3f), "Toon_Earth_Brown");

            // Air: white totem with floating rings.
            var air = Group(t, "Air_Totem");
            air.localPosition = new Vector3(9.4f, 0f, 1.2f);
            Part(air, "Totem_Base", PrimitiveType.Cylinder, new Vector3(0f, 0.15f, 0f), new Vector3(0.9f, 0.15f, 0.9f), Vector3.zero, "Toon_Neutral_Stone");
            Part(air, "Totem_Pillar", PrimitiveType.Cylinder, new Vector3(0f, 1.2f, 0f), new Vector3(0.35f, 1f, 0.35f), Vector3.zero, "Toon_Air_White");
            Part(air, "Totem_Orb", PrimitiveType.Sphere, new Vector3(0f, 2.45f, 0f), Vector3.one * 0.5f, Vector3.zero, "Toon_Air_Turquoise");
            MeshPart(air, "Totem_Ring_A", _meshes["Ring"], new Vector3(0f, 2.45f, 0f), Vector3.one, new Vector3(70f, 20f, 0f), "Toon_Air_LightBlue");
            MeshPart(air, "Totem_Ring_B", _meshes["Ring"], new Vector3(0f, 2.45f, 0f), Vector3.one * 0.8f, new Vector3(-50f, 70f, 0f), "Toon_Air_White");
            return point;
        }

        private static void BuildEdgeTests(Transform root)
        {
            var t = Group(root, "EdgeTests");
            t.localPosition = new Vector3(-6.8f, 0f, 3.2f);
            t.localRotation = Quaternion.Euler(0f, 10f, 0f);
            Part(t, "EdgeTest_Plinth", PrimitiveType.Cube, new Vector3(0f, 0.2f, 0f), new Vector3(4.2f, 0.4f, 3.2f), Vector3.zero, "Toon_Earth_Sandstone");
            Part(t, "EdgeTest_Cube", PrimitiveType.Cube, new Vector3(-1.4f, 0.9f, -0.7f), Vector3.one, new Vector3(0f, 30f, 0f), "Toon_Fire_Orange");
            Part(t, "EdgeTest_Sphere", PrimitiveType.Sphere, new Vector3(0f, 0.9f, -0.7f), Vector3.one, Vector3.zero, "Toon_Water_Blue");
            Part(t, "EdgeTest_Cylinder", PrimitiveType.Cylinder, new Vector3(1.4f, 0.9f, -0.7f), new Vector3(0.9f, 0.5f, 0.9f), Vector3.zero, "Toon_Air_LightBlue");
            Part(t, "EdgeTest_Step_1", PrimitiveType.Cube, new Vector3(-1.2f, 0.55f, 0.8f), new Vector3(1.2f, 0.3f, 1.2f), Vector3.zero, "Toon_Earth_Ochre");
            Part(t, "EdgeTest_Step_2", PrimitiveType.Cube, new Vector3(-1.2f, 0.85f, 0.8f), new Vector3(0.8f, 0.3f, 0.8f), Vector3.zero, "Toon_Earth_Ochre");
            Part(t, "EdgeTest_Step_3", PrimitiveType.Cube, new Vector3(-1.2f, 1.15f, 0.8f), new Vector3(0.4f, 0.3f, 0.4f), Vector3.zero, "Toon_Earth_Ochre");
            MeshPart(t, "EdgeTest_Torus", _meshes["Torus"], new Vector3(0.2f, 0.95f, 0.85f), Vector3.one, new Vector3(60f, 0f, 0f), "Toon_Neutral_Metal");
            MeshPart(t, "EdgeTest_Cone", _meshes["Roof"], new Vector3(1.45f, 0.4f, 0.85f), Vector3.one * 0.35f, new Vector3(0f, 45f, 0f), "Toon_Air_Turquoise");
        }

        private static void BuildHero(Transform root, Vector3 position, float yaw)
        {
            var hero = Group(root, "Hero_Placeholder");
            hero.localPosition = position;
            hero.localRotation = Quaternion.Euler(0f, yaw, 0f);

            // Legs and boots.
            Part(hero, "Leg_L", PrimitiveType.Capsule, new Vector3(-0.14f, 0.42f, 0f), new Vector3(0.2f, 0.32f, 0.2f), Vector3.zero, "Toon_Neutral_Cloth");
            Part(hero, "Leg_R", PrimitiveType.Capsule, new Vector3(0.14f, 0.42f, 0f), new Vector3(0.2f, 0.32f, 0.2f), Vector3.zero, "Toon_Neutral_Cloth");
            Part(hero, "Boot_L", PrimitiveType.Cube, new Vector3(-0.14f, 0.09f, 0.05f), new Vector3(0.2f, 0.18f, 0.32f), Vector3.zero, "Toon_Char_Leather");
            Part(hero, "Boot_R", PrimitiveType.Cube, new Vector3(0.14f, 0.09f, 0.05f), new Vector3(0.2f, 0.18f, 0.32f), Vector3.zero, "Toon_Char_Leather");
            // Robe, belt and cape.
            Part(hero, "Robe_Lower", PrimitiveType.Cylinder, new Vector3(0f, 0.72f, 0f), new Vector3(0.62f, 0.2f, 0.5f), Vector3.zero, "Toon_Fire_Orange");
            Part(hero, "Torso", PrimitiveType.Capsule, new Vector3(0f, 1.12f, 0f), new Vector3(0.56f, 0.38f, 0.42f), Vector3.zero, "Toon_Fire_Orange");
            Part(hero, "Robe_Trim", PrimitiveType.Cylinder, new Vector3(0f, 0.53f, 0f), new Vector3(0.64f, 0.025f, 0.52f), Vector3.zero, "Toon_Fire_Yellow");
            Part(hero, "Belt", PrimitiveType.Cylinder, new Vector3(0f, 0.95f, 0f), new Vector3(0.58f, 0.05f, 0.45f), Vector3.zero, "Toon_Char_Leather");
            Part(hero, "Belt_Buckle", PrimitiveType.Cube, new Vector3(0f, 0.95f, 0.22f), new Vector3(0.12f, 0.1f, 0.04f), Vector3.zero, "Toon_Neutral_Metal");
            Part(hero, "Cape", PrimitiveType.Cube, new Vector3(0f, 1.0f, -0.25f), new Vector3(0.62f, 0.95f, 0.05f), new Vector3(-8f, 0f, 0f), "Toon_Fire_Red");
            Part(hero, "Shoulder_L", PrimitiveType.Sphere, new Vector3(-0.32f, 1.4f, 0f), new Vector3(0.26f, 0.2f, 0.26f), Vector3.zero, "Toon_Neutral_Metal");
            Part(hero, "Shoulder_R", PrimitiveType.Sphere, new Vector3(0.32f, 1.4f, 0f), new Vector3(0.26f, 0.2f, 0.26f), Vector3.zero, "Toon_Neutral_Metal");
            // Arms.
            Part(hero, "Arm_L", PrimitiveType.Capsule, new Vector3(-0.38f, 1.12f, 0.02f), new Vector3(0.15f, 0.26f, 0.15f), new Vector3(0f, 0f, -12f), "Toon_Fire_Orange");
            Part(hero, "Arm_R", PrimitiveType.Capsule, new Vector3(0.4f, 1.12f, 0.1f), new Vector3(0.15f, 0.26f, 0.15f), new Vector3(-25f, 0f, 10f), "Toon_Fire_Orange");
            Part(hero, "Hand_L", PrimitiveType.Sphere, new Vector3(-0.44f, 0.86f, 0.02f), Vector3.one * 0.12f, Vector3.zero, "Toon_Char_Skin");
            Part(hero, "Hand_R", PrimitiveType.Sphere, new Vector3(0.46f, 0.9f, 0.24f), Vector3.one * 0.12f, Vector3.zero, "Toon_Char_Skin");
            // Head: face, hair, eyes, nose.
            Part(hero, "Neck", PrimitiveType.Cylinder, new Vector3(0f, 1.5f, 0f), new Vector3(0.14f, 0.06f, 0.14f), Vector3.zero, "Toon_Char_Skin");
            Part(hero, "Head", PrimitiveType.Sphere, new Vector3(0f, 1.73f, 0f), new Vector3(0.42f, 0.44f, 0.4f), Vector3.zero, "Toon_Char_Skin");
            Part(hero, "Hair", PrimitiveType.Sphere, new Vector3(0f, 1.82f, -0.05f), new Vector3(0.45f, 0.36f, 0.42f), new Vector3(-12f, 0f, 0f), "Toon_Char_Hair");
            Part(hero, "Hair_Tail", PrimitiveType.Capsule, new Vector3(0f, 1.6f, -0.22f), new Vector3(0.14f, 0.2f, 0.14f), new Vector3(25f, 0f, 0f), "Toon_Char_Hair");
            Part(hero, "Eye_L", PrimitiveType.Sphere, new Vector3(-0.08f, 1.74f, 0.185f), new Vector3(0.06f, 0.08f, 0.04f), Vector3.zero, "Toon_Char_Eye");
            Part(hero, "Eye_R", PrimitiveType.Sphere, new Vector3(0.08f, 1.74f, 0.185f), new Vector3(0.06f, 0.08f, 0.04f), Vector3.zero, "Toon_Char_Eye");
            Part(hero, "Nose", PrimitiveType.Cube, new Vector3(0f, 1.69f, 0.2f), new Vector3(0.05f, 0.07f, 0.05f), new Vector3(10f, 0f, 0f), "Toon_Char_Skin");
            Part(hero, "Headband", PrimitiveType.Cylinder, new Vector3(0f, 1.83f, 0f), new Vector3(0.44f, 0.025f, 0.42f), new Vector3(-8f, 0f, 0f), "Toon_Fire_Red");
            // Equipment: staff with a fire orb.
            Part(hero, "Staff", PrimitiveType.Cylinder, new Vector3(0.48f, 1.05f, 0.26f), new Vector3(0.06f, 0.85f, 0.06f), new Vector3(8f, 0f, -4f), "Toon_Neutral_Wood");
            Part(hero, "Staff_Cap", PrimitiveType.Cylinder, new Vector3(0.53f, 1.9f, 0.37f), new Vector3(0.12f, 0.05f, 0.12f), new Vector3(8f, 0f, -4f), "Toon_Neutral_Metal");
            Part(hero, "Staff_Orb", PrimitiveType.Sphere, new Vector3(0.54f, 2.03f, 0.39f), Vector3.one * 0.2f, Vector3.zero, "Toon_Fire_Flame");
        }

        private static void BuildEnemy(Transform root, Vector3 position, float yaw)
        {
            var enemy = Group(root, "Enemy_Placeholder");
            enemy.localPosition = position;
            enemy.localRotation = Quaternion.Euler(0f, yaw, 0f);

            // Hunched rock golem: chunky body, small head, long arms, bone horns, glowing eyes, crystal spikes.
            Part(enemy, "Leg_L", PrimitiveType.Cylinder, new Vector3(-0.32f, 0.3f, 0f), new Vector3(0.36f, 0.3f, 0.36f), Vector3.zero, "Toon_Enemy_Hide");
            Part(enemy, "Leg_R", PrimitiveType.Cylinder, new Vector3(0.32f, 0.3f, 0f), new Vector3(0.36f, 0.3f, 0.36f), Vector3.zero, "Toon_Enemy_Hide");
            MeshPart(enemy, "Body", _meshes["Rock0"], new Vector3(0f, 1.1f, -0.05f), new Vector3(0.95f, 0.75f, 0.75f), new Vector3(15f, 0f, 0f), "Toon_Enemy_Hide");
            MeshPart(enemy, "Head", _meshes["Rock2"], new Vector3(0f, 1.6f, 0.42f), new Vector3(0.38f, 0.32f, 0.36f), new Vector3(0f, 20f, 0f), "Toon_Enemy_Hide");
            Part(enemy, "Jaw", PrimitiveType.Cube, new Vector3(0f, 1.42f, 0.55f), new Vector3(0.36f, 0.12f, 0.2f), new Vector3(10f, 0f, 0f), "Toon_Enemy_Hide");
            Part(enemy, "Tusk_L", PrimitiveType.Capsule, new Vector3(-0.12f, 1.47f, 0.66f), new Vector3(0.05f, 0.08f, 0.05f), new Vector3(-20f, 0f, 0f), "Toon_Enemy_Bone");
            Part(enemy, "Tusk_R", PrimitiveType.Capsule, new Vector3(0.12f, 1.47f, 0.66f), new Vector3(0.05f, 0.08f, 0.05f), new Vector3(-20f, 0f, 0f), "Toon_Enemy_Bone");
            Part(enemy, "Eye_L", PrimitiveType.Sphere, new Vector3(-0.11f, 1.64f, 0.7f), Vector3.one * 0.08f, Vector3.zero, "Toon_Enemy_Glow");
            Part(enemy, "Eye_R", PrimitiveType.Sphere, new Vector3(0.11f, 1.64f, 0.7f), Vector3.one * 0.08f, Vector3.zero, "Toon_Enemy_Glow");
            MeshPart(enemy, "Horn_L", _meshes["Horn"], new Vector3(-0.22f, 1.78f, 0.38f), Vector3.one * 1.3f, new Vector3(-20f, 0f, 35f), "Toon_Enemy_Bone");
            MeshPart(enemy, "Horn_R", _meshes["Horn"], new Vector3(0.22f, 1.78f, 0.38f), Vector3.one * 1.3f, new Vector3(-20f, 0f, -35f), "Toon_Enemy_Bone");
            MeshPart(enemy, "Arm_L", _meshes["Rock4"], new Vector3(-0.78f, 0.95f, 0.25f), new Vector3(0.32f, 0.6f, 0.32f), new Vector3(20f, 0f, 12f), "Toon_Enemy_Hide");
            MeshPart(enemy, "Arm_R", _meshes["Rock4"], new Vector3(0.78f, 0.95f, 0.25f), new Vector3(0.32f, 0.6f, 0.32f), new Vector3(20f, 0f, -12f), "Toon_Enemy_Hide");
            MeshPart(enemy, "Fist_L", _meshes["Rock1"], new Vector3(-0.86f, 0.42f, 0.42f), Vector3.one * 0.3f, Vector3.zero, "Toon_Neutral_Stone");
            MeshPart(enemy, "Fist_R", _meshes["Rock3"], new Vector3(0.86f, 0.42f, 0.42f), Vector3.one * 0.3f, Vector3.zero, "Toon_Neutral_Stone");
            MeshPart(enemy, "Spike_A", _meshes["Crystal"], new Vector3(-0.25f, 1.6f, -0.35f), Vector3.one * 0.5f, new Vector3(-35f, 0f, 20f), "Toon_Fire_Red");
            MeshPart(enemy, "Spike_B", _meshes["Crystal"], new Vector3(0.2f, 1.7f, -0.4f), Vector3.one * 0.6f, new Vector3(-30f, 0f, -15f), "Toon_Fire_Red");
            MeshPart(enemy, "Spike_C", _meshes["Crystal"], new Vector3(0f, 1.35f, -0.6f), Vector3.one * 0.45f, new Vector3(-60f, 0f, 0f), "Toon_Fire_Orange");
            Part(enemy, "Loincloth", PrimitiveType.Cube, new Vector3(0f, 0.62f, 0.32f), new Vector3(0.5f, 0.4f, 0.05f), new Vector3(8f, 0f, 0f), "Toon_Neutral_Cloth");
        }

        // ------------------------------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------------------------------

        private static Transform Group(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static GameObject Part(Transform parent, string name, PrimitiveType type, Vector3 localPosition, Vector3 localScale, Vector3 localEuler, string material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            // Visual-only scene: physics colliders are not needed.
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            Place(go, parent, localPosition, localScale, localEuler, material);
            return go;
        }

        private static GameObject MeshPart(Transform parent, string name, Mesh mesh, Vector3 localPosition, Vector3 localScale, Vector3 localEuler, string material)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            Place(go, parent, localPosition, localScale, localEuler, material);
            return go;
        }

        private static void Place(GameObject go, Transform parent, Vector3 localPosition, Vector3 localScale, Vector3 localEuler, string material)
        {
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.Euler(localEuler);
            go.transform.localScale = localScale;
            if (!_materials.TryGetValue(material, out var mat))
                throw new InvalidOperationException("Avatar5: unknown material " + material);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }
    }
}
