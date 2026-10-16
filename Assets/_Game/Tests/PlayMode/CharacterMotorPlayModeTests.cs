using System.Collections;
using Avatar5.Contracts;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Avatar5.Tests.PlayMode
{
    public sealed class CharacterMotorPlayModeTests
    {
        private TestWorld _world;

        [SetUp]
        public void SetUp()
        {
            _world = new TestWorld();
            _world.Ground();
        }

        [TearDown]
        public void TearDown()
        {
            _world.Dispose();
        }

        [UnityTest]
        public IEnumerator EightDirections_CoverEqualDistance_OnOpenGround()
        {
            yield return null;
            var distances = new float[8];
            for (var i = 0; i < 8; i++)
            {
                var motor = _world.Motor(new Vector3(0f, 0.05f, 0f));
                var raw = new Vector2(Mathf.Round(Mathf.Sin(i * Mathf.PI / 4f)), Mathf.Round(Mathf.Cos(i * Mathf.PI / 4f)));
                _world.Drive(motor, Vector2.ClampMagnitude(raw, 1f));
                TestWorld.Simulate(motor, 1f);

                var flat = motor.transform.position;
                flat.y = 0f;
                distances[i] = flat.magnitude;
                Object.Destroy(motor.gameObject);
                yield return null;
            }

            for (var i = 0; i < 8; i++)
            {
                Assert.That(distances[i], Is.GreaterThan(5f), "direction " + i + " moved");
                Assert.That(distances[i], Is.EqualTo(distances[0]).Within(0.05f), "direction " + i + " not faster/slower");
            }
        }

        [UnityTest]
        public IEnumerator Wall_StopsMovement()
        {
            _world.Box("Wall", new Vector3(3f, 1f, 0f), new Vector3(1f, 2f, 10f));
            var motor = _world.Motor(new Vector3(0f, 0.05f, 0f));
            yield return null;

            _world.Drive(motor, Vector2.right);
            TestWorld.Simulate(motor, 2f);

            // Wall face at x = 2.5, capsule radius 0.4 (+ controller skin).
            Assert.That(motor.transform.position.x, Is.InRange(1.9f, 2.15f));
            Assert.That(motor.CurrentPose.Velocity.X, Is.LessThan(0.5f), "no velocity builds up into the wall");
        }

        [UnityTest]
        public IEnumerator FrameDrop_DoesNotTunnelThroughThinWall()
        {
            _world.Box("ThinWall", new Vector3(1.5f, 1f, 0f), new Vector3(0.1f, 2f, 10f));
            var motor = _world.Motor(new Vector3(0f, 0.05f, 0f));
            yield return null;

            _world.Drive(motor, Vector2.right);
            for (var i = 0; i < 5; i++) motor.Simulate(1.5f); // five 1.5 s hitches

            Assert.That(motor.transform.position.x, Is.LessThan(1.5f));
        }

        [UnityTest]
        public IEnumerator DiagonalIntoWall_SlidesAlongIt()
        {
            _world.Box("Wall", new Vector3(2f, 1f, 0f), new Vector3(1f, 2f, 30f));
            var motor = _world.Motor(new Vector3(0f, 0.05f, 0f));
            yield return null;

            _world.Drive(motor, new Vector2(0.7071f, 0.7071f));
            TestWorld.Simulate(motor, 1.5f);

            Assert.That(motor.transform.position.x, Is.LessThan(1.5f));
            Assert.That(motor.transform.position.z, Is.GreaterThan(3f));
        }

        [UnityTest]
        public IEnumerator Facing_FollowsAim_WhileMovingSideways()
        {
            var motor = _world.Motor(new Vector3(0f, 0.05f, 0f));
            yield return null;

            _world.Drive(motor, Vector2.right, Vector2.up);
            TestWorld.Simulate(motor, 0.5f);

            Assert.That(motor.transform.position.x, Is.GreaterThan(1f));
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(motor.transform.eulerAngles.y, 0f)), Is.LessThan(0.5f));
        }

        [UnityTest]
        public IEnumerator Teleport_HaltsAndRaisesEvent()
        {
            var motor = _world.Motor(new Vector3(0f, 0.05f, 0f));
            var raised = false;
            motor.Teleported += _ => raised = true;
            yield return null;

            _world.Drive(motor, Vector2.up);
            TestWorld.Simulate(motor, 0.5f);
            motor.Teleport(new Vector3(10f, 0.05f, 10f), 90f);

            Assert.That(raised, Is.True);
            Assert.That(motor.CurrentPose.Velocity.X, Is.EqualTo(0f));
            Assert.That(motor.CurrentPose.Velocity.Z, Is.EqualTo(0f));
            Assert.That(Vector3.Distance(motor.transform.position, new Vector3(10f, 0.05f, 10f)), Is.LessThan(0.01f));
        }

        [UnityTest]
        public IEnumerator ForeignActorCommand_IsRejected_AndDoesNotMove()
        {
            var motor = _world.Motor(new Vector3(0f, 0.05f, 0f), actorId: 1);
            yield return null;

            var status = motor.Submit(new PlayerCommand { ActorId = 2, SequenceId = 1, Move = new Float2(1f, 0f) });
            TestWorld.Simulate(motor, 0.5f);

            Assert.That(status, Is.EqualTo(RequestStatus.RejectedNoAuthority));
            Assert.That(Mathf.Abs(motor.transform.position.x), Is.LessThan(0.01f));
        }
    }
}
