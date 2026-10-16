using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Avatar5.Rendering
{
    public enum InkOutlineDebugView
    {
        Off = 0,
        LinesOnly = 1,
        LineTypes = 2
    }

    /// <summary>
    /// Volume override for the Avatar5 ink lines. The renderer feature draws nothing unless a volume
    /// enables it, so scenes opt in (P01 arena and other scenes stay unchanged until they add a volume).
    /// Thickness values are pixels at 1080p and are scaled with the render height.
    /// </summary>
    [Serializable, VolumeComponentMenu("Avatar5/Ink Outline")]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    public sealed class InkOutlineVolume : VolumeComponent
    {
        [Tooltip("Turns the screen-space ink lines on.")]
        public BoolParameter enableInk = new BoolParameter(false);

        [Tooltip("Ink colour. Alpha scales the overall opacity.")]
        public ColorParameter color = new ColorParameter(Color.black, false, true, true);

        [Tooltip("Silhouette (outer) line thickness in pixels at 1080p.")]
        public ClampedFloatParameter silhouetteThickness = new ClampedFloatParameter(3f, 0.5f, 6f);

        [Tooltip("Inner line thickness in pixels at 1080p.")]
        public ClampedFloatParameter innerThickness = new ClampedFloatParameter(1.6f, 0.5f, 6f);

        [Tooltip("Silhouette opacity.")]
        public ClampedFloatParameter silhouetteIntensity = new ClampedFloatParameter(1f, 0f, 1f);

        [Tooltip("Inner line opacity.")]
        public ClampedFloatParameter innerIntensity = new ClampedFloatParameter(0.85f, 0f, 1f);

        [Tooltip("Draw inner lines at normal creases. Off = silhouettes only (cheaper).")]
        public BoolParameter innerLines = new BoolParameter(true);

        [Tooltip("Minimum eye-depth jump in metres that counts as a silhouette.")]
        public ClampedFloatParameter depthThreshold = new ClampedFloatParameter(0.18f, 0.01f, 3f);

        [Tooltip("Extra depth threshold per metre of camera distance (keeps far lines stable).")]
        public ClampedFloatParameter depthDistanceScale = new ClampedFloatParameter(0.006f, 0f, 0.1f);

        [Tooltip("Raises the depth threshold on surfaces seen at a grazing angle (anti false-line on slopes).")]
        public ClampedFloatParameter grazingCorrection = new ClampedFloatParameter(0.6f, 0f, 2f);

        [Tooltip("Minimum normal difference (1 - cos angle) that counts as an inner line. 0.3 ≈ 45°.")]
        public ClampedFloatParameter normalThreshold = new ClampedFloatParameter(0.35f, 0.02f, 1.5f);

        [Tooltip("Width of the anti-aliased ramp above the thresholds (higher = softer, less flicker).")]
        public ClampedFloatParameter edgeSoftness = new ClampedFloatParameter(0.5f, 0.05f, 2f);

        [Tooltip("Inner lines start fading out at this eye depth (m).")]
        public MinFloatParameter innerFadeStart = new MinFloatParameter(35f, 0f);

        [Tooltip("Inner lines are gone at this eye depth (m).")]
        public MinFloatParameter innerFadeEnd = new MinFloatParameter(60f, 0f);

        [Tooltip("8 taps (diagonals) instead of 4: rounder, more stable lines at a small GPU cost.")]
        public BoolParameter highQualitySampling = new BoolParameter(false);

        [Tooltip("Debug: lines only, or colour-coded line types (red = silhouette, cyan = inner).")]
        public EnumParameter<InkOutlineDebugView> debugView = new EnumParameter<InkOutlineDebugView>(InkOutlineDebugView.Off);

        public bool IsActive()
        {
            return enableInk.value && color.value.a > 0f && (silhouetteIntensity.value > 0f || innerIntensity.value > 0f);
        }
    }
}
