using System.Collections.Generic;
using Avatar5.Contracts;
using Avatar5.Foundation;
using Avatar5.Gameplay.World;
using Avatar5.World;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Avatar5.Networking
{
    /// <summary>
    /// Runs the host's <see cref="WorldStateService"/> for the loaded level: player Interact intents, element impacts
    /// from the combat core and the goal check. Changes go to every client as they happen; a client that joins later
    /// gets the full snapshot first, so nobody can see (or open) a different door state.
    /// </summary>
    [RequireComponent(typeof(NetworkManager))]
    public sealed class WorldHost : MonoBehaviour
    {
        private const string ChangeMessage = "A5.World";
        private const string SnapshotMessage = "A5.WorldSnapshot";

        private NetworkManager _manager;
        private LevelAuthoring _level;
        private WorldStateService _service;
        private readonly Dictionary<int, int> _states = new Dictionary<int, int>();
        private readonly CombatEventDeduplicator _received = new CombatEventDeduplicator();
        private bool _impactHooked;

        public static WorldHost Instance { get; private set; }
        public bool IsCompleted { get; private set; }
        public int LevelId { get { return _level != null ? _level.LevelId : 0; } }

        /// <summary>Replicated state as this peer sees it (id → state code).</summary>
        public IReadOnlyDictionary<int, int> States { get { return _states; } }

        private void Awake()
        {
            Instance = this;
            _manager = GetComponent<NetworkManager>();
            _level = FindAnyObjectByType<LevelAuthoring>();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnEnable()
        {
            if (_manager == null) _manager = GetComponent<NetworkManager>();
            _manager.OnServerStarted += OnServerStarted;
            _manager.OnServerStopped += OnServerStopped;
            _manager.OnClientStarted += OnClientStarted;
            _manager.OnConnectionEvent += OnConnectionEvent;
        }

        private void OnDisable()
        {
            if (_manager == null) return;
            _manager.OnServerStarted -= OnServerStarted;
            _manager.OnServerStopped -= OnServerStopped;
            _manager.OnClientStarted -= OnClientStarted;
            _manager.OnConnectionEvent -= OnConnectionEvent;
        }

        private void OnServerStarted()
        {
            if (_level == null) return;
            _service = new WorldStateService(_level.BuildDefinition());
            _service.Changed += OnWorldChanged;
            _states.Clear();
            foreach (var pair in _service.Snapshot()) _states[pair.Key] = pair.Value;
            IsCompleted = false;
        }

        private void OnServerStopped(bool wasHost)
        {
            if (_service != null) _service.Changed -= OnWorldChanged;
            _service = null;
            _impactHooked = false;
        }

        private void OnClientStarted()
        {
            _received.Reset();
            if (_manager.CustomMessagingManager == null || _manager.IsServer) return;
            _manager.CustomMessagingManager.RegisterNamedMessageHandler(ChangeMessage, OnChangeMessage);
            _manager.CustomMessagingManager.RegisterNamedMessageHandler(SnapshotMessage, OnSnapshotMessage);
        }

        private void OnConnectionEvent(NetworkManager manager, ConnectionEventData data)
        {
            if (!manager.IsServer || _service == null || data.EventType != ConnectionEvent.ClientConnected) return;
            if (data.ClientId == NetworkManager.ServerClientId) return;
            SendSnapshot(data.ClientId);
        }

        // ---- host input ------------------------------------------------------------------------

        /// <summary>Interact intent of a player (host): operates the nearest lever in reach, if any.</summary>
        internal RequestStatus HandleInteract(ulong actorId, Vector3 position)
        {
            if (_service == null) return RequestStatus.RejectedInvalidState;
            var at = new Float2(position.x, position.z);
            var target = _service.FindInteractable(at);
            if (target == 0) return RequestStatus.RejectedRange;
            return _service.TryInteract(actorId, target, at);
        }

        private void Update()
        {
            if (_service == null || !_manager.IsServer) return;
            var combat = CombatHost.Instance;
            if (!_impactHooked && combat != null && combat.Simulation != null)
            {
                combat.Simulation.ImpactRaised += impact => { if (_service != null) _service.ApplyImpact(impact); };
                _impactHooked = true;
            }
            if (IsCompleted) return;
            foreach (var avatar in FindObjectsByType<NetworkPlayerAvatar>(FindObjectsSortMode.None))
            {
                if (!avatar.IsSpawned || (combat != null && combat.IsDefeated(avatar.ActorId))) continue;
                var p = avatar.transform.position;
                _service.ServerTick = (uint)(Time.timeAsDouble * 20d);
                _service.CheckGoal(avatar.ActorId, new Float2(p.x, p.z));
            }
        }

        // ---- replication -----------------------------------------------------------------------

        private void OnWorldChanged(WorldEvent e)
        {
            Apply(e);
            if (_manager.CustomMessagingManager == null || _manager.ConnectedClientsIds.Count <= 1) return;
            var writer = new FastBufferWriter(64, Allocator.Temp);
            try
            {
                Write(ref writer, e);
                var targets = new List<ulong>();
                foreach (var id in _manager.ConnectedClientsIds) if (id != NetworkManager.ServerClientId) targets.Add(id);
                _manager.CustomMessagingManager.SendNamedMessage(ChangeMessage, targets, writer, NetworkDelivery.ReliableSequenced);
            }
            finally
            {
                writer.Dispose();
            }
        }

        private void SendSnapshot(ulong clientId)
        {
            var snapshot = _service.Snapshot();
            var writer = new FastBufferWriter(16 + snapshot.Count * 8, Allocator.Temp);
            try
            {
                writer.WriteValueSafe(_service.LevelId);
                writer.WriteValueSafe(_service.IsCompleted);
                writer.WriteValueSafe(snapshot.Count);
                foreach (var pair in snapshot)
                {
                    writer.WriteValueSafe(pair.Key);
                    writer.WriteValueSafe(pair.Value);
                }
                _manager.CustomMessagingManager.SendNamedMessage(SnapshotMessage, clientId, writer, NetworkDelivery.ReliableSequenced);
            }
            finally
            {
                writer.Dispose();
            }
        }

        private void OnChangeMessage(ulong sender, FastBufferReader reader)
        {
            if (sender != NetworkManager.ServerClientId) return;
            Apply(Read(ref reader));
        }

        private void OnSnapshotMessage(ulong sender, FastBufferReader reader)
        {
            if (sender != NetworkManager.ServerClientId) return;
            int levelId, count;
            bool completed;
            reader.ReadValueSafe(out levelId);
            reader.ReadValueSafe(out completed);
            reader.ReadValueSafe(out count);
            for (var i = 0; i < count; i++)
            {
                int id, state;
                reader.ReadValueSafe(out id);
                reader.ReadValueSafe(out state);
                _states[id] = state;
                if (_level != null) _level.Apply(id, state);
            }
            IsCompleted = completed;
        }

        private void Apply(in WorldEvent e)
        {
            if (!_received.Accept(e.EventId)) return;
            if (e.StateCode == WorldStateCodes.LevelCompletedCode) IsCompleted = true;
            else
            {
                _states[e.WorldObjectId] = e.StateCode;
                if (_level != null) _level.Apply(e.WorldObjectId, e.StateCode);
            }
            PresentationBus.Publish(e);
        }

        private static void Write(ref FastBufferWriter w, in WorldEvent e)
        {
            w.WriteValueSafe(e.EventId);
            w.WriteValueSafe(e.ServerTick);
            w.WriteValueSafe(e.LevelId);
            w.WriteValueSafe(e.WorldObjectId);
            w.WriteValueSafe(e.StateCode);
        }

        private static WorldEvent Read(ref FastBufferReader r)
        {
            var e = new WorldEvent();
            r.ReadValueSafe(out e.EventId);
            r.ReadValueSafe(out e.ServerTick);
            r.ReadValueSafe(out e.LevelId);
            r.ReadValueSafe(out e.WorldObjectId);
            r.ReadValueSafe(out e.StateCode);
            return e;
        }
    }
}
