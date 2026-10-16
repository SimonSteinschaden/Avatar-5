using Avatar5.Contracts;

namespace Avatar5.Gameplay.Movement
{
    /// <summary>Client-side builder for strictly increasing PlayerCommand sequences of one local actor.</summary>
    public sealed class PlayerCommandSequencer
    {
        private uint _lastSequenceId;

        public PlayerCommandSequencer(ulong actorId)
        {
            ActorId = actorId;
        }

        public ulong ActorId { get; }
        public uint LastSequenceId { get { return _lastSequenceId; } }

        public PlayerCommand Next(uint clientTick, Float2 move, Float2 aim, PlayerActionKind action = PlayerActionKind.None, int actionRefId = 0)
        {
            _lastSequenceId++;
            return new PlayerCommand
            {
                ActorId = ActorId,
                SequenceId = _lastSequenceId,
                ClientTick = clientTick,
                Move = PlanarMath.ClampToUnit(move),
                Aim = aim,
                Action = action,
                ActionRefId = actionRefId
            };
        }
    }

    /// <summary>Fixed-rate client tick counter (20 Hz start value, see architecture §6).</summary>
    public sealed class ClientTickClock
    {
        public const float DefaultTickRate = 20f;
        private readonly float _tickRate;
        private double _time;

        public ClientTickClock(float tickRate = DefaultTickRate)
        {
            _tickRate = tickRate;
        }

        public uint CurrentTick { get { return (uint)(_time * _tickRate); } }

        public uint Advance(float deltaTime)
        {
            if (deltaTime > 0f) _time += deltaTime;
            return CurrentTick;
        }
    }
}
