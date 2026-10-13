using System.Collections.Generic;

namespace Avatar5.Gameplay
{
    /// <summary>
    /// Server-side, per-session duplicate gate for strictly increasing command sequences.
    /// The first observed sequence for an actor is accepted; equal or lower ones are rejected.
    /// Recreate/reset at explicit session boundaries; do not persist as account state.
    /// Sequence rollover is NOT supported yet: protocol must reject or renegotiate before wrap.
    /// Do not trust actor identity supplied by a client: session ownership must be checked first.
    /// </summary>
    public sealed class SequenceDeduplicator
    {
        private readonly Dictionary<ulong, uint> _lastSequence = new Dictionary<ulong, uint>();

        public bool TryAccept(ulong actorId, uint sequenceId)
        {
            uint previous;
            if (_lastSequence.TryGetValue(actorId, out previous) && sequenceId <= previous)
            {
                return false;
            }

            _lastSequence[actorId] = sequenceId;
            return true;
        }

        public bool RemoveActor(ulong actorId)
        {
            return _lastSequence.Remove(actorId);
        }

        public void Reset()
        {
            _lastSequence.Clear();
        }
    }
}
