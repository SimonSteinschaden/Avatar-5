using System.Collections;
using System.Threading;
using Avatar5.Combat;
using Avatar5.Contracts;
using Avatar5.Gameplay.Combat;
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
        private const string DummyPath = "Assets/_Game/Prefabs/TrainingDummy.prefab";
        private GameObject _root;
        private GameObject _ground;
        private DirectSessionService _service;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
#if UNITY_EDITOR
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var dummy = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(DummyPath);
            var abilities = new[]
            {
                UnityEditor.AssetDatabase.LoadAssetAtPath<AbilityDefinitionAsset>("Assets/_Game/Data/Abilities/fireball.asset"),
                UnityEditor.AssetDatabase.LoadAssetAtPath<AbilityDefinitionAsset>("Assets/_Game/Data/Abilities/water_wave.asset")
            };
#else
            GameObject prefab = null, dummy = null;
            AbilityDefinitionAsset[] abilities = null;
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
            manager.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = dummy });
            var spawner = _root.AddComponent<NetworkPlayerSpawner>();
            var spawn = new GameObject("Spawn").transform;
            spawn.SetParent(_root.transform, false);
            spawner.Configure(prefab.GetComponent<NetworkObject>(), new[] { spawn });
            var dummyPoint = new GameObject("DummyPoint").transform;
            dummyPoint.SetParent(_root.transform, false);
            dummyPoint.position = new Vector3(0f, 0f, 4f);
            _root.AddComponent<CombatHost>().Configure(abilities, dummy.GetComponent<NetworkObject>(), new[] { dummyPoint });
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
            foreach (var actor in Object.FindObjectsByType<NetworkCombatActor>(FindObjectsSortMode.None)) Object.Destroy(actor.gameObject);
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

        [UnityTest]
        public IEnumerator HostFireball_DamagesDummyOnceThroughCombatHost()
        {
            var create = _service.CreatePrivateAsync(SessionConfig.Default, CancellationToken.None);
            while (!create.IsCompleted) yield return null;
            Assert.IsTrue(create.Result.Success, create.Result.Detail);

            NetworkPlayerAvatar avatar = null;
            NetworkCombatActor dummy = null;
            for (var i = 0; i < 60 && (avatar == null || dummy == null); i++)
            {
                avatar = Object.FindAnyObjectByType<NetworkPlayerAvatar>();
                foreach (var actor in Object.FindObjectsByType<NetworkCombatActor>(FindObjectsSortMode.None))
                    if (actor.Team == TeamKind.Enemies) dummy = actor;
                yield return null;
            }
            Assert.IsNotNull(avatar);
            Assert.IsNotNull(dummy, "combat host must spawn the training dummy");
            var adapter = avatar.GetComponent<Avatar5.Input.PlayerInputAdapter>();
            if (adapter != null) adapter.enabled = false;
            var start = dummy.CombatState.Health;
            var casts = 0;
            var directHits = 0;
            System.Action<CombatEvent> listener = e =>
            {
                if (e.ResultKind == (int)CombatEventKind.AbilityCastAccepted) casts++;
                if (e.ResultKind == (int)CombatEventKind.DamageApplied && e.AbilityId == StarterAbilities.FireballId) directHits++;
            };
            Avatar5.Foundation.PresentationBus.CombatEventRaised += listener;

            var cast = new PlayerCommand { SequenceId = 10, Aim = new Float2(0f, 1f), Action = PlayerActionKind.Cast, ActionRefId = 0 };
            avatar.Submit(cast);
            avatar.Submit(cast); // duplicate sequence: must not cast twice

            var deadline = Time.realtimeSinceStartup + 1f;
            while (Time.realtimeSinceStartup < deadline) yield return null;

            Avatar5.Foundation.PresentationBus.CombatEventRaised -= listener;
            Assert.AreEqual(1, casts, "duplicate command must not cast twice");
            Assert.AreEqual(1, directHits, "exactly one fireball hit");
            // Hit damage plus at most one burn tick (0.5 s steps) inside the 1 s window.
            Assert.That(start - dummy.CombatState.Health, Is.InRange(StarterAbilities.Fireball().Damage, StarterAbilities.Fireball().Damage + 2.5f + 0.01f));
            Assert.IsTrue((dummy.CombatState.Status & StatusFlags.Burning) != 0);
            var player = avatar.GetComponent<NetworkCombatActor>();
            Assert.Less(player.CombatState.Energy, player.CombatState.MaxEnergy, "energy was spent");
        }
    }
}