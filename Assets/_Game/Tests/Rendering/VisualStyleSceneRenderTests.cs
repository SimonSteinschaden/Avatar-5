using System.Linq;
using Avatar5.EditorTools.Rendering;
using Avatar5.Rendering;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Avatar5.Tests.Rendering
{
    /// <summary>
    /// Renders VisualStyle_Test offscreen on the real GPU (needs a graphics device, i.e. no -nographics)
    /// and checks the image: ink lines present, line types separable, aspect ratios, lighting presets,
    /// quality switching. Pixel thresholds are deliberately loose; they guard against "effect missing"
    /// or "image black", not against artistic tweaks.
    /// </summary>
    public sealed class VisualStyleSceneRenderTests
    {
        private const int Width = 1920;
        private const int Height = 1080;
        private VisualStyleScreenshotCapture.SceneHandles _scene;

        [OneTimeSetUp]
        public void OpenScene()
        {
            _scene = VisualStyleScreenshotCapture.OpenScene();
        }

        [OneTimeTearDown]
        public void CloseScene()
        {
            _scene?.Quality.Shutdown();
        }

        [SetUp]
        public void ResetState()
        {
            _scene.Quality.Apply(1);
            _scene.Quality.SetOutlineEnabled(true);
            _scene.Quality.SetDebugView(InkOutlineDebugView.Off);
            _scene.Lighting.Apply(0);
            _scene.Camera.ResetAspect();
            _scene.Rig.Apply();
        }

        private Texture2D Render(int width = Width, int height = Height)
        {
            return VisualStyleRenderUtil.Render(_scene.Camera, width, height);
        }

        private RectInt RectOf(Transform t, int width = Width, int height = Height)
        {
            _scene.Camera.aspect = width / (float)height;
            var rect = VisualStyleRenderUtil.ScreenRect(_scene.Camera, t, width, height);
            _scene.Camera.ResetAspect();
            return rect;
        }

        [Test]
        public void Scene_ContainsRequiredContent()
        {
            Assert.That(_scene.Hero, Is.Not.Null, "stylised human placeholder");
            Assert.That(_scene.Enemy, Is.Not.Null, "enemy placeholder");
            var all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            int CountPrefix(string prefix) => all.Count(t => t.name.StartsWith(prefix));
            Assert.That(CountPrefix("Rock_"), Is.GreaterThanOrEqualTo(5), "rocks");
            Assert.That(CountPrefix("StoneBlock_"), Is.GreaterThanOrEqualTo(5), "stone blocks");
            Assert.That(CountPrefix("Wood_"), Is.GreaterThanOrEqualTo(10), "wooden construction");
            Assert.That(CountPrefix("EdgeTest_"), Is.GreaterThanOrEqualTo(6), "geometric edge test shapes");
            var water = GameObject.Find("Water_River");
            Assert.That(water, Is.Not.Null, "water surface");
            Assert.That(water.GetComponent<MeshRenderer>().sharedMaterial.shader.name, Is.EqualTo(ToonMaterialLibrary.WaterShaderName));

            var sun = Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Single(l => l.type == LightType.Directional);
            Assert.That(sun.shadows, Is.Not.EqualTo(LightShadows.None), "directional light casts shadows");
            Assert.That(_scene.Camera.orthographic, Is.True, "isometric orthographic camera");
            Assert.That(_scene.Camera.transform.eulerAngles.x, Is.EqualTo(40f).Within(0.1f));
            Assert.That(_scene.Camera.transform.eulerAngles.y, Is.EqualTo(45f).Within(0.1f));
        }

        [Test]
        public void Scene_UsesOnlyAvatar5Shaders()
        {
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            foreach (var m in r.sharedMaterials)
                Assert.That(m != null && m.shader.name.StartsWith("Avatar5/"), Is.True, r.name + " uses " + (m != null ? m.shader.name : "null"));
        }

        [Test]
        public void ToonScene_RendersWithoutErrorShader()
        {
            var image = Render();
            var magenta = VisualStyleRenderUtil.Count(image, default, c => c.r > 230 && c.g < 30 && c.b > 230);
            var mean = VisualStyleRenderUtil.MeanLuminance(image, new RectInt(0, 0, Width, Height));
            Object.DestroyImmediate(image);
            Assert.That(magenta, Is.EqualTo(0), "error/fallback shader pixels");
            Assert.That(mean, Is.InRange(0.2f, 0.8f), "image neither black nor blown out");
        }

        [Test]
        public void InkLines_AppearOnCharactersAndScene()
        {
            var on = Render();
            _scene.Quality.SetOutlineEnabled(false);
            var off = Render();

            var whole = new RectInt(0, 0, Width, Height);
            var heroRect = RectOf(_scene.Hero);
            var enemyRect = RectOf(_scene.Enemy);
            var inkOn = VisualStyleRenderUtil.CountInk(on, whole);
            var inkOff = VisualStyleRenderUtil.CountInk(off, whole);
            var heroOn = VisualStyleRenderUtil.CountInk(on, heroRect);
            var heroOff = VisualStyleRenderUtil.CountInk(off, heroRect);
            var enemyOn = VisualStyleRenderUtil.CountInk(on, enemyRect);
            var enemyOff = VisualStyleRenderUtil.CountInk(off, enemyRect);
            Object.DestroyImmediate(on);
            Object.DestroyImmediate(off);

            TestContext.WriteLine($"ink pixels whole {inkOn} (off {inkOff}), hero {heroOn} (off {heroOff}), enemy {enemyOn} (off {enemyOff})");
            Assert.That(inkOn, Is.GreaterThan(inkOff + 8000), "scene-wide ink lines");
            Assert.That(heroOn, Is.GreaterThan(heroOff + 40), "hero outline");
            Assert.That(enemyOn, Is.GreaterThan(enemyOff + 40), "enemy outline");
            // Lines, not black fills: ink must stay a small fraction of the picture.
            Assert.That(inkOn / (float)(Width * Height), Is.LessThan(0.12f));
        }

        [Test]
        public void InnerAndOuterLines_AreDistinguishable()
        {
            var normal = Render();
            _scene.Quality.SetDebugView(InkOutlineDebugView.LineTypes);
            var debug = Render();

            bool Red(Color32 c) => c.r > 150 && c.g < 90 && c.b < 90;
            bool Cyan(Color32 c) => c.r < 110 && c.g > 150 && c.b > 170;
            var edgeTests = GameObject.Find("EdgeTests").transform;
            var rect = RectOf(edgeTests);

            var redGain = VisualStyleRenderUtil.Count(debug, default, Red) - VisualStyleRenderUtil.Count(normal, default, Red);
            var cyanGain = VisualStyleRenderUtil.Count(debug, default, Cyan) - VisualStyleRenderUtil.Count(normal, default, Cyan);
            var redLocal = VisualStyleRenderUtil.Count(debug, rect, Red) - VisualStyleRenderUtil.Count(normal, rect, Red);
            var cyanLocal = VisualStyleRenderUtil.Count(debug, rect, Cyan) - VisualStyleRenderUtil.Count(normal, rect, Cyan);
            Object.DestroyImmediate(normal);
            Object.DestroyImmediate(debug);

            TestContext.WriteLine($"silhouette(red) +{redGain}, inner(cyan) +{cyanGain}; edge-test shapes red +{redLocal}, cyan +{cyanLocal}");
            Assert.That(redGain, Is.GreaterThan(3000), "silhouette lines detected");
            Assert.That(cyanGain, Is.GreaterThan(3000), "inner lines detected");
            Assert.That(redLocal, Is.GreaterThan(150), "edge-test shapes have outer lines");
            Assert.That(cyanLocal, Is.GreaterThan(150), "edge-test shapes have inner lines");
        }

        [TestCase(1920, 1080)]
        [TestCase(2520, 1080)]
        public void Camera_FramesCharactersAndInk_AtAspect(int width, int height)
        {
            var image = Render(width, height);
            var heroRect = RectOf(_scene.Hero, width, height);
            var enemyRect = RectOf(_scene.Enemy, width, height);
            var heroInk = VisualStyleRenderUtil.CountInk(image, heroRect);
            Object.DestroyImmediate(image);

            foreach (var rect in new[] { heroRect, enemyRect })
            {
                Assert.That(rect.width, Is.GreaterThan(10), "character visible");
                Assert.That(rect.xMin, Is.GreaterThan(width * 0.02f));
                Assert.That(rect.xMax, Is.LessThan(width * 0.98f));
                Assert.That(rect.yMin, Is.GreaterThan(height * 0.02f));
                Assert.That(rect.yMax, Is.LessThan(height * 0.98f));
            }
            Assert.That(heroInk, Is.GreaterThan(40), "hero outline at " + width + "x" + height);
            // Orthographic size is vertical: both aspects show the same character size in pixels.
            Assert.That(heroRect.height, Is.EqualTo(RectOf(_scene.Hero).height).Within(2));
        }

        [Test]
        public void LightingPresets_KeepCharactersReadable()
        {
            for (var p = 0; p < _scene.Lighting.PresetCount; p++)
            {
                _scene.Lighting.Apply(p);
                var image = Render();
                var heroRect = RectOf(_scene.Hero);
                var heroLuma = VisualStyleRenderUtil.MeanLuminance(image, heroRect);
                var dark = VisualStyleRenderUtil.Count(image, heroRect, c => VisualStyleRenderUtil.Luminance(c) < 0.07f) / (float)(heroRect.width * heroRect.height);
                var sceneLuma = VisualStyleRenderUtil.MeanLuminance(image, new RectInt(0, 0, Width, Height));
                Object.DestroyImmediate(image);

                TestContext.WriteLine($"{_scene.Lighting.CurrentName}: hero luma {heroLuma:F3}, hero dark {dark:P1}, scene luma {sceneLuma:F3}");
                Assert.That(heroLuma, Is.InRange(0.15f, 0.9f), _scene.Lighting.CurrentName);
                Assert.That(dark, Is.LessThan(0.3f), _scene.Lighting.CurrentName + ": shading must not become black fill");
                Assert.That(sceneLuma, Is.GreaterThan(0.12f), _scene.Lighting.CurrentName);
            }
        }

        [Test]
        public void QualityProfiles_SwitchRenderingSettings()
        {
            var keyword = GlobalKeyword.Create(GraphicsQualityController.SimplifiedToonKeyword);
            var cameraData = _scene.Camera.GetUniversalAdditionalCameraData();

            _scene.Quality.Apply(0);
            Assert.That(_scene.Quality.CurrentProfile.displayName, Is.EqualTo("Low"));
            Assert.That(Shader.IsKeywordEnabled(keyword), Is.True);
            Assert.That(_scene.Quality.Overrides.innerLines.value, Is.False);
            Assert.That(cameraData.antialiasing, Is.EqualTo(AntialiasingMode.None));
            Assert.That(cameraData.renderShadows, Is.False);
            var low = Render();

            _scene.Quality.Apply(2);
            Assert.That(_scene.Quality.CurrentProfile.displayName, Is.EqualTo("High"));
            Assert.That(Shader.IsKeywordEnabled(keyword), Is.False);
            Assert.That(_scene.Quality.Overrides.highQualitySampling.value, Is.True);
            Assert.That(cameraData.antialiasing, Is.EqualTo(AntialiasingMode.SubpixelMorphologicalAntiAliasing));
            Assert.That(cameraData.renderShadows, Is.True);
            var high = Render();

            var whole = new RectInt(0, 0, Width, Height);
            var lowInk = VisualStyleRenderUtil.CountInk(low, whole);
            var highInk = VisualStyleRenderUtil.CountInk(high, whole);
            Object.DestroyImmediate(low);
            Object.DestroyImmediate(high);
            TestContext.WriteLine($"ink pixels low {lowInk}, high {highInk}");
            Assert.That(lowInk, Is.GreaterThan(5000), "Low still draws silhouettes");
            Assert.That(highInk, Is.GreaterThan(lowInk), "High adds inner lines");
        }

        [Test]
        public void QualityController_ShutdownRestoresCameraAndKeyword()
        {
            var cameraData = _scene.Camera.GetUniversalAdditionalCameraData();
            _scene.Quality.Shutdown();
            var authoredAa = cameraData.antialiasing;
            var authoredShadows = cameraData.renderShadows;

            _scene.Quality.Apply(0);
            _scene.Quality.Shutdown();

            Assert.That(cameraData.antialiasing, Is.EqualTo(authoredAa));
            Assert.That(cameraData.renderShadows, Is.EqualTo(authoredShadows));
            Assert.That(Shader.IsKeywordEnabled(GlobalKeyword.Create(GraphicsQualityController.SimplifiedToonKeyword)), Is.False);
            Assert.That(_scene.Quality.Overrides, Is.Null);
            _scene.Quality.EnsureInitialised();
        }
    }
}
