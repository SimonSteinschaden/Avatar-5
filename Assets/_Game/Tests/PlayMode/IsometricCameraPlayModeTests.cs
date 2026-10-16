using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Avatar5.Tests.PlayMode
{
    public sealed class IsometricCameraPlayModeTests
    {
        private TestWorld _world;

        [SetUp]
        public void SetUp()
        {
            _world = new TestWorld();
            _world.Ground(200f);
        }

        [TearDown]
        public void TearDown()
        {
            _world.Dispose();
        }

        private static Vector3 Viewport(Camera camera, Transform target)
        {
            return camera.WorldToViewportPoint(target.position + Vector3.up);
        }

        private static IEnumerator Frames(int count)
        {
            for (var i = 0; i < count; i++) yield return null;
        }

        [UnityTest]
        public IEnumerator Camera_UsesLockedIsometricAngle()
        {
            var target = _world.Track(new GameObject("Target")).transform;
            Camera output;
            var rig = _world.CameraRig(target, out output);
            yield return Frames(3);

            var expected = rig.Profile.Rotation;
            Assert.That(Quaternion.Angle(output.transform.rotation, expected), Is.LessThan(0.1f));
            Assert.That(output.orthographic, Is.EqualTo(rig.Profile.Orthographic));
        }

        [UnityTest]
        public IEnumerator Camera_FollowsMovingPlayer()
        {
            var motor = _world.Motor(new Vector3(0f, 0.05f, 0f), simulateInUpdate: true);
            Camera output;
            _world.CameraRig(motor.transform, out output);
            yield return Frames(3);

            _world.Drive(motor, new Vector2(0.7071f, 0.7071f));
            var start = motor.transform.position;
            var elapsed = 0f;
            while (elapsed < 1.5f)
            {
                elapsed += Time.deltaTime;
                var during = Viewport(output, motor.transform);
                Assert.That(during.x, Is.InRange(0.1f, 0.9f), "player stays on screen while moving");
                Assert.That(during.y, Is.InRange(0.1f, 0.9f), "player stays on screen while moving");
                yield return null;
            }

            _world.Drive(motor, Vector2.zero);
            yield return new WaitForSeconds(1.5f);

            Assert.That(Vector3.Distance(start, motor.transform.position), Is.GreaterThan(3f), "player actually moved");
            var settled = Viewport(output, motor.transform);
            Assert.That(settled.x, Is.EqualTo(0.5f).Within(0.03f));
            Assert.That(settled.y, Is.EqualTo(0.5f).Within(0.03f));
        }

        [UnityTest]
        public IEnumerator Camera_StaysCentred_At16x9And21x9()
        {
            var target = _world.Track(new GameObject("Target")).transform;
            target.position = new Vector3(4f, 0f, -3f);
            Camera output;
            var rig = _world.CameraRig(target, out output);

            foreach (var aspect in new[] { 16f / 9f, 21f / 9f })
            {
                output.aspect = aspect;
                rig.SnapToTarget();
                yield return Frames(3);

                var viewport = Viewport(output, target);
                Assert.That(viewport.x, Is.EqualTo(0.5f).Within(0.02f), "aspect " + aspect);
                Assert.That(viewport.y, Is.EqualTo(0.5f).Within(0.02f), "aspect " + aspect);
                if (output.orthographic) Assert.That(output.orthographicSize, Is.EqualTo(rig.Zoom).Within(0.01f), "aspect " + aspect);
            }

            output.ResetAspect();
        }

        [UnityTest]
        public IEnumerator Camera_SnapsToPlayer_AfterRespawnTeleport()
        {
            var motor = _world.Motor(new Vector3(0f, 0.05f, 0f), simulateInUpdate: true);
            Camera output;
            var rig = _world.CameraRig(motor.transform, out output);
            yield return Frames(5);
            var snapsBefore = rig.SnapCount;

            motor.Teleport(new Vector3(40f, 0.05f, 35f), 0f);
            yield return Frames(2);

            Assert.That(rig.SnapCount, Is.GreaterThan(snapsBefore), "teleport detected");
            var viewport = Viewport(output, motor.transform);
            Assert.That(viewport.x, Is.EqualTo(0.5f).Within(0.03f));
            Assert.That(viewport.y, Is.EqualTo(0.5f).Within(0.03f));
        }

        [UnityTest]
        public IEnumerator Zoom_IsClampedToProfileLimits()
        {
            var target = _world.Track(new GameObject("Target")).transform;
            Camera output;
            var rig = _world.CameraRig(target, out output);
            yield return null;

            rig.AddZoom(100f);
            Assert.That(rig.Zoom, Is.EqualTo(rig.Profile.MinZoom));
            rig.AddZoom(-100f);
            Assert.That(rig.Zoom, Is.EqualTo(rig.Profile.MaxZoom));
            yield return Frames(2);
            if (output.orthographic) Assert.That(output.orthographicSize, Is.EqualTo(rig.Profile.MaxZoom).Within(0.01f));
        }
    }
}
