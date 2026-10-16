using Avatar5.Contracts;
using UnityEngine;

namespace Avatar5.Presentation
{
    /// <summary>Development-only OnGUI readout of movement and aim; production HUD belongs to P07.</summary>
    public sealed class MovementDebugHud : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour poseSourceBehaviour;
        [SerializeField] private bool visible = true;

        private IPlayerPoseSource _poseSource;

        public void Configure(IPlayerPoseSource poseSource)
        {
            _poseSource = poseSource;
        }

        private void Awake()
        {
            if (_poseSource == null) _poseSource = poseSourceBehaviour as IPlayerPoseSource;
        }

        private void OnGUI()
        {
            if (!visible || _poseSource == null) return;

            var pose = _poseSource.CurrentPose;
            var speed = Mathf.Sqrt(pose.Velocity.X * pose.Velocity.X + pose.Velocity.Z * pose.Velocity.Z);
            GUILayout.BeginArea(new Rect(Screen.width - 300, 20, 280, 150), GUI.skin.box);
            GUILayout.Label("P01 MOVEMENT DEBUG");
            GUILayout.Label(string.Format("Pos  {0:F2} / {1:F2} / {2:F2}", pose.Position.X, pose.Position.Y, pose.Position.Z));
            GUILayout.Label(string.Format("Speed {0:F2} m/s   Facing {1:F0}°", speed, pose.FacingYawDegrees));
            GUILayout.Label(string.Format("Aim  {0:F2} / {1:F2}", pose.AimDirection.X, pose.AimDirection.Y));
            GUILayout.Label("Seq  " + pose.LastProcessedSequenceId);
            GUILayout.Label("WASD/L-Stick move · Mouse/R-Stick aim · Wheel zoom");
            GUILayout.EndArea();
        }
    }
}
