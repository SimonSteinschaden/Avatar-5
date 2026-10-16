using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

namespace Avatar5.Rendering
{
    /// <summary>
    /// Development overlay for P07a: frame times (CPU, GPU via FrameTimingManager), GPU time of the ink
    /// pass, current quality profile and line settings, plus hotkeys to switch them. Presentation only.
    /// F1 overlay · F2 quality · F3 ink on/off · F4 line debug view · F5 lighting · F6 camera pan ·
    /// [ / ] silhouette thickness.
    /// </summary>
    public sealed class GraphicsDebugOverlay : MonoBehaviour
    {
        [SerializeField] private GraphicsQualityController quality;
        [SerializeField] private LightingPresetSwitcher lighting;
        [SerializeField] private VisualStyleCameraRig cameraRig;
        [SerializeField] private bool visible = true;
        [SerializeField] private float sampleWindowSeconds = 0.5f;

        private readonly FrameTiming[] _timings = new FrameTiming[1];
        private Recorder _inkRecorder;
        private float _windowTime;
        private int _windowFrames;
        private float _windowMaxMs;
        private float _cpuMs;
        private float _cpuMaxMs;
        private double _gpuMs = -1;
        private double _inkGpuMs = -1;
        private bool _outlineOn = true;
        private InkOutlineDebugView _debugView = InkOutlineDebugView.Off;
        private GUIStyle _style;

        public void Configure(GraphicsQualityController newQuality, LightingPresetSwitcher newLighting, VisualStyleCameraRig newRig)
        {
            quality = newQuality;
            lighting = newLighting;
            cameraRig = newRig;
        }

        private void OnEnable()
        {
            _inkRecorder = Recorder.Get(InkOutlineRendererFeature.ProfilerTag);
            if (_inkRecorder != null && _inkRecorder.isValid) _inkRecorder.enabled = true;
        }

        private void OnDisable()
        {
            if (_inkRecorder != null && _inkRecorder.isValid) _inkRecorder.enabled = false;
        }

        private void Update()
        {
            HandleHotkeys();

            var dtMs = Time.unscaledDeltaTime * 1000f;
            _windowTime += Time.unscaledDeltaTime;
            _windowFrames++;
            _windowMaxMs = Mathf.Max(_windowMaxMs, dtMs);
            if (_windowTime >= sampleWindowSeconds)
            {
                _cpuMs = _windowTime * 1000f / _windowFrames;
                _cpuMaxMs = _windowMaxMs;
                _windowTime = 0f;
                _windowFrames = 0;
                _windowMaxMs = 0f;
            }

            FrameTimingManager.CaptureFrameTimings();
            _gpuMs = FrameTimingManager.GetLatestTimings(1, _timings) > 0 ? _timings[0].gpuFrameTime : -1;
            _inkGpuMs = _inkRecorder != null && _inkRecorder.isValid && _inkRecorder.gpuElapsedNanoseconds > 0
                ? _inkRecorder.gpuElapsedNanoseconds / 1e6
                : -1;
        }

        private void HandleHotkeys()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.f1Key.wasPressedThisFrame) visible = !visible;
            if (quality != null)
            {
                if (keyboard.f2Key.wasPressedThisFrame)
                {
                    quality.ApplyNext();
                    _outlineOn = quality.CurrentProfile == null || quality.CurrentProfile.outlineEnabled;
                    quality.SetDebugView(_debugView);
                }
                if (keyboard.f3Key.wasPressedThisFrame)
                {
                    _outlineOn = !_outlineOn;
                    quality.SetOutlineEnabled(_outlineOn);
                }
                if (keyboard.f4Key.wasPressedThisFrame)
                {
                    _debugView = (InkOutlineDebugView)(((int)_debugView + 1) % 3);
                    quality.SetDebugView(_debugView);
                }
                if (keyboard.leftBracketKey.wasPressedThisFrame) quality.AdjustSilhouetteThickness(-0.25f);
                if (keyboard.rightBracketKey.wasPressedThisFrame) quality.AdjustSilhouetteThickness(0.25f);
            }
            if (lighting != null && keyboard.f5Key.wasPressedThisFrame) lighting.ApplyNext();
            if (cameraRig != null)
            {
                if (keyboard.f6Key.wasPressedThisFrame) cameraRig.AutoPan = !cameraRig.AutoPan;
                var scroll = Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0f;
                if (Mathf.Abs(scroll) > 0.01f) cameraRig.OrthographicSize -= Mathf.Sign(scroll) * 0.5f;
                var pan = Vector2.zero;
                if (keyboard.wKey.isPressed) pan.y += 1f;
                if (keyboard.sKey.isPressed) pan.y -= 1f;
                if (keyboard.dKey.isPressed) pan.x += 1f;
                if (keyboard.aKey.isPressed) pan.x -= 1f;
                if (pan != Vector2.zero) cameraRig.Pan(pan * (6f * Time.unscaledDeltaTime));
            }
        }

        private void OnGUI()
        {
            if (!visible) return;
            if (_style == null) _style = new GUIStyle(GUI.skin.label) { fontSize = 14, richText = true };

            var volume = VolumeManager.instance.stack?.GetComponent<InkOutlineVolume>();
            GUILayout.BeginArea(new Rect(16, 16, 390, 300), GUI.skin.box);
            GUILayout.Label("<b>P07a GRAPHICS DEBUG</b>", _style);
            GUILayout.Label(string.Format("{0}×{1}   FPS {2:F0}   CPU {3:F2} ms (max {4:F1})",
                Screen.width, Screen.height, _cpuMs > 0f ? 1000f / _cpuMs : 0f, _cpuMs, _cpuMaxMs), _style);
            GUILayout.Label("GPU frame " + FormatMs(_gpuMs) + "   Ink pass " + FormatMs(_inkGpuMs), _style);
            GUILayout.Label("Quality: <b>" + (quality != null && quality.CurrentProfile != null ? quality.CurrentProfile.displayName : "scene default") + "</b>"
                            + "   Light: " + (lighting != null ? lighting.CurrentName : "-"), _style);
            if (volume != null)
            {
                GUILayout.Label(string.Format("Ink {0}  outer {1:F2}px  inner {2:F2}px ({3})  HQ {4}",
                    volume.enableInk.value ? "on" : "off", volume.silhouetteThickness.value, volume.innerThickness.value,
                    volume.innerLines.value ? "on" : "off", volume.highQualitySampling.value ? "on" : "off"), _style);
                GUILayout.Label(string.Format("Depth thr {0:F2} m   Normal thr {1:F2}   View {2}",
                    volume.depthThreshold.value, volume.normalThreshold.value, volume.debugView.value), _style);
            }
            GUILayout.Label("F1 overlay · F2 quality · F3 ink · F4 line view\nF5 lighting · F6 auto-pan · [ ] thickness · WASD pan · wheel zoom", _style);
            GUILayout.EndArea();
        }

        private static string FormatMs(double ms)
        {
            return ms >= 0 ? ms.ToString("F2") + " ms" : "n/a";
        }
    }
}
