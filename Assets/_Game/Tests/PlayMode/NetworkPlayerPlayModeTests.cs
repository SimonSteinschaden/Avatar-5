using System.Collections;
using System.Threading;
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
    /// Solo host with the real NetworkPlayer prefab: the spawner creates the host's avatar and host-side
    /// commands move it. Remote clients are covered by scripts/P03-NetworkPlayerTest.ps1.
    /// </summary>
    public sealed class NetworkPlayerPlayModeTests
    {
        private const string PrefabPath = "Assets/_Game/Prefabs/NetworkPlayer.prefab";
        private GameObject _root;
        private GameObject _ground;
        private DirectSessionService _service;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
#if UNITY_EDITOR
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
#else
            GameObject prefab = null;
#endif
            Assert.IsNotNull(prefab, "run Avatar5/Setup/Build P03 Network Arena first");

            _ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            _ground.transform.localScale = new Vector3(10f, 1f, 10f);

            _root = new GameObject("NetworkPlayerTest");
            _root.SetActive(false);
            var manager = _root.AddComponent<NetworkManager>();
            var transport = _root.AddComponent<UnityTransport>();
            manager.NetworkConfig = new NetworkConfig { NetworkTransport = transport, TickRate = 20 };
            manager.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = prefab });
            var spawner = _root.AddComponent<NetworkPlayerSpawner>();
            var spawn = new GameObject("Spawn").transform;
            spawn.SetParent(_root.transform, false);
            spawner.Configure(prefab.GetComponent<NetworkObject>(), new[] { spawn });
            _root.SetActive(true);
            _service = new DirectSessionService(manager) { Port = 7795 };
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            var manager = _root.GetComponent<NetworkManager>();
            if (manager.IsListening) manager.Shutdown();
            yield return null;
            _service.Dispose();
            Object.Destroy(_root);
            Object.Destroy(_ground);
            foreach (var avatar in Object.FindObjectsByType<NetworkPlayerAvatar>(FindObjectsSortMode.None)) Object.Destroy(avatar.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator HostAvatar_SpawnsOwnedAndMovesFromHostCommands()
        {
            var create = _service.CreatePrivateAsync(SessionConfig.Default, CancellationToken.None);
            while (!create.IsCompleted) yield return null;
            Assert.IsTrue(create.Result.Success, create.Result.Detail);

            NetworkPlayerAvatar avatar = null;
            for (var i = 0; i < 60 && avatar == null; i++)
            {
                avatar = Object.FindAnyObjectByType<NetworkPlayerAvatar>();
                yield return null;
            }
            Assert.IsNotNull(avatar, "spawner must create the host's avatar");
            Assert.IsTrue(avatar.IsOwner);
            Assert.AreEqual(1UL, avatar.ActorId);
            // No devices in batch runs; drive the avatar from the test instead of the input adapter.
            var adapter = avatar.GetComponent<Avatar5.Input.PlayerInputAdapter>();
            if (adapter != null) adapter.enabled = false;

            var start = avatar.transform.position;
            var deadline = Time.realtimeSinceStartup + 0.6f;
            uint sequence = 0;
            while (Time.realtimeSinceStartup < deadline)
            {
                var command = new PlayerCommand { ActorId = 77, SequenceId = ++sequence, Move = new Float2(1f, 0f) };
                Assert.AreNotEqual(RequestStatus.RejectedNoAuthority, avatar.Submit(command));
                yield return null;
            }
            var moved = avatar.transform.position - start;
            Assert.Greater(moved.x, 1f, "host simulation must move the avatar along +X");
            Assert.Less(Mathf.Abs(moved.z), 0.2f);
            Assert.AreEqual(1UL, avatar.CurrentPose.ActorId);
        }
    }
}
