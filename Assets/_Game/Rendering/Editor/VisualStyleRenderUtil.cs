using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Avatar5.EditorTools.Rendering
{
    /// <summary>Offscreen rendering and pixel statistics shared by the P07a screenshot tool and tests.</summary>
    public static class VisualStyleRenderUtil
    {
        /// <summary>Renders <paramref name="camera"/> into a new readable texture of the given size via URP's render request API.</summary>
        public static Texture2D Render(Camera camera, int width, int height)
        {
            // Shader variants must exist before the frame, otherwise the editor draws cyan placeholders.
            var previousAsync = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            var rt = RenderTexture.GetTemporary(new RenderTextureDescriptor(width, height, RenderTextureFormat.ARGB32, 24) { sRGB = true, msaaSamples = 1 });
            try
            {
                // First frame warms up history-dependent passes (SMAA/FXAA, volume stack).
                for (var i = 0; i < 2; i++)
                {
                    // StandardRequest renders like a game camera (volume update, camera stack, post-processing);
                    // SingleCameraRequest skips the volume framework, so the ink settings would not apply.
                    var request = new RenderPipeline.StandardRequest { destination = rt };
                    if (!RenderPipeline.SupportsRenderRequest(camera, request))
                        throw new System.InvalidOperationException("Avatar5: render request not supported for camera " + camera.name);
                    RenderPipeline.SubmitRenderRequest(camera, request);
                }

                var previous = RenderTexture.active;
                RenderTexture.active = rt;
                var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();
                RenderTexture.active = previous;
                return texture;
            }
            finally
            {
                RenderTexture.ReleaseTemporary(rt);
                ShaderUtil.allowAsyncCompilation = previousAsync;
            }
        }

        public static void SaveJpg(Texture2D texture, string path, int quality = 90)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, texture.EncodeToJPG(quality));
        }

        /// <summary>Pixel rectangle (x, y from bottom-left) covering the renderers below <paramref name="root"/>.</summary>
        public static RectInt ScreenRect(Camera camera, Transform root, int width, int height)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new RectInt(0, 0, 0, 0);
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);

            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            for (var i = 0; i < 8; i++)
            {
                var corner = new Vector3(
                    (i & 1) == 0 ? bounds.min.x : bounds.max.x,
                    (i & 2) == 0 ? bounds.min.y : bounds.max.y,
                    (i & 4) == 0 ? bounds.min.z : bounds.max.z);
                var v = camera.WorldToViewportPoint(corner);
                min = Vector2.Min(min, v);
                max = Vector2.Max(max, v);
            }

            var x0 = Mathf.Clamp(Mathf.FloorToInt(min.x * width), 0, width);
            var y0 = Mathf.Clamp(Mathf.FloorToInt(min.y * height), 0, height);
            var x1 = Mathf.Clamp(Mathf.CeilToInt(max.x * width), 0, width);
            var y1 = Mathf.Clamp(Mathf.CeilToInt(max.y * height), 0, height);
            return new RectInt(x0, y0, Mathf.Max(0, x1 - x0), Mathf.Max(0, y1 - y0));
        }

        public static float Luminance(Color32 c)
        {
            return (0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b) / 255f;
        }

        /// <summary>Counts near-black "ink" pixels inside a rect (whole image when rect is empty).</summary>
        public static int CountInk(Texture2D texture, RectInt rect, float maxLuminance = 0.12f)
        {
            return Count(texture, rect, c => Luminance(c) <= maxLuminance);
        }

        public static int Count(Texture2D texture, RectInt rect, System.Func<Color32, bool> predicate)
        {
            if (rect.width == 0 || rect.height == 0) rect = new RectInt(0, 0, texture.width, texture.height);
            var pixels = texture.GetPixels32();
            var count = 0;
            for (var y = rect.yMin; y < rect.yMax; y++)
            for (var x = rect.xMin; x < rect.xMax; x++)
                if (predicate(pixels[y * texture.width + x])) count++;
            return count;
        }

        public static float MeanLuminance(Texture2D texture, RectInt rect)
        {
            var pixels = texture.GetPixels32();
            double sum = 0;
            var n = 0;
            for (var y = rect.yMin; y < rect.yMax; y++)
            for (var x = rect.xMin; x < rect.xMax; x++)
            {
                sum += Luminance(pixels[y * texture.width + x]);
                n++;
            }
            return n > 0 ? (float)(sum / n) : 0f;
        }
    }
}
