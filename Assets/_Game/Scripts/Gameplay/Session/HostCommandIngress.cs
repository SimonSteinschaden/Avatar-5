using Avatar5.Contracts;

namespace Avatar5.Gameplay.Session
{
    /// <summary>Stable mapping between a session connection and the actor it controls.</summary>
    public static class ActorIds
    {
        /// <summary>Actor ids start at 1 so 0 can mean "none"; the host's own client (id 0) controls actor 1.</summary>
        public static ulong ForClient(ulong clientId)
        {
            return clientId + 1UL;
        }
    }

    /// <summary>
    /// Token bucket per sender: allows short bursts but caps the sustained command rate,
    /// so a flooding client cannot starve the host.
    /// </summary>
    public sealed class CommandRateLimiter
    {
        private readonly float _ratePerSecond;
        private readonly float _burst;
        private float _tokens;
        private double _lastTime;
        private bool _started;

        public CommandRateLimiter(float ratePerSecond, float burst)
        {
            _ratePerSecond = ratePerSecond > 0f ? ratePerSecond : 1f;
            _burst = burst >= 1f ? burst : 1f;
            _tokens = _burst;
        }

        public int Rejected { get; private set; }

        /// <param name="nowSeconds">Monotonic host time in seconds.</param>
        public bool TryConsume(double nowSeconds)
        {
            if (!_started)
            {
                _started = true;
                _lastTime = nowSeconds;
            }
            var elapsed = nowSeconds - _lastTime;
            if (elapsed > 0d)
            {
                _tokens += (float)(elapsed * _ratePerSecond);
                if (_tokens > _burst) _tokens = _burst;
                _lastTime = nowSeconds;
            }
            if (_tokens >= 1f)
            {
                _tokens -= 1f;
                return true;
            }
            Rejected++;
            return false;
        }
    }

    /// <summary>
    /// Host-side checks for commands arriving over the network, in front of the actor's motor (which still
    /// rejects duplicates/out-of-order sequences itself). The client never chooses which actor it drives:
    /// the actor id is derived from the sending connection.
    /// </summary>
    public sealed class HostCommandIngress
    {
        /// <summary>Clients send at most ~30 Hz plus action edges; 60/s with a burst of 20 leaves headroom.</summary>
        public const float DefaultRatePerSecond = 60f;
        public const float DefaultBurst = 20f;

        private readonly ulong _ownerClientId;
        private readonly CommandRateLimiter _limiter;

        public HostCommandIngress(ulong ownerClientId, float ratePerSecond = DefaultRatePerSecond, float burst = DefaultBurst)
        {
            _ownerClientId = ownerClientId;
            _limiter = new CommandRateLimiter(ratePerSecond, burst);
        }

        public ulong ActorId { get { return ActorIds.ForClient(_ownerClientId); } }
        public int RejectedNoAuthority { get; private set; }
        public int RejectedRateLimited { get { return _limiter.Rejected; } }

        /// <summary>
        /// Checks sender and rate. On success <paramref name="command"/> gets the authoritative ActorId,
        /// whatever the client wrote into it.
        /// </summary>
        public RequestStatus Admit(ulong senderClientId, ref PlayerCommand command, double nowSeconds)
        {
            if (senderClientId != _ownerClientId)
            {
                RejectedNoAuthority++;
                return RequestStatus.RejectedNoAuthority;
            }
            if (!_limiter.TryConsume(nowSeconds)) return RequestStatus.RejectedRateLimited;
            command.ActorId = ActorId;
            return RequestStatus.Accepted;
        }
    }

    /// <summary>
    /// Client-side send policy: action edges go out at once; changed intents at most every
    /// <see cref="MinInterval"/>; unchanged intents are refreshed every <see cref="HeartbeatInterval"/>.
    /// Keeps a 144 Hz client well below the host's rate limit.
    /// </summary>
    public sealed class CommandSendThrottle
    {
        private const float IntentEpsilon = 0.02f;

        private Float2 _lastMove;
        private Float2 _lastAim;
        private double _lastSent = double.NegativeInfinity;

        public CommandSendThrottle(float minInterval = 1f / 30f, float heartbeatInterval = 0.2f)
        {
            MinInterval = minInterval;
            HeartbeatInterval = heartbeatInterval;
        }

        public float MinInterval { get; }
        public float HeartbeatInterval { get; }

        public bool ShouldSend(in PlayerCommand command, double nowSeconds)
        {
            var since = nowSeconds - _lastSent;
            var send = command.Action != PlayerActionKind.None
                || (since >= MinInterval && (Differs(command.Move, _lastMove) || Differs(command.Aim, _lastAim)))
                || since >= HeartbeatInterval;
            if (!send) return false;
            _lastMove = command.Move;
            _lastAim = command.Aim;
            _lastSent = nowSeconds;
            return true;
        }

        private static bool Differs(Float2 a, Float2 b)
        {
            var dx = a.X - b.X;
            var dy = a.Y - b.Y;
            return dx * dx + dy * dy > IntentEpsilon * IntentEpsilon;
        }
    }
}
