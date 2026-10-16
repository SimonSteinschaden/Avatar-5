using System;
using Avatar5.Contracts;
using Avatar5.Gameplay.Movement;
using NUnit.Framework;

namespace Avatar5.Tests.EditMode
{
    public sealed class MovementCoreTests
    {
        private const ulong Actor = 7;
        private const float Tolerance = 1e-3f;

        private static MovementSettings Settings()
        {
            return new MovementSettings(6f, 60f, 80f, 720f, 1f / 60f, 8);
        }

        private static PlayerCommand Command(uint seq, float moveX, float moveY, float aimX = 0f, float aimY = 0f, ulong actor = Actor)
        {
            return new PlayerCommand
            {
                ActorId = actor,
                SequenceId = seq,
                ClientTick = seq,
                Move = new Float2(moveX, moveY),
                Aim = new Float2(aimX, aimY)
            };
        }

        private sealed class FreeMover : IPlanarMover
        {
            public Float2 Position;
            public Float2 Move(Float2 desired)
            {
                Position = PlanarMath.Add(Position, desired);
                return desired;
            }
        }

        /// <summary>Blocks any movement into x > WallX, like a wall perpendicular to +X.</summary>
        private sealed class WallMover : IPlanarMover
        {
            public float WallX;
            public Float2 Position;
            public Float2 Move(Float2 desired)
            {
                var target = PlanarMath.Add(Position, desired);
                if (target.X > WallX) target.X = WallX;
                var actual = new Float2(target.X - Position.X, target.Y - Position.Y);
                Position = target;
                return actual;
            }
        }

        [Test]
        public void KeyboardDiagonal_IsNotFasterThanAxial()
        {
            var diagonal = InputIntentMath.CameraRelativeMove(new Float2(1f, 1f), 0f, 0.2f);
            var axial = InputIntentMath.CameraRelativeMove(new Float2(1f, 0f), 0f, 0.2f);
            Assert.That(PlanarMath.Length(diagonal), Is.EqualTo(1f).Within(Tolerance));
            Assert.That(PlanarMath.Length(axial), Is.EqualTo(1f).Within(Tolerance));
        }

        [Test]
        public void StickInsideDeadZone_ProducesNoMovement()
        {
            var move = InputIntentMath.CameraRelativeMove(new Float2(0.1f, 0.05f), 45f, 0.2f);
            Assert.That(PlanarMath.IsZero(move), Is.True);
        }

        [Test]
        public void PartialStick_KeepsAnalogMagnitude()
        {
            var move = InputIntentMath.CameraRelativeMove(new Float2(0f, 0.5f), 0f, 0.2f);
            Assert.That(PlanarMath.Length(move), Is.EqualTo(0.5f).Within(Tolerance));
        }

        [TestCase(0f, 0f, 1f)]
        [TestCase(90f, 1f, 0f)]
        [TestCase(45f, 0.7071f, 0.7071f)]
        [TestCase(180f, 0f, -1f)]
        public void ForwardInput_FollowsCameraYaw(float cameraYaw, float expectedX, float expectedZ)
        {
            var move = InputIntentMath.CameraRelativeMove(new Float2(0f, 1f), cameraYaw, 0.2f);
            Assert.That(move.X, Is.EqualTo(expectedX).Within(Tolerance));
            Assert.That(move.Y, Is.EqualTo(expectedZ).Within(Tolerance));
        }

        [Test]
        public void RightInput_IsPerpendicularToForward_ForIsometricYaw()
        {
            var right = InputIntentMath.CameraRelativeMove(new Float2(1f, 0f), 45f, 0.2f);
            Assert.That(right.X, Is.EqualTo(0.7071f).Within(Tolerance));
            Assert.That(right.Y, Is.EqualTo(-0.7071f).Within(Tolerance));
        }

        [Test]
        public void EightDirections_ReachSameTopSpeedAndDistance()
        {
            var distances = new float[8];
            for (var i = 0; i < 8; i++)
            {
                var angle = i * Math.PI / 4.0;
                var raw = new Float2((float)Math.Round(Math.Sin(angle)), (float)Math.Round(Math.Cos(angle)));
                var move = InputIntentMath.CameraRelativeMove(raw, 45f, 0.2f);

                var motor = new PlayerMotorCore(Actor, Settings());
                var mover = new FreeMover();
                Assert.That(motor.Submit(Command(1, move.X, move.Y)), Is.EqualTo(RequestStatus.Accepted));
                for (var frame = 0; frame < 120; frame++) motor.Advance(1f / 60f, mover);

                Assert.That(PlanarMath.Length(motor.Velocity), Is.EqualTo(6f).Within(Tolerance), "direction " + i);
                distances[i] = PlanarMath.Length(mover.Position);
            }

            for (var i = 1; i < 8; i++) Assert.That(distances[i], Is.EqualTo(distances[0]).Within(1e-2f), "direction " + i);
        }

