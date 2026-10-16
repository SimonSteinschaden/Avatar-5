using Avatar5.Contracts;

namespace Avatar5.Gameplay.Movement
{
    /// <summary>Collision step supplied by the engine adapter; returns the displacement that was actually possible.</summary>
    public interface IPlanarMover
    {
        Float2 Move(Float2 desiredDisplacement);
    }

    /// <summary>
    /// Engine-free character motor: consumes PlayerCommands (intents), integrates velocity and facing,
    /// and asks an <see cref="IPlanarMover"/> to resolve collisions. Frame hitches are split into bounded
    /// substeps and truncated beyond MaxSimulationStep * MaxSubstepsPerFrame, so a long frame never teleports.
    /// </summary>
    public sealed class PlayerMotorCore
    {
        private readonly PlayerCommandGate _gate;
        private MovementSettings _settings;
        private Float2 _moveIntent;
        private Float2 _aimIntent;

        public PlayerMotorCore(ulong actorId, MovementSettings settings, float initialYawDegrees = 0f)
        {
            settings.Validate();
            _gate = new PlayerCommandGate(actorId);
            _settings = settings;
            FacingYawDegrees = PlanarMath.Normalize360(initialYawDegrees);
        }

        public ulong ActorId { get { return _gate.ActorId; } }
        public MovementSettings Settings { get { return _settings; } }
        public Float2 Velocity { get; private set; }
        public float FacingYawDegrees { get; private set; }
        public Float2 MoveIntent { get { return _moveIntent; } }
        public Float2 AimDirection { get { return _aimIntent; } }
        public uint LastProcessedSequenceId { get; private set; }
        public PlayerActionKind LastAction { get; private set; }

        public RequestStatus Submit(in PlayerCommand command)
        {
            var status = _gate.Check(command);
            if (status != RequestStatus.Accepted) return status;

            LastProcessedSequenceId = command.SequenceId;
            _moveIntent = PlanarMath.ClampToUnit(command.Move);
            _aimIntent = PlanarMath.Normalize(command.Aim);
            LastAction = command.Action;
            return status;
        }

        /// <summary>Simulates <paramref name="deltaTime"/> seconds; returns the summed actual displacement.</summary>
        public Float2 Advance(float deltaTime, IPlanarMover mover)
        {
            var total = new Float2(0f, 0f);
            if (!(deltaTime > 0f)) return total;

            var maxStep = _settings.MaxSimulationStep;
            var remaining = deltaTime;
            var maxTotal = maxStep * _settings.MaxSubstepsPerFrame;
            if (remaining > maxTotal) remaining = maxTotal;

            // Tiny frame times (very high FPS) must still integrate; never drop them below an epsilon.
            while (remaining > 0f)
            {
                var step = remaining > maxStep ? maxStep : remaining;
                remaining = step < remaining ? remaining - step : 0f;
                total = PlanarMath.Add(total, Step(step, mover));
            }

            return total;
        }

        /// <summary>Clears intents and velocity, e.g. on respawn, stun or pause.</summary>
        public void Halt()
        {
            _moveIntent = new Float2(0f, 0f);
            Velocity = new Float2(0f, 0f);
        }

        public void ResetSequence()
        {
            _gate.Reset();
            LastProcessedSequenceId = 0;
        }

        private Float2 Step(float dt, IPlanarMover mover)
        {
            var desiredVelocity = PlanarMath.Scale(_moveIntent, _settings.MaxSpeed);
            var rate = PlanarMath.IsZero(_moveIntent) ? _settings.Deceleration : _settings.Acceleration;
            var velocity = PlanarMath.MoveTowards(Velocity, desiredVelocity, rate * dt);

            var facingSource = !PlanarMath.IsZero(_aimIntent) ? _aimIntent : _moveIntent;
            if (!PlanarMath.IsZero(facingSource))
            {
                FacingYawDegrees = PlanarMath.MoveTowardsAngle(FacingYawDegrees,
                    PlanarMath.YawFromDirection(facingSource), _settings.TurnSpeedDegreesPerSecond * dt);
            }

            var desired = PlanarMath.Scale(velocity, dt);
            var actual = mover != null ? mover.Move(desired) : desired;

            // Walls absorb the blocked velocity component instead of letting it build up.
            Velocity = PlanarMath.Scale(actual, 1f / dt);
            if (PlanarMath.Length(Velocity) > PlanarMath.Length(velocity)) Velocity = velocity;
            return actual;
        }
    }
}
