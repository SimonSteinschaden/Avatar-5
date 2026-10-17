using System;

namespace Avatar5.Contracts
{
    public enum ElementKind : byte { Fire, Water, Earth, Air }
    public enum PlayerActionKind : byte { None, Cast, Interact, Dodge }
    public enum RequestStatus : byte
    {
        Accepted,
        RejectedNoAuthority,
        RejectedCooldown,
        RejectedResource,
        RejectedRange,
        RejectedInvalidState,
        RejectedVersion,
        Duplicate,
        TimedOut,
        RejectedRateLimited
    }

    [Serializable]
    public struct Float2
    {
        public float X;
        public float Y;
        public Float2(float x, float y) { X = x; Y = y; }
    }

    [Serializable]
    public struct Float3
    {
        public float X;
        public float Y;
        public float Z;
        public Float3(float x, float y, float z) { X = x; Y = y; Z = z; }
    }

    [Serializable]
    public struct PlayerCommand
    {
        public ulong ActorId;
        public uint SequenceId;
        public uint ClientTick;
        public Float2 Move;
        public Float2 Aim;
        public PlayerActionKind Action;
        public int ActionRefId;
    }

    [Serializable]
    public struct AbilityRequest
    {
        public ulong ActorId;
        public uint SequenceId;
        public int AbilityId;
        public Float2 TargetOnGround;
        public uint ClientTick;
    }

    [Serializable]
    public struct CombatEvent
    {
        public ulong EventId;
        public uint ServerTick;
        public ulong SourceActorId;
        public ulong TargetActorId;
        public int AbilityId;
        public int EffectId;
        public float Amount;
        public int ResultKind;
    }

    [Serializable]
    public struct WorldEvent
    {
        public ulong EventId;
        public uint ServerTick;
        public int LevelId;
        public int WorldObjectId;
        public int StateCode;
    }

    [Serializable]
    public struct PresentationCue
    {
        public ulong EventId;
        public uint ServerTick;
        public int CueId;
        public Float3 Position;
        public Float3 Direction;
        public uint VisualSeed;
    }
}