        [Test]
        public void Motor_AcceleratesAndDecelerates_WithoutOvershoot()
        {
            var motor = new PlayerMotorCore(Actor, Settings());
            var mover = new FreeMover();
            motor.Submit(Command(1, 1f, 0f));

            motor.Advance(1f / 60f, mover);
            Assert.That(motor.Velocity.X, Is.EqualTo(1f).Within(Tolerance), "60 m/s² * 1/60 s");

            for (var i = 0; i < 60; i++)
            {
                motor.Advance(1f / 60f, mover);
                Assert.That(PlanarMath.Length(motor.Velocity), Is.LessThanOrEqualTo(6f + Tolerance));
            }

            motor.Submit(Command(2, 0f, 0f));
            motor.Advance(0.2f, mover);
            Assert.That(PlanarMath.IsZero(motor.Velocity), Is.True, "80 m/s² stops 6 m/s within 0.075 s");
        }

        [Test]
        public void FrameHitch_IsTruncated_SoActorNeverTeleports()
        {
            var settings = Settings();
            var motor = new PlayerMotorCore(Actor, settings);
            var mover = new FreeMover();
            motor.Submit(Command(1, 0f, 1f));
            for (var i = 0; i < 60; i++) motor.Advance(1f / 60f, mover);
            var before = mover.Position;

            motor.Advance(5f, mover);

            var travelled = PlanarMath.Length(new Float2(mover.Position.X - before.X, mover.Position.Y - before.Y));
            var maxAllowed = settings.MaxSpeed * settings.MaxSimulationStep * settings.MaxSubstepsPerFrame;
            Assert.That(travelled, Is.LessThanOrEqualTo(maxAllowed + Tolerance));
        }

        [Test]
        public void TinyFrameTimes_StillIntegrate()
        {
            var motor = new PlayerMotorCore(Actor, Settings());
            var mover = new FreeMover();
            motor.Submit(Command(1, 1f, 0f));
            for (var i = 0; i < 100000; i++) motor.Advance(1e-5f, mover); // 1 s at 100k FPS
            Assert.That(mover.Position.X, Is.GreaterThan(5f));
        }

        [Test]
        public void Facing_FollowsAim_IndependentOfMovement()
        {
            var motor = new PlayerMotorCore(Actor, Settings());
            var mover = new FreeMover();
            motor.Submit(Command(1, 1f, 0f, 0f, 1f)); // move +X, aim +Z
            for (var i = 0; i < 60; i++) motor.Advance(1f / 60f, mover);

            Assert.That(motor.Velocity.X, Is.EqualTo(6f).Within(Tolerance));
            Assert.That(PlanarMath.DeltaAngle(motor.FacingYawDegrees, 0f), Is.EqualTo(0f).Within(0.01f));
        }

        [Test]
        public void Facing_FollowsMovement_WhenNoAim()
        {
            var motor = new PlayerMotorCore(Actor, Settings());
            motor.Submit(Command(1, -1f, 0f));
            for (var i = 0; i < 60; i++) motor.Advance(1f / 60f, null);
            Assert.That(PlanarMath.DeltaAngle(motor.FacingYawDegrees, 270f), Is.EqualTo(0f).Within(0.01f));
        }

        [Test]
        public void Facing_RespectsTurnSpeed()
        {
            var motor = new PlayerMotorCore(Actor, Settings());
            motor.Submit(Command(1, 0f, 0f, 0f, -1f)); // aim behind: 180°
            motor.Advance(1f / 60f, null);
            Assert.That(Math.Abs(PlanarMath.DeltaAngle(0f, motor.FacingYawDegrees)), Is.EqualTo(12f).Within(0.01f), "720°/s * 1/60 s");
        }

        [Test]
        public void Wall_AbsorbsBlockedVelocity_AndAllowsSliding()
        {
            var motor = new PlayerMotorCore(Actor, Settings());
            var mover = new WallMover { WallX = 1f };
            var diagonal = PlanarMath.Normalize(new Float2(1f, 1f));
            motor.Submit(Command(1, diagonal.X, diagonal.Y));
            for (var i = 0; i < 120; i++) motor.Advance(1f / 60f, mover);

            Assert.That(mover.Position.X, Is.EqualTo(1f).Within(Tolerance), "never passes the wall");
            Assert.That(motor.Velocity.X, Is.EqualTo(0f).Within(Tolerance), "no speed builds up into the wall");
            Assert.That(mover.Position.Y, Is.GreaterThan(5f), "slides along the wall");
        }

        [Test]
        public void PrebuiltCommandSequence_IsDeterministic()
        {
            Func<Float2> run = () =>
            {
                var motor = new PlayerMotorCore(Actor, Settings());
                var mover = new FreeMover();
                var sequencer = new PlayerCommandSequencer(Actor);
                for (uint tick = 0; tick < 90; tick++)
                {
                    var move = tick < 30 ? new Float2(1f, 0f) : tick < 60 ? new Float2(0f, 1f) : new Float2(-0.7071f, -0.7071f);
                    motor.Submit(sequencer.Next(tick, move, new Float2(0f, 0f)));
                    motor.Advance(1f / 60f, mover);
                }
                return mover.Position;
            };

            var first = run();
            var second = run();
            Assert.That(first.X, Is.EqualTo(second.X));
            Assert.That(first.Y, Is.EqualTo(second.Y));
        }

