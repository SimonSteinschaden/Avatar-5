using UnityEngine;

namespace Avatar5.Input
{
    /// <summary>Isometric camera tuning. Orbit is locked by design: yaw only changes through this asset.</summary>
    [CreateAssetMenu(menuName = "Avatar5/Camera Profile", fileName = "CameraProfile")]
    public sealed class CameraProfile : ScriptableObject
    {
        [Header("Angle")]
        [SerializeField, Range(20f, 80f)] private float pitchDegrees = 40f;
        [SerializeField, Range(-180f, 180f)] private float yawDegrees = 45f;
        [SerializeField, Min(5f)] private float distance = 30f;

        [Header("Lens")]
        [SerializeField] private bool orthographic = true;
        [SerializeField, Range(10f, 60f)] private float perspectiveFieldOfView = 22f;
        [SerializeField, Min(1f)] private float defaultZoom = 8f;
        [SerializeField, Min(1f)] private float minZoom = 5f;
        [SerializeField, Min(1f)] private float maxZoom = 12f;
        [SerializeField, Min(0.01f)] private float zoomStep = 1f;

        [Header("Follow")]
        [SerializeField] private Vector3 followDamping = new Vector3(0.25f, 0.25f, 0.25f);
        [SerializeField] private Vector3 targetOffset = new Vector3(0f, 1f, 0f);
        [Tooltip("Target jumps larger than this (metres per frame) count as respawn/teleport and snap the camera.")]
        [SerializeField, Min(0.5f)] private float snapDistance = 5f;

        public float PitchDegrees { get { return pitchDegrees; } }
        public float YawDegrees { get { return yawDegrees; } }
        public float Distance { get { return distance; } }
        public bool Orthographic { get { return orthographic; } }
        public float PerspectiveFieldOfView { get { return perspectiveFieldOfView; } }
        public float DefaultZoom { get { return Mathf.Clamp(defaultZoom, minZoom, maxZoom); } }
        public float MinZoom { get { return minZoom; } }
        public float MaxZoom { get { return Mathf.Max(minZoom, maxZoom); } }
        public float ZoomStep { get { return zoomStep; } }
        public Vector3 FollowDamping { get { return followDamping; } }
        public Vector3 TargetOffset { get { return targetOffset; } }
        public float SnapDistance { get { return snapDistance; } }

        public Quaternion Rotation { get { return Quaternion.Euler(pitchDegrees, yawDegrees, 0f); } }

        public float ClampZoom(float zoom)
        {
            return Mathf.Clamp(zoom, MinZoom, MaxZoom);
        }
    }
}
