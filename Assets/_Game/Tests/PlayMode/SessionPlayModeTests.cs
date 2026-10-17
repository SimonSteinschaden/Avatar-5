using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using Avatar5.Contracts;
using Avatar5.Networking;
using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.TestTools;

namespace Avatar5.Tests.PlayMode
{
    /// <summary>
    /// In-process checks of the direct session service with a real NetworkManager. Host + remote clients
    /// are covered by the multi-process test script (scripts/p03_session_test.ps1), not here.
    /// </summary>
    public sealed class SessionPlayModeTests
    {
        private const ushort TestPort = 7791;
        private GameObject _root;
        private DirectSessionService _service;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("SessionTest");
            var manager = _root.AddComponent<NetworkManager>();
            var transport = _root.AddComponent<UnityTransport>();
            manager.NetworkConfig = new NetworkConfig { NetworkTransport = transport };
            _service = new DirectSessionService(manager) { Port = TestPort, ConnectTimeoutSeconds = 3f };
        }

        [TearDown]
        public void TearDown()
        {
            var manager = _root.GetComponent<NetworkManager>();
            if (manager.IsListening) manager.Shutdown();
            _service.Dispose();
            Object.DestroyImmediate(_root);
        }

        [UnityTest]
        public IEnumerator SoloHost_IsConnectedWithOnePlayer_ThenLeaves()
        {
            var create = _service.CreatePrivateAsync(SessionConfig.Default, CancellationToken.None);
            yield return Wait(create);
            Assert.IsTrue(create.Result.Success, create.Result.Detail);
            Assert.AreEqual(SessionPhase.Connected, _service.State.Phase);
            Assert.AreEqual(SessionRole.Host, _service.State.Role);
            StringAssert.EndsWith(":" + TestPort, _service.State.JoinCode);

            for (var i = 0; i < 10 && _service.State.PlayerCount == 0; i++) yield return null;
            Assert.AreEqual(1, _service.State.PlayerCount);
            Assert.AreEqual(SessionConfig.AbsoluteMaxPlayers, _service.State.MaxPlayers);

            var leave = _service.LeaveAsync(CancellationToken.None);
            yield return Wait(leave);
            Assert.AreEqual(SessionPhase.Offline, _service.State.Phase);
            Assert.AreEqual(SessionError.None, _service.State.LastError);
        }

        [UnityTest]
        public IEnumerator SecondCreate_WhileActive_IsRejected()
        {
            var create = _service.CreatePrivateAsync(SessionConfig.Default, CancellationToken.None);
            yield return Wait(create);
            var again = _service.CreatePrivateAsync(SessionConfig.Default, CancellationToken.None);
            yield return Wait(again);
            Assert.IsFalse(again.Result.Success);
            Assert.AreEqual(SessionError.AlreadyInSession, again.Result.Error);
            Assert.AreEqual(SessionPhase.Connected, _service.State.Phase, "existing session must stay intact");
        }

        [UnityTest]
        public IEnumerator Join_MalformedCode_FailsWithoutTouchingNetwork()
        {
            var join = _service.JoinAsync("not a code", SessionConfig.Default, CancellationToken.None);
            yield return Wait(join);
            Assert.AreEqual(SessionError.InvalidCode, join.Result.Error);
            Assert.IsFalse(_root.GetComponent<NetworkManager>().IsListening);
        }

        [UnityTest]
        public IEnumerator Join_NobodyListening_ReportsNotFoundAfterTimeout()
        {
            var join = _service.JoinAsync("127.0.0.1:7792", SessionConfig.Default, CancellationToken.None);
            yield return Wait(join, 10f);
            Assert.IsFalse(join.Result.Success);
            Assert.AreEqual(SessionError.SessionNotFound, join.Result.Error);
            Assert.AreEqual(SessionPhase.Failed, _service.State.Phase);
            Assert.IsFalse(_root.GetComponent<NetworkManager>().IsListening);
        }

        private static IEnumerator Wait(Task task, float seconds = 5f)
        {
            var deadline = Time.realtimeSinceStartup + seconds;
            while (!task.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(task.IsCompleted, "task did not finish in time");
            if (task.IsFaulted) throw task.Exception;
        }
    }
}