        [Test]
        public void CommandGate_RejectsForeignActorAndDuplicates()
        {
            var motor = new PlayerMotorCore(Actor, Settings());
            Assert.That(motor.Submit(Command(1, 1f, 0f, actor: Actor + 1)), Is.EqualTo(RequestStatus.RejectedNoAuthority));
            Assert.That(motor.Submit(Command(1, 1f, 0f)), Is.EqualTo(RequestStatus.Accepted));
            Assert.That(motor.Submit(Command(1, -1f, 0f)), Is.EqualTo(RequestStatus.Duplicate));
            Assert.That(motor.MoveIntent.X, Is.EqualTo(1f), "duplicate must not change intent");
            Assert.That(motor.LastProcessedSequenceId, Is.EqualTo(1u));
        }

        [Test]
        public void OversizedMoveIntent_IsClampedByMotor()
        {
            var motor = new PlayerMotorCore(Actor, Settings());
            motor.Submit(Command(1, 10f, 10f));
            for (var i = 0; i < 120; i++) motor.Advance(1f / 60f, null);
            Assert.That(PlanarMath.Length(motor.Velocity), Is.EqualTo(6f).Within(Tolerance));
        }

        [Test]
        public void Sequencer_ProducesStrictlyIncreasingIds()
        {
            var sequencer = new PlayerCommandSequencer(Actor);
            var a = sequencer.Next(0, new Float2(0f, 0f), new Float2(0f, 0f));
            var b = sequencer.Next(0, new Float2(2f, 0f), new Float2(0f, 0f));
            Assert.That(a.SequenceId, Is.EqualTo(1u));
            Assert.That(b.SequenceId, Is.EqualTo(2u));
            Assert.That(b.ActorId, Is.EqualTo(Actor));
            Assert.That(PlanarMath.Length(b.Move), Is.EqualTo(1f).Within(Tolerance));
        }

        [Test]
        public void TickClock_RunsAtTwentyHertz()
        {
            var clock = new ClientTickClock();
            Assert.That(clock.Advance(0.5f), Is.EqualTo(10u));
            Assert.That(clock.Advance(-1f), Is.EqualTo(10u));
        }

        [Test]
        public void PointerRay_ProjectsOntoGroundPlane()
        {
            Float3 hit;
            var ok = InputIntentMath.TryProjectRayOnGround(new Float3(0f, 10f, -10f), new Float3(0f, -1f, 1f), 0f, out hit);
            Assert.That(ok, Is.True);
            Assert.That(hit.X, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(hit.Z, Is.EqualTo(0f).Within(Tolerance));

            Assert.That(InputIntentMath.TryProjectRayOnGround(new Float3(0f, 10f, 0f), new Float3(0f, 1f, 0f), 0f, out hit), Is.False, "ray pointing away");
            Assert.That(InputIntentMath.TryProjectRayOnGround(new Float3(0f, 10f, 0f), new Float3(1f, 0f, 0f), 0f, out hit), Is.False, "parallel ray");
        }

        [Test]
        public void AimDirection_IsUnitLength_AndZeroOnActor()
        {
            var aim = InputIntentMath.AimDirection(new Float3(1f, 0f, 1f), new Float3(4f, 0f, 5f), 0.1f);
            Assert.That(aim.X, Is.EqualTo(0.6f).Within(Tolerance));
            Assert.That(aim.Y, Is.EqualTo(0.8f).Within(Tolerance));
            Assert.That(PlanarMath.IsZero(InputIntentMath.AimDirection(new Float3(1f, 0f, 1f), new Float3(1.05f, 0f, 1f), 0.1f)), Is.True);
        }

        [Test]
        public void StickAim_IsCameraRelativeUnitVector()
        {
            var aim = InputIntentMath.CameraRelativeAim(new Float2(0f, 0.4f), 90f, 0.2f);
            Assert.That(aim.X, Is.EqualTo(1f).Within(Tolerance));
            Assert.That(aim.Y, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(PlanarMath.IsZero(InputIntentMath.CameraRelativeAim(new Float2(0.1f, 0f), 90f, 0.2f)), Is.True);
        }

        [Test]
        public void InvalidSettings_AreRejected()
        {
            Assert.Throws<ArgumentException>(() => new PlayerMotorCore(Actor, new MovementSettings(0f, 1f, 1f, 1f, 0.01f, 1)));
        }

        [Test]
        public void Halt_ClearsVelocityAndIntent()
        {
            var motor = new PlayerMotorCore(Actor, Settings());
            motor.Submit(Command(1, 1f, 0f));
            motor.Advance(0.1f, null);
            motor.Halt();
            Assert.That(PlanarMath.IsZero(motor.Velocity), Is.True);
            motor.Advance(0.1f, null);
            Assert.That(PlanarMath.IsZero(motor.Velocity), Is.True);
        }
    }
}
