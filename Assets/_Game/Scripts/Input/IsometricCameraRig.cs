using Unity.Cinemachine;
using UnityEngine;

namespace Avatar5.Input
{
    /// <summary>
    /// Applies a <see cref="CameraProfile"/> to a CinemachineCamera with a PositionComposer body:
    /// fixed isometric rotation (no orbit), damped follow, clamped zoom and a snap fallback when the
    /// target jumps (respawn/teleport). Purely local presentation; never replicated.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public sealed class IsometricCameraRig : MonoBehaviour
    {
        [SerializeField] private CameraProfile profile;
        [SerializeField] private CinemachineCamera virtualCamera;
        [SerializeField] private Transform target;
        [SerializeField] private Camera outputCamera;

        private CinemachinePositionComposer _composer;
        private Vector3 _lastTargetPosition;
        private bool _hasLastTargetPosition;
        private float _zoom;

        public CameraProfile Profile { get { return profile; } }
        public CinemachineCamera VirtualCamera { get { return virtualCamera; } }
        public float Zoom { get { return _zoom; } }
        public int SnapCount { get; private set; }

        public Transform Target
        {
            get { return target; }
            set
            {
                target = value;
                if (virtualCamera != null) virtualCamera.Follow = value;
                SnapToTarget();
            }
        }

        public void Configure(CameraProfile cameraProfile, CinemachineCamera cinemachineCamera, Transform followTarget, Camera brainCamera)
        {
            profile = cameraProfile;
            outputCamera = brainCamera;
            virtualCamera = cinemachineCamera;
            target = followTarget;
            ApplyProfile();
        }

        public void ApplyProfile()
        {
            if (profile == null || virtualCamera == null) return;

            _composer = virtualCamera.GetComponent<CinemachinePositionComposer>();
            if (_composer == null) _composer = virtualCamera.gameObject.AddComponent<CinemachinePositionComposer>();

            virtualCamera.transform.rotation = profile.Rotation;
            virtualCamera.Follow = target;
            virtualCamera.LookAt = null;

            _composer.Damping = profile.FollowDamping;
            _composer.TargetOffset = profile.TargetOffset;

            _zoom = profile.DefaultZoom;
            ApplyZoom();
            SnapToTarget();
        }

        public void AddZoom(float steps)
        {
            if (profile == null || Mathf.Approximately(steps, 0f)) return;
            _zoom = profile.ClampZoom(_zoom - steps * profile.ZoomStep);
            ApplyZoom();
        }

        /// <summary>Drops damping history so the next camera update centres the target immediately.</summary>
        public void SnapToTarget()
        {
            if (virtualCamera == null) return;
            virtualCamera.PreviousStateIsValid = false;
            if (target != null)
            {
                _lastTargetPosition = target.position;
                _hasLastTargetPosition = true;
            }

            SnapCount++;
        }

        private void ApplyZoom()
        {
            if (profile == null || virtualCamera == null) return;

            var lens = virtualCamera.Lens;
            // The output camera owns the projection mode; the CinemachineCamera lens only carries size/FOV.
            if (outputCamera != null) outputCamera.orthographic = profile.Orthographic;
            if (profile.Orthographic)
            {
                lens.OrthographicSize = _zoom;
                if (_composer != null) _composer.CameraDistance = profile.Distance;
            }
            else
            {
                lens.FieldOfView = profile.PerspectiveFieldOfView;
                if (_composer != null) _composer.CameraDistance = profile.Distance * (_zoom / profile.DefaultZoom);
            }

            virtualCamera.Lens = lens;
        }

        private void Awake()
        {
            ApplyProfile();
        }

        private void LateUpdate()
        {
            if (target == null || profile == null) return;

            // Safe fallback for respawn/teleport: never sweep the damped camera across the level.
            if (_hasLastTargetPosition && (target.position - _lastTargetPosition).sqrMagnitude > profile.SnapDistance * profile.SnapDistance)
            {
                SnapToTarget();
            }

            _lastTargetPosition = target.position;
            _hasLastTargetPosition = true;
        }
    }
}
