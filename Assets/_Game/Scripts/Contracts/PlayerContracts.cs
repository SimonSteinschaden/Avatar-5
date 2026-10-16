using System;

namespace Avatar5.Contracts
{
    /// <summary>
    /// Readable movement/aim snapshot of one actor. Written by whoever simulates the motor
    /// (locally the motor itself, later the host via P03); read by camera, HUD and presentation.
    /// Position is world space in metres, FacingYawDegrees is clockwise from world +Z.
    /// </summary>
    [Serializable]
    public struct PlayerPose
    {
        public ulong ActorId;
        public uint LastProcessedSequenceId;
        public Float3 Position;
        public Float3 Velocity;
        public float FacingYawDegrees;
        public Float2 AimDirection;
    }

    /// <summary>Command ingress port. Local in P01 (motor), replaced by a network sink in P03.</summary>
    public interface IPlayerCommandSink
    {
        RequestStatus Submit(in PlayerCommand command);
    }

    /// <summary>Read-only pose port for camera, HUD and presentation.</summary>
    public interface IPlayerPoseSource
    {
        PlayerPose CurrentPose { get; }
    }
}
