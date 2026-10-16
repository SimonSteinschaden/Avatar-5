using UnityEngine;

namespace Avatar5.Rendering
{
    /// <summary>
    /// Stand-alone isometric viewing camera for the visual-style scene (no dependency on P01).
    /// Defaults mirror the P01 gameplay camera (pitch 40°, yaw 45°, orthographic size 8).
    /// Optional slow automatic pan to inspect line stability under camera motion.
    /// </summary>
    [ExecuteAlways]
    public sealed class VisualStyleCameraRig : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Vector3 focusPoint = Vector3.zero;
        [Range(10f, 80f)] [SerializeField] private float pitchDegrees = 40f;
        [SerializeField] private float yawDegrees = 45f;
        [Min(1f)] [SerializeField] private float distance = 30f;
        [Range(3f, 14f)] [SerializeField] private float orthographicSize = 8f;
        [SerializeField] private bool autoPan;
        [SerializeField] private float autoPanRadius = 3f;
        [SerializeField] private float autoPanSpeed = 0.25f;

        private float _panTime;
        private Vector3 _panOffset;

        public bool AutoPan
        {
            get => autoPan;
            set => autoPan = value;
        }

        public float OrthographicSize
        {
            get => orthographicSize;
            set => orthographicSize = Mathf.Clamp(value, 3f, 14f);
        }

        public Vector3 FocusPoint
        {
            get => focusPoint;
            set => focusPoint = value;
        }

        public void Configure(Camera newCamera, Vector3 newFocus, float pitch, float yaw, float newDistance, float size)
        {
            targetCamera = newCamera;
            focusPoint = newFocus;
            pitchDegrees = pitch;
            yawDegrees = yaw;
            distance = newDistance;
            orthographicSize = size;
            Apply();
        }

        public void Pan(Vector2 planarDelta)
        {
            var yaw = Quaternion.Euler(0f, yawDegrees, 0f);
            focusPoint += yaw * new Vector3(planarDelta.x, 0f, planarDelta.y);
        }

        private void LateUpdate()
        {
            if (autoPan && Application.isPlaying)
            {
                _panTime += Time.deltaTime * autoPanSpeed;
                // Lissajous figure: covers diagonal and axis-aligned motion, the worst cases for line shimmer.
                _panOffset = new Vector3(Mathf.Sin(_panTime) * autoPanRadius, 0f, Mathf.Sin(_panTime * 2f) * autoPanRadius * 0.5f);
            }
            else
            {
                _panOffset = Vector3.zero;
            }

            Apply();
        }

        public void Apply()
        {
            if (targetCamera == null) targetCamera = GetComponent<Camera>();
            if (targetCamera == null) return;

            var rotation = Quaternion.Euler(pitchDegrees, yawDegrees, 0f);
            var focus = focusPoint + _panOffset;
            targetCamera.transform.SetPositionAndRotation(focus - rotation * Vector3.forward * distance, rotation);
            targetCamera.orthographic = true;
            targetCamera.orthographicSize = orthographicSize;
            targetCamera.nearClipPlane = 0.3f;
            targetCamera.farClipPlane = distance * 2.5f;
        }
    }
}
