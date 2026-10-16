using System.Linq;
using Avatar5.EditorTools.Rendering;
using Avatar5.Rendering;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Avatar5.Tests.Rendering
{
    /// <summary>Static checks of the P07a assets: shaders, materials, profiles, renderer setup, module boundaries.</summary>
    public sealed class RenderingAssetTests
    {
        private static readonly string[] ShaderNames =
        {
            ToonMaterialLibrary.ToonShaderName,
            ToonMaterialLibrary.WaterShaderName,
            ToonMaterialLibrary.HullShaderName,
            VisualStyleSceneBuilder.InkShaderName
        };

        [TestCaseSource(nameof(ShaderNames))]
        public void Shader_CompilesWithoutErrors(string shaderName)
        {
            var shader = Shader.Find(shaderName);
            Assert.That(shader, Is.Not.Null, shaderName);
            Assert.That(ShaderUtil.ShaderHasError(shader), Is.False, shaderName + " has compile errors");
            var errors = ShaderUtil.GetShaderMessages(shader).Where(m => m.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error).ToArray();
            Assert.That(errors, Is.Empty, string.Join("\n", errors.Select(e => e.message)));
            Assert.That(shader.isSupported, Is.True, shaderName + " unsupported on this device");
        }

        [TestCase("Fire", 3)]
        [TestCase("Water", 2)]
        [TestCase("Earth", 3)]
        [TestCase("Air", 3)]
        [TestCase("Neutral", 4)]
        public void PaletteFamily_HasRequiredToonMaterials(string family, int minimumCount)
        {
            var definitions = ToonMaterialLibrary.Definitions.Where(d => d.Family == family && d.Emission.maxColorComponent <= 0f).ToArray();
            Assert.That(definitions.Length, Is.GreaterThanOrEqualTo(minimumCount));
            foreach (var d in definitions)
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(ToonMaterialLibrary.PathFor(d.Name));
                Assert.That(material, Is.Not.Null, d.Name);
                Assert.That(material.shader.name, Is.EqualTo(ToonMaterialLibrary.ToonShaderName), d.Name);
                Assert.That(material.GetFloat("_BandCount"), Is.InRange(2f, 4f), d.Name);
                var stored = material.GetColor("_BaseColor");
                Assert.That(Mathf.Abs(stored.r - d.Base.r) + Mathf.Abs(stored.g - d.Base.g) + Mathf.Abs(stored.b - d.Base.b), Is.LessThan(0.01f), d.Name);
                Assert.That(material.enableInstancing, Is.True, d.Name);
            }
        }

        [Test]
        public void PaletteColours_MatchElementIdentity()
        {
            foreach (var d in ToonMaterialLibrary.Definitions)
            {
                Color.RGBToHSV(d.Base, out var h, out var s, out var v);
                switch (d.Family)
                {
                    case "Fire": // red .. yellow, saturated
                        Assert.That(h <= 0.17f || h >= 0.97f, d.Name + " hue " + h);
                        Assert.That(s, Is.GreaterThan(0.6f), d.Name);
                        break;
                    case "Water": // cyan .. blue
                        Assert.That(h, Is.InRange(0.45f, 0.65f), d.Name);
                        break;
                    case "Earth": // ochre/brown/sand: warm, muted
                        Assert.That(h, Is.InRange(0.05f, 0.14f), d.Name);
                        break;
                    case "Air": // bright, cool or white
                        Assert.That(v, Is.GreaterThan(0.6f), d.Name);
                        Assert.That(s < 0.1f || (h > 0.4f && h < 0.62f), d.Name + " hue " + h);
                        break;
                }
                // Shade colours tint the base but never crush it to black.
                Assert.That(d.Shade.maxColorComponent, Is.GreaterThan(0.4f), d.Name);
            }
        }

        [Test]
        public void QualityProfiles_LowMediumHigh_ScaleCost()
        {
            var profiles = VisualStyleSceneBuilder.ProfileNames
                .Select(n => AssetDatabase.LoadAssetAtPath<ToonQualityProfile>(VisualStyleSceneBuilder.SettingsFolder + "/ToonQuality_" + n + ".asset"))
                .ToArray();
            Assert.That(profiles, Has.All.Not.Null);
            var low = profiles[0];
            var medium = profiles[1];
            var high = profiles[2];

            Assert.That(low.simplifiedToon && !low.innerLines && !low.highQualitySampling, Is.True, "Low: silhouettes only, simplified toon");
            Assert.That(low.antialiasing, Is.EqualTo(AntialiasingMode.None));
            Assert.That(!medium.simplifiedToon && medium.innerLines && !medium.highQualitySampling, Is.True, "Medium: full toon, optimised lines");
            Assert.That(!high.simplifiedToon && high.innerLines && high.highQualitySampling, Is.True, "High: 8-tap lines");
            Assert.That(high.antialiasing, Is.EqualTo(AntialiasingMode.SubpixelMorphologicalAntiAliasing));
            foreach (var p in profiles)
                Assert.That(p.silhouetteThicknessPx, Is.InRange(1f, 3.5f), p.displayName + ": brief asks for 1-3 px at 1080p");
        }

        [Test]
        public void PcRenderer_HasActiveInkFeature_OnRenderGraph()
        {
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(VisualStyleSceneBuilder.RendererDataPath);
            Assert.That(data, Is.Not.Null);
            var features = data.rendererFeatures.OfType<InkOutlineRendererFeature>().ToArray();
            Assert.That(features.Length, Is.EqualTo(1));
            Assert.That(features[0].isActive, Is.True);
            Assert.That(features[0].OutlineShader, Is.EqualTo(Shader.Find(VisualStyleSceneBuilder.InkShaderName)));
#if URP_COMPATIBILITY_MODE
            Assert.Fail("URP Compatibility Mode is enabled; the ink pass is implemented for Render Graph only.");
#endif
        }

        [Test]
        public void InkVolumeDefaults_AreOffSoOtherScenesStayUnchanged()
        {
            var defaults = ScriptableObject.CreateInstance<InkOutlineVolume>();
            try
            {
                Assert.That(defaults.enableInk.value, Is.False);
                Assert.That(defaults.IsActive(), Is.False);
                Assert.That(defaults.color.value, Is.EqualTo(Color.black));
            }
            finally
            {
                Object.DestroyImmediate(defaults);
            }
        }

        [Test]
        public void OtherScenes_DoNotEnableInkLines()
        {
            // P01 arena and starter scenes must render exactly as before unless they opt in.
            var scenes = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/_Game/Scenes" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p != VisualStyleSceneBuilder.ScenePath)
                .ToArray();
            Assert.That(scenes, Is.Not.Empty);
            foreach (var path in scenes)
            {
                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                foreach (var volume in Object.FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    var profile = volume.sharedProfile;
                    if (profile != null && profile.TryGet<InkOutlineVolume>(out var ink))
                        Assert.That(ink.enableInk.overrideState && ink.enableInk.value, Is.False, path);
                }
            }
        }

        [Test]
        public void RenderingAssembly_HasNoGameplayDependencies()
        {
            var references = typeof(InkOutlineRendererFeature).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToArray();
            foreach (var forbidden in new[] { "Avatar5.Gameplay", "Avatar5.Contracts", "Avatar5.Player", "Avatar5.Input", "Avatar5.Networking", "Avatar5.World" })
                Assert.That(references, Has.No.Member(forbidden));
        }

        [Test]
        public void InkSampleRadius_ScalesWithResolution()
        {
            Assert.That(InkOutlineMath.SampleRadiusPixels(3f, 1f), Is.EqualTo(1.5f).Within(1e-5f));
            Assert.That(InkOutlineMath.SampleRadiusPixels(3f, 2f), Is.EqualTo(3f).Within(1e-5f), "1080p -> 2160p doubles the radius");
            Assert.That(InkOutlineMath.SampleRadiusPixels(0.5f, 0.5f), Is.EqualTo(0.5f), "never below half a pixel");
        }

        [Test]
        public void Benchmark_OnlyRunsWithCommandLineFlag()
        {
            Assert.That(VisualStyleBenchmark.ReadOutputPath(new[] { "Avatar5.exe" }), Is.Null);
            Assert.That(VisualStyleBenchmark.ReadOutputPath(new[] { "Avatar5.exe", "-a5benchmark", "out.csv" }), Is.EqualTo("out.csv"));
        }
    }
}
