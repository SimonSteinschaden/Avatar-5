using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Avatar5.Rendering
{
    [Serializable]
    public struct LightingPreset
    {
        public string name;
        public Color sunColor;
        [Min(0f)] public float sunIntensity;
        public Vector3 sunEuler;
        public Color ambientSky;
        public Color ambientEquator;
        public Color ambientGround;
        public Color background;
        [Range(0f, 1f)] public float pointLightScale;
    }

    /// <summary>
    /// Switches the visual-style test lighting between presets (day, golden hour, overcast, night) to
    /// check that toon materials stay readable. Changes scene lighting only; reverts on disable.
    /// </summary>
    public sealed class LightingPresetSwitcher : MonoBehaviour
    {
        [SerializeField] private Light sun;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Light[] pointLights = Array.Empty<Light>();
        [SerializeField] private LightingPreset[] presets = Array.Empty<LightingPreset>();

        private int _current = -1;
        private float[] _pointBaseIntensity = Array.Empty<float>();

        public int CurrentIndex => _current;
        public int PresetCount => presets.Length;
        public string CurrentName => _current >= 0 && _current < presets.Length ? presets[_current].name : "-";

        public void Configure(Light newSun, Camera newCamera, Light[] newPointLights, LightingPreset[] newPresets)
        {
            sun = newSun;
            targetCamera = newCamera;
            pointLights = newPointLights ?? Array.Empty<Light>();
            presets = newPresets ?? Array.Empty<LightingPreset>();
            _pointBaseIntensity = Array.Empty<float>();
        }

        private void CaptureBase()
        {
            if (_pointBaseIntensity.Length == pointLights.Length) return;
            _pointBaseIntensity = new float[pointLights.Length];
            for (var i = 0; i < pointLights.Length; i++)
                _pointBaseIntensity[i] = pointLights[i] != null ? pointLights[i].intensity : 0f;
        }

        public void Apply(int index)
        {
            if (presets.Length == 0) return;
            CaptureBase();
            _current = ((index % presets.Length) + presets.Length) % presets.Length;
            var p = presets[_current];

            if (sun != null)
            {
                sun.color = p.sunColor;
                sun.intensity = p.sunIntensity;
                sun.transform.rotation = Quaternion.Euler(p.sunEuler);
            }

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = p.ambientSky;
            RenderSettings.ambientEquatorColor = p.ambientEquator;
            RenderSettings.ambientGroundColor = p.ambientGround;
            RenderSettings.ambientProbe = TrilightProbe(p.ambientSky, p.ambientEquator, p.ambientGround);
            if (targetCamera != null) targetCamera.backgroundColor = p.background;

            for (var i = 0; i < pointLights.Length; i++)
                if (pointLights[i] != null) pointLights[i].intensity = _pointBaseIntensity[i] * p.pointLightScale;
        }

        /// <summary>
        /// SH approximation of a trilight gradient. Set explicitly so runtime preset changes reach shaders
        /// that sample SH (the toon shade term) without a lighting bake.
        /// </summary>
        public static SphericalHarmonicsL2 TrilightProbe(Color sky, Color equator, Color ground)
        {
            var sh = new SphericalHarmonicsL2();
            sh.AddAmbientLight(equator);
            sh.AddDirectionalLight(Vector3.up, sky - equator, 1f);
            sh.AddDirectionalLight(Vector3.down, ground - equator, 1f);
            return sh;
        }

        public void ApplyNext()
        {
            Apply(_current + 1);
        }

        private void OnEnable()
        {
            if (Application.isPlaying && presets.Length > 0) Apply(0);
        }

        private void OnDisable()
        {
            // Restore the authored preset (index 0 is what the scene was saved with).
            if (_current > 0) Apply(0);
        }

        public static LightingPreset[] DefaultPresets()
        {
            return new[]
            {
                new LightingPreset
                {
                    name = "Day", sunColor = new Color(1f, 0.96f, 0.88f), sunIntensity = 1.25f, sunEuler = new Vector3(50f, -35f, 0f),
                    ambientSky = new Color(0.62f, 0.72f, 0.86f), ambientEquator = new Color(0.58f, 0.6f, 0.62f), ambientGround = new Color(0.36f, 0.33f, 0.3f),
                    background = new Color(0.38f, 0.55f, 0.66f), pointLightScale = 0.6f
                },
                new LightingPreset
                {
                    name = "Golden Hour", sunColor = new Color(1f, 0.74f, 0.48f), sunIntensity = 1.15f, sunEuler = new Vector3(22f, -70f, 0f),
                    ambientSky = new Color(0.58f, 0.52f, 0.66f), ambientEquator = new Color(0.62f, 0.5f, 0.45f), ambientGround = new Color(0.3f, 0.24f, 0.22f),
                    background = new Color(0.62f, 0.45f, 0.42f), pointLightScale = 0.9f
                },
                new LightingPreset
                {
                    name = "Overcast", sunColor = new Color(0.86f, 0.9f, 0.95f), sunIntensity = 0.7f, sunEuler = new Vector3(65f, 10f, 0f),
                    ambientSky = new Color(0.7f, 0.74f, 0.78f), ambientEquator = new Color(0.62f, 0.64f, 0.66f), ambientGround = new Color(0.42f, 0.42f, 0.42f),
                    background = new Color(0.52f, 0.57f, 0.6f), pointLightScale = 0.7f
                },
                new LightingPreset
                {
                    name = "Night", sunColor = new Color(0.55f, 0.65f, 1f), sunIntensity = 0.45f, sunEuler = new Vector3(40f, 120f, 0f),
                    ambientSky = new Color(0.22f, 0.27f, 0.45f), ambientEquator = new Color(0.17f, 0.2f, 0.32f), ambientGround = new Color(0.1f, 0.1f, 0.16f),
                    background = new Color(0.08f, 0.1f, 0.2f), pointLightScale = 0.9f
                }
            };
        }
    }
}
