using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Profiling;

namespace Avatar5.Rendering
{
    /// <summary>
    /// Automated frame-time measurement for the visual-style scene. Only runs when the player is started
    /// with <c>-a5benchmark &lt;output.csv&gt;</c>; cycles all quality profiles (plus Medium without ink lines),
    /// records CPU frame time, GPU frame time (FrameTimingManager) and the ink pass GPU time, writes a CSV
    /// and quits. Does nothing in normal play.
    /// </summary>
    public sealed class VisualStyleBenchmark : MonoBehaviour
    {
        public const string CommandLineFlag = "-a5benchmark";

        [SerializeField] private GraphicsQualityController quality;
        [SerializeField] private VisualStyleCameraRig cameraRig;
        [SerializeField] private float warmupSeconds = 2f;
        [SerializeField] private float measureSeconds = 8f;

        public void Configure(GraphicsQualityController newQuality, VisualStyleCameraRig newRig)
        {
            quality = newQuality;
            cameraRig = newRig;
        }

        private void Start()
        {
            var output = ReadOutputPath(Environment.GetCommandLineArgs());
            if (output == null || quality == null) return;
            StartCoroutine(Run(output));
        }

        public static string ReadOutputPath(string[] args)
        {
            for (var i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], CommandLineFlag, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            return null;
        }

        private IEnumerator Run(string outputPath)
        {
            Application.targetFrameRate = -1;
            Application.runInBackground = true;
            QualitySettings.vSyncCount = 0;
            var overlay = GetComponent<GraphicsDebugOverlay>();
            if (overlay != null) overlay.enabled = false;
            if (cameraRig != null) cameraRig.AutoPan = true;

            var inkRecorder = Recorder.Get(InkOutlineRendererFeature.ProfilerTag);
            inkRecorder.enabled = true;
            var timings = new FrameTiming[1];
            var csv = new StringBuilder();
            csv.AppendLine("case,width,height,frames,cpu_median_ms,cpu_p95_ms,cpu_max_ms,gpu_median_ms,gpu_p95_ms,ink_gpu_median_ms,fps_from_median");
            csv.AppendLine("# device," + Escape(SystemInfo.graphicsDeviceName) + "," + Escape(SystemInfo.graphicsDeviceType.ToString())
                           + "," + Escape(SystemInfo.processorType) + "," + Application.unityVersion);

            var cases = new List<(string name, int profile, bool ink)>();
            for (var i = 0; i < quality.ProfileCount; i++) cases.Add((null, i, true));
            cases.Add(("Medium (ink off)", Mathf.Min(1, quality.ProfileCount - 1), false));

            foreach (var c in cases)
            {
                quality.Apply(c.profile);
                quality.SetOutlineEnabled(c.ink && quality.CurrentProfile.outlineEnabled);
                var name = c.name ?? quality.CurrentProfile.displayName;

                var t = 0f;
                while (t < warmupSeconds) { t += Time.unscaledDeltaTime; yield return null; }

                var cpu = new List<float>();
                var gpu = new List<float>();
                var ink = new List<float>();
                t = 0f;
                while (t < measureSeconds)
                {
                    yield return null;
                    t += Time.unscaledDeltaTime;
                    cpu.Add(Time.unscaledDeltaTime * 1000f);
                    FrameTimingManager.CaptureFrameTimings();
                    if (FrameTimingManager.GetLatestTimings(1, timings) > 0 && timings[0].gpuFrameTime > 0) gpu.Add((float)timings[0].gpuFrameTime);
                    if (inkRecorder.isValid && inkRecorder.gpuElapsedNanoseconds > 0) ink.Add(inkRecorder.gpuElapsedNanoseconds / 1e6f);
                }

                var cpuMedian = Percentile(cpu, 0.5f);
                csv.AppendLine(string.Join(",", Escape(name), Screen.width.ToString(), Screen.height.ToString(), cpu.Count.ToString(),
                    F(cpuMedian), F(Percentile(cpu, 0.95f)), F(Percentile(cpu, 1f)), F(Percentile(gpu, 0.5f)), F(Percentile(gpu, 0.95f)), F(Percentile(ink, 0.5f)),
                    F(cpuMedian > 0f ? 1000f / cpuMedian : 0f)));
            }

            var directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(outputPath, csv.ToString());
            Debug.Log("Avatar5 benchmark written to " + outputPath + "\n" + csv);
            Application.Quit();
        }

        private static float Percentile(List<float> values, float p)
        {
            if (values.Count == 0) return -1f;
            var sorted = new List<float>(values);
            sorted.Sort();
            return sorted[Mathf.Clamp(Mathf.CeilToInt(p * sorted.Count) - 1, 0, sorted.Count - 1)];
        }

        private static string F(float v)
        {
            return v.ToString("F3", CultureInfo.InvariantCulture);
        }

        private static string Escape(string s)
        {
            return "\"" + (s ?? string.Empty).Replace("\"", "'") + "\"";
        }
    }
}
