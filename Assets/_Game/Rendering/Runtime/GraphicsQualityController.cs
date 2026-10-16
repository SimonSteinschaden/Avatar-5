using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Avatar5.Rendering
{
    /// <summary>
    /// Applies a <see cref="ToonQualityProfile"/> at runtime: ink-line overrides via a private global
    /// volume, the simplified-toon shader keyword and per-camera AA/shadows. Everything is reverted on
    /// disable, so play-mode switching never dirties project assets. Presentation only.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GraphicsQualityController : MonoBehaviour
    {
        public const string SimplifiedToonKeyword = "A5_TOON_SIMPLE";
        private const float OverridePriority = 100f;

        [SerializeField] private ToonQualityProfile[] profiles = Array.Empty<ToonQualityProfile>();
        [SerializeField] private int startProfileIndex = 1;
        [Tooltip("Cameras to configure. Empty = Camera.main.")]
        [SerializeField] private Camera[] cameras = Array.Empty<Camera>();

        private Volume _overrideVolume;
        private VolumeProfile _overrideProfile;
        private InkOutlineVolume _overrides;
        private GlobalKeyword _simplifiedKeyword;
        private int _currentIndex = -1;
        private CameraState[] _cameraStates = Array.Empty<CameraState>();

        public event Action<ToonQualityProfile> ProfileApplied;

        public int CurrentIndex => _currentIndex;
        public int ProfileCount => profiles.Length;
        public ToonQualityProfile CurrentProfile => _currentIndex >= 0 && _currentIndex < profiles.Length ? profiles[_currentIndex] : null;
        public InkOutlineVolume Overrides => _overrides;

        private struct CameraState
        {
            public UniversalAdditionalCameraData Data;
            public AntialiasingMode Antialiasing;
            public AntialiasingQuality AntialiasingQuality;
            public bool RenderShadows;
            public bool RenderPostProcessing;
        }

        public void Configure(ToonQualityProfile[] newProfiles, Camera[] newCameras, int startIndex)
        {
            profiles = newProfiles ?? Array.Empty<ToonQualityProfile>();
            cameras = newCameras ?? Array.Empty<Camera>();
            startProfileIndex = startIndex;
        }

        private void OnEnable()
        {
            EnsureInitialised();
            if (profiles.Length > 0) Apply(Mathf.Clamp(startProfileIndex, 0, profiles.Length - 1));
        }

        private void OnDisable()
        {
            Shutdown();
        }

        public void EnsureInitialised()
        {
            if (_overrideVolume != null) return;

            _simplifiedKeyword = GlobalKeyword.Create(SimplifiedToonKeyword);
            var volumeObject = new GameObject("Avatar5 Quality Overrides (runtime)") { hideFlags = HideFlags.HideAndDontSave };
            volumeObject.transform.SetParent(transform, false);
            _overrideVolume = volumeObject.AddComponent<Volume>();
            _overrideVolume.isGlobal = true;
            _overrideVolume.priority = OverridePriority;
            _overrideProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            _overrideProfile.hideFlags = HideFlags.HideAndDontSave;
            _overrides = _overrideProfile.Add<InkOutlineVolume>();
            _overrideVolume.sharedProfile = _overrideProfile;

            var targets = cameras.Length > 0 ? cameras : (Camera.main != null ? new[] { Camera.main } : Array.Empty<Camera>());
            _cameraStates = new CameraState[targets.Length];
            for (var i = 0; i < targets.Length; i++)
            {
                if (targets[i] == null) continue;
                var data = targets[i].GetUniversalAdditionalCameraData();
                _cameraStates[i] = new CameraState
                {
                    Data = data,
                    Antialiasing = data.antialiasing,
                    AntialiasingQuality = data.antialiasingQuality,
                    RenderShadows = data.renderShadows,
                    RenderPostProcessing = data.renderPostProcessing
                };
            }
        }

        public void Apply(int index)
        {
            EnsureInitialised();
            if (profiles.Length == 0) return;
            _currentIndex = ((index % profiles.Length) + profiles.Length) % profiles.Length;
            var profile = profiles[_currentIndex];
            if (profile == null) return;

            profile.ApplyTo(_overrides);
            Shader.SetKeyword(_simplifiedKeyword, profile.simplifiedToon);
            foreach (var state in _cameraStates)
            {
                if (state.Data == null) continue;
                state.Data.antialiasing = profile.antialiasing;
                state.Data.antialiasingQuality = profile.antialiasingQuality;
                state.Data.renderShadows = profile.renderShadows;
                // FXAA/SMAA run in URP post-processing.
                if (profile.antialiasing != AntialiasingMode.None) state.Data.renderPostProcessing = true;
            }

            ProfileApplied?.Invoke(profile);
        }

        public void ApplyNext()
        {
            Apply(_currentIndex + 1);
        }

        public void SetOutlineEnabled(bool value)
        {
            EnsureInitialised();
            _overrides.enableInk.Override(value);
        }

        public void AdjustSilhouetteThickness(float deltaPx)
        {
            EnsureInitialised();
            var current = _overrides.silhouetteThickness.overrideState
                ? _overrides.silhouetteThickness.value
                : VolumeManager.instance.stack.GetComponent<InkOutlineVolume>().silhouetteThickness.value;
            _overrides.silhouetteThickness.Override(Mathf.Clamp(current + deltaPx, 0.5f, 6f));
        }

        public void SetDebugView(InkOutlineDebugView view)
        {
            EnsureInitialised();
            _overrides.debugView.Override(view);
        }

        public void Shutdown()
        {
            if (_overrideVolume == null) return;
            Shader.SetKeyword(_simplifiedKeyword, false);
            foreach (var state in _cameraStates)
            {
                if (state.Data == null) continue;
                state.Data.antialiasing = state.Antialiasing;
                state.Data.antialiasingQuality = state.AntialiasingQuality;
                state.Data.renderShadows = state.RenderShadows;
                state.Data.renderPostProcessing = state.RenderPostProcessing;
            }

            DestroyImmediateOrDeferred(_overrideVolume.gameObject);
            DestroyImmediateOrDeferred(_overrideProfile);
            _overrideVolume = null;
            _overrideProfile = null;
            _overrides = null;
            _currentIndex = -1;
            _cameraStates = Array.Empty<CameraState>();
        }

        private static void DestroyImmediateOrDeferred(UnityEngine.Object target)
        {
            if (target == null) return;
            if (Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);
        }
    }
}
