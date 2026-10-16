using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Avatar5.Rendering
{
    /// <summary>
    /// Screen-space black ink lines (silhouettes + inner creases) for URP 17 Render Graph.
    /// Parameters come from the <see cref="InkOutlineVolume"/> override of the camera's volume stack.
    /// Presentation only: the feature reads depth/normals and writes the camera colour, nothing else.
    /// </summary>
    [DisallowMultipleRendererFeature("Avatar5 Ink Outline")]
    public sealed class InkOutlineRendererFeature : ScriptableRendererFeature
    {
        public const string ProfilerTag = "Avatar5 Ink Outline";

        /// <summary>Number of camera renders the ink pass was enqueued for (diagnostics and tests).</summary>
        public static int EnqueuedFrames { get; private set; }
        public static bool DiagnosticsLogging;

        [SerializeField] private Shader outlineShader;
        [SerializeField] private RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
        [Tooltip("Draw in Scene view cameras as well.")]
        [SerializeField] private bool drawInSceneView = true;

        private Material _material;
        private InkOutlinePass _pass;

        public Shader OutlineShader
        {
            get => outlineShader;
            set => outlineShader = value;
        }

        public override void Create()
        {
            if (outlineShader == null) outlineShader = Shader.Find("Hidden/Avatar5/InkOutline");
            _pass = new InkOutlinePass { renderPassEvent = renderPassEvent };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var cameraType = renderingData.cameraData.cameraType;
            if (cameraType == CameraType.Preview || cameraType == CameraType.Reflection) return;
            if (cameraType == CameraType.SceneView && !drawInSceneView) return;
            if (outlineShader == null) return;

            var settings = VolumeManager.instance.stack?.GetComponent<InkOutlineVolume>();
            if (DiagnosticsLogging) Debug.Log("Avatar5 ink: camera=" + renderingData.cameraData.camera.name + " settings=" + (settings != null) + " active=" + (settings != null && settings.IsActive()) + " shader=" + outlineShader);
            if (settings == null || !settings.IsActive()) return;

            if (_material == null) _material = CoreUtils.CreateEngineMaterial(outlineShader);

            _pass.renderPassEvent = renderPassEvent;
            _pass.Setup(_material, settings);
            // Normals (and the per-material category in their alpha) are only requested for inner lines.
            // Silhouettes-only (Low profile) runs on the depth texture; categories are then read from
            // the normals texture only if another feature (e.g. SSAO) produced it anyway.
            var input = ScriptableRenderPassInput.Depth;
            if (settings.innerLines.value) input |= ScriptableRenderPassInput.Normal;
            _pass.ConfigureInput(input);
            renderer.EnqueuePass(_pass);
            EnqueuedFrames++;
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(_material);
            _material = null;
        }
    }

    internal sealed class InkOutlinePass : ScriptableRenderPass
    {
        private static readonly int OutlineColorId = Shader.PropertyToID("_A5_OutlineColor");
        private static readonly int TargetSizeId = Shader.PropertyToID("_A5_TargetSize");
        private static readonly int LineParamsId = Shader.PropertyToID("_A5_LineParams");
        private static readonly int DepthParamsId = Shader.PropertyToID("_A5_DepthParams");
        private static readonly int NormalParamsId = Shader.PropertyToID("_A5_NormalParams");
        private static readonly int CameraParamsId = Shader.PropertyToID("_A5_CameraParams");
        private static readonly int CameraForwardId = Shader.PropertyToID("_A5_CameraForward");
        private static readonly int DebugViewId = Shader.PropertyToID("_A5_DebugView");
        private static readonly int BlitScaleBiasId = Shader.PropertyToID("_BlitScaleBias");

        private const string InnerLinesKeyword = "_A5_INNER_LINES";
        private const string HighQualityKeyword = "_A5_HQ_SAMPLING";

        private Material _material;
        private InkOutlineVolume _settings;

        private sealed class PassData
        {
            public Material Material;
        }

        public InkOutlinePass()
        {
            profilingSampler = new ProfilingSampler(InkOutlineRendererFeature.ProfilerTag);
        }

        public void Setup(Material material, InkOutlineVolume settings)
        {
            _material = material;
            _settings = settings;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resourceData = frameData.Get<UniversalResourceData>();
            var cameraData = frameData.Get<UniversalCameraData>();
            if (_material == null || _settings == null) return;
            if (!resourceData.cameraDepthTexture.IsValid()) return;

            var innerLines = _settings.innerLines.value && resourceData.cameraNormalsTexture.IsValid();
            UpdateMaterial(cameraData, innerLines);

            using (var builder = renderGraph.AddRasterRenderPass<PassData>(InkOutlineRendererFeature.ProfilerTag, out var passData, profilingSampler))
            {
                passData.Material = _material;
                builder.UseTexture(resourceData.cameraDepthTexture);
                if (resourceData.cameraNormalsTexture.IsValid()) builder.UseTexture(resourceData.cameraNormalsTexture);
                builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.ReadWrite);
                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    data.Material.SetVector(BlitScaleBiasId, new Vector4(1f, 1f, 0f, 0f));
                    context.cmd.DrawProcedural(Matrix4x4.identity, data.Material, 0, MeshTopology.Triangles, 3, 1);
                });
            }
        }

        private void UpdateMaterial(UniversalCameraData cameraData, bool innerLines)
        {
            var width = Mathf.Max(1, cameraData.scaledWidth);
            var height = Mathf.Max(1, cameraData.scaledHeight);
            var pixelScale = height / 1080f;
            var camera = cameraData.camera;
            var s = _settings;

            // SetVector: no implicit colour-space conversion; the shader works in linear space.
            _material.SetVector(OutlineColorId, s.color.value.linear);
            _material.SetVector(TargetSizeId, new Vector4(width, height, 1f / width, 1f / height));
            _material.SetVector(LineParamsId, new Vector4(
                InkOutlineMath.SampleRadiusPixels(s.silhouetteThickness.value, pixelScale),
                InkOutlineMath.SampleRadiusPixels(s.innerThickness.value, pixelScale),
                s.silhouetteIntensity.value,
                s.innerIntensity.value));
            _material.SetVector(DepthParamsId, new Vector4(s.depthThreshold.value, s.depthDistanceScale.value, s.grazingCorrection.value, s.edgeSoftness.value));
            _material.SetVector(NormalParamsId, new Vector4(s.normalThreshold.value, Mathf.Max(0.01f, s.edgeSoftness.value * 0.3f),
                s.innerFadeStart.value, Mathf.Max(s.innerFadeStart.value + 0.01f, s.innerFadeEnd.value)));
            _material.SetVector(CameraParamsId, new Vector4(camera.orthographic ? 1f : 0f, camera.nearClipPlane, camera.farClipPlane, 0f));
            _material.SetVector(CameraForwardId, camera.transform.forward);
            _material.SetFloat(DebugViewId, (float)s.debugView.value);
            CoreUtils.SetKeyword(_material, InnerLinesKeyword, innerLines);
            CoreUtils.SetKeyword(_material, HighQualityKeyword, s.highQualitySampling.value);
        }
    }

    /// <summary>Pure helpers shared by the pass and its tests.</summary>
    public static class InkOutlineMath
    {
        /// <summary>
        /// Lines are detected symmetrically (both sides of a discontinuity), so a line of
        /// <paramref name="thicknessPx1080"/> pixels needs a sample radius of half that, scaled to the target height.
        /// </summary>
        public static float SampleRadiusPixels(float thicknessPx1080, float pixelScale)
        {
            return Mathf.Max(0.5f, thicknessPx1080 * 0.5f * pixelScale);
        }
    }
}
