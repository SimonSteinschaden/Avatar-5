using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Avatar5.Rendering
{
    /// <summary>
    /// One graphics quality level for the Avatar5 comic look (Low / Medium / High).
    /// Data only; <see cref="GraphicsQualityController"/> applies it at runtime without modifying
    /// project assets (URP pipeline asset, renderer data or materials stay untouched).
    /// </summary>
    [CreateAssetMenu(menuName = "Avatar5/Rendering/Toon Quality Profile", fileName = "ToonQuality_")]
    public sealed class ToonQualityProfile : ScriptableObject
    {
        [Header("Identity")]
        public string displayName = "Medium";

        [Header("Ink lines")]
        public bool outlineEnabled = true;
        [Range(0.5f, 6f)] public float silhouetteThicknessPx = 3f;
        [Range(0.5f, 6f)] public float innerThicknessPx = 1.6f;
        public bool innerLines = true;
        [Range(0f, 1f)] public float innerIntensity = 0.85f;
        public bool highQualitySampling;
        [Min(0f)] public float innerFadeEndMetres = 60f;

        [Header("Toon surfaces")]
        [Tooltip("Low: two bands, no paint wobble/specular/rim (global keyword A5_TOON_SIMPLE).")]
        public bool simplifiedToon;

        [Header("Camera")]
        public AntialiasingMode antialiasing = AntialiasingMode.FastApproximateAntialiasing;
        public AntialiasingQuality antialiasingQuality = AntialiasingQuality.Medium;
        public bool renderShadows = true;

        public void ApplyTo(InkOutlineVolume volume)
        {
            volume.enableInk.Override(outlineEnabled);
            volume.silhouetteThickness.Override(silhouetteThicknessPx);
            volume.innerThickness.Override(innerThicknessPx);
            volume.innerLines.Override(innerLines);
            volume.innerIntensity.Override(innerIntensity);
            volume.highQualitySampling.Override(highQualitySampling);
            volume.innerFadeEnd.Override(innerFadeEndMetres);
            volume.innerFadeStart.Override(innerFadeEndMetres * 0.6f);
        }
    }
}
