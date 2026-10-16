using System.Collections;
using Avatar5.Contracts;
using Avatar5.Input;
using Avatar5.Player;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Avatar5.Tests.PlayMode
{
    /// <summary>Loads the generated P01 arena and drives the real player prefab through it.</summary>
    public sealed class TestArenaSmokeTests
    {
        private const string SceneName = "P01_TestArena";

        [UnityTest]
        public IEnumerator Arena_LoadsWithPlayerCameraAndObstacles()
        {
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            yield return null;

            var motor = Object.FindFirstObjectByType<CharacterMotor>();
            Assert.That(motor, Is.Not.Null, "player motor");
            Assert.That(motor.GetComponent<PlayerInputAdapter>(), Is.Not.Null, "input adapter");
            Assert.That(Object.FindFirstObjectByType<CinemachineBrain>(), Is.Not.Null, "cinemachine brain");
            Assert.That(Object.FindFirstObjectByType<IsometricCameraRig>(), Is.Not.Null, "camera rig");
            Assert.That(GameObject.Find("Crate_A"), Is.Not.Null, "obstacles");
            Assert.That(motor.IsGrounded, Is.True, "player stands on the ground");
        }

        [UnityTest]
        public IEnumerator Arena_PlayerWalksIntoCrate_StopsAndCameraFollows()
        {
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            yield return null;

            var motor = Object.FindFirstObjectByType<CharacterMotor>();
            motor.GetComponent<PlayerInputAdapter>().enabled = false; // test drives intents directly
            var camera = Camera.main;

            // Crate_A: 1.5 m cube at (-5, -3). Start south of it and walk north into it.
            motor.Teleport(new Vector3(-5f, 0.05f, -7f), 0f);
            yield return null;
            Assert.That(motor.Submit(new PlayerCommand
            {
                ActorId = motor.ActorId,
                SequenceId = 1_000_000,
                Move = new Float2(0f, 1f)
            }), Is.EqualTo(RequestStatus.Accepted));

            yield return new WaitForSeconds(2f);

            var z = motor.transform.position.z;
            Assert.That(z, Is.InRange(-4.4f, -3.75f), "stopped at the crate's south face (-3.75) minus radius");

            var viewport = camera.WorldToViewportPoint(motor.transform.position + Vector3.up);
            Assert.That(viewport.x, Is.EqualTo(0.5f).Within(0.05f));
            Assert.That(viewport.y, Is.EqualTo(0.5f).Within(0.05f));
        }
    }
}
