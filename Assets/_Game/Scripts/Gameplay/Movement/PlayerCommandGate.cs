using Avatar5.Contracts;

namespace Avatar5.Gameplay.Movement
{
    /// <summary>
    /// Authority + duplicate check in front of one actor's motor. Locally the motor owns it;
    /// in P03 the host additionally verifies that the sending connection owns <see cref="ActorId"/>.
    /// </summary>
    public sealed class PlayerCommandGate
    {
        private readonly SequenceDeduplicator _deduplicator = new SequenceDeduplicator();

        public PlayerCommandGate(ulong actorId)
        {
            ActorId = actorId;
        }

        public ulong ActorId { get; }

        public RequestStatus Check(in PlayerCommand command)
        {
            if (command.ActorId != ActorId) return RequestStatus.RejectedNoAuthority;
            return _deduplicator.TryAccept(command.ActorId, command.SequenceId)
                ? RequestStatus.Accepted
                : RequestStatus.Duplicate;
        }

        public void Reset()
        {
            _deduplicator.Reset();
        }
    }
}
