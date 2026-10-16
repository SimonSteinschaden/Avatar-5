using System.Collections.Generic;
using Avatar5.EditorTools.Rendering;
using Avatar5.Rendering;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Avatar5.Tests.Rendering
{
    /// <summary>
    /// Renders a sphere lit from the side with the toon shader (no ink, no noise, no highlights) and
    /// counts the distinct brightness levels: the cel shading must produce exactly "Light Bands" steps.
    /// </summary>
    public sealed class ToonBandTests
    {
        private Camera _camera;
        private Material _material;
        private Light _sun;

        [SetUp]
        public void CreateScene()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientProbe = LightingPresetSwitcher.TrilightProbe(Color.gray, Color.gray, Color.gray);

            _sun = new GameObject("Sun").AddComponent<Light>();
            _sun.type = LightType.Directional;
            _sun.shadows = LightShadows.None;
            _sun.color = Color.white;
            _sun.intensity = 1f;
            // Light travels along +X: the visible hemisphere covers N·L from -1 to 1, i.e. all bands.
            _sun.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

            _camera = new GameObject("Camera").AddComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = 0.6f;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Color.black;
            _camera.transform.position = new Vector3(0f, 0f, -5f);

            _material = new Material(Shader.Find(ToonMaterialLibrary.ToonShaderName));
            _material.SetColor("_BaseColor", new Color(1f, 0.55f, 0.2f));
            _material.SetColor("_ShadeColor", new Color(0.35f, 0.3f, 0.55f));
            _material.SetFloat("_BandSoftness", 0.001f);
            _material.SetFloat("_AmbientStrength", 0f);
            _material.SetFloat("_RimStrength", 0f);
            _material.SetColor("_SpecularColor", Color.black);
            _material.SetFloat("_PaintNoiseStrength", 0f);
            _material.SetFloat("_ColorVariation", 0f);

            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.GetComponent<MeshRenderer>().sharedMaterial = _material;
        }

        [TearDown]
        public void Cleanup()
        {
            Object.DestroyImmediate(_material);
        }

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void Sphere_ShowsConfiguredNumberOfBands(int bands)
        {
            _material.SetFloat("_BandCount", bands);
            var image = VisualStyleRenderUtil.Render(_camera, 256, 256);
            var levels = CountBrightnessLevels(image);
            Object.DestroyImmediate(image);
            Assert.That(levels, Is.EqualTo(bands));
        }

        [Test]
        public void ShadeSide_UsesShadeColourNotBlack()
        {
            _material.SetFloat("_BandCount", 3);
            var image = VisualStyleRenderUtil.Render(_camera, 256, 256);
            // Light comes from -X (travels +X), so the left half of the sphere is lit, the right half shaded.
            var shade = image.GetPixel(200, 128);
            var lit = image.GetPixel(56, 128);
            Object.DestroyImmediate(image);
            Assert.That(shade.maxColorComponent, Is.GreaterThan(0.15f), "shadow band keeps colour");
            Assert.That(shade.b / shade.r, Is.GreaterThan(lit.b / lit.r), "shade colour shifts hue toward violet instead of just darkening");
            Assert.That(lit.r, Is.GreaterThan(shade.r));
        }

        /// <summary>Groups object pixels by luminance (2% bins) and counts clusters holding ≥ 4% of them.</summary>
        private static int CountBrightnessLevels(Texture2D image)
        {
            var bins = new int[51];
            var total = 0;
            foreach (var c in image.GetPixels32())
            {
                var l = VisualStyleRenderUtil.Luminance(c);
                if (l < 0.02f) continue; // background
                bins[Mathf.Clamp(Mathf.RoundToInt(l * 50f), 0, 50)]++;
                total++;
            }

            var clusters = new List<int>();
            var inCluster = false;
            var clusterCount = 0;
            for (var i = 0; i < bins.Length; i++)
            {
                var significant = bins[i] >= total * 0.04f;
                if (significant)
                {
                    clusterCount += bins[i];
                    inCluster = true;
                }
                else if (inCluster)
                {
                    clusters.Add(clusterCount);
                    clusterCount = 0;
                    inCluster = false;
                }
            }
            if (inCluster) clusters.Add(clusterCount);
            return clusters.Count;
        }
    }
}
