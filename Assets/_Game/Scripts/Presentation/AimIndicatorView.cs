using Avatar5.Contracts;
using Avatar5.Foundation;
using UnityEngine;

namespace Avatar5.Presentation
{
    /// <summary>Placeholder aim marker: reads the pose and places a marker in aim direction. Writes no gameplay state.</summary>
    public sealed class AimIndicatorView : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour poseSourceBehaviour;
        [SerializeField] private Transform marker;
        [SerializeField, Min(0f)] private float radius = 1.4f;
        [SerializeField] private float height = 0.1f;

        private IPlayerPoseSource _poseSource;

        public Transform Marker { get { return marker; } }

        public void Configure(IPlayerPoseSource poseSource, Transform markerTransform)
        {
            _poseSource = poseSource;
            marker = markerTransform;
        }

        private void Awake()
        {
            if (_poseSource == null) _poseSource = poseSourceBehaviour as IPlayerPoseSource;
        }

        private void LateUpdate()
        {
            if (_poseSource == null || marker == null) return;

            var pose = _poseSource.CurrentPose;
            var aim = pose.AimDirection.ToWorldXZ();
            if (aim.sqrMagnitude < 0.0001f) aim = Quaternion.Euler(0f, pose.FacingYawDegrees, 0f) * Vector3.forward;

            var origin = pose.Position.ToVector3();
            marker.SetPositionAndRotation(origin + aim.normalized * radius + Vector3.up * height,
                Quaternion.LookRotation(aim.normalized, Vector3.up));
        }
    }
}
