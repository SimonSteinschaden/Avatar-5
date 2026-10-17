using System.Collections.Generic;
using Avatar5.Combat;
using Avatar5.Contracts;
using Avatar5.Foundation;
using Avatar5.Gameplay.Combat;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Avatar5.Networking
{
    /// <summary>
    /// Runs the authoritative <see cref="CombatSimulation"/> on the host, feeds it casts from player commands,
    /// writes resulting actor state into replicated variables and broadcasts combat events and presentation cues
    /// to every peer (one named message per frame). Clients only receive; they never compute damage.
    /// </summary>
    [RequireComponent(typeof(NetworkManager))]
    public sealed class CombatHost : MonoBehaviour, ICombatWorld
    {
        private const string CombatMessage = "A5.Combat";
        private const float CastHeight = 1f;

        [SerializeField] private AbilityDefinitionAsset[] abilities = new AbilityDefinitionAsset[0];
        [SerializeField] private NetworkObject dummyPrefab;
        [SerializeField] private Transform[] dummyPoints = new Transform[0];
        [SerializeField, Min(0f)] private float dummyReviveSeconds = 3f;

        private readonly Dictionary<ulong, NetworkCombatActor> _actors = new Dictionary<ulong, NetworkCombatActor>();
        private readonly List<CombatEvent> _pendingEvents = new List<CombatEvent>();
        private readonly List<PresentationCue> _pendingCues = new List<PresentationCue>();
        private readonly Dictionary<ulong, float> _reviveAt = new Dictionary<ulong, float>();
        private readonly HashSet<Collider> _ignored = new HashSet<Collider>();
        private readonly RaycastHit[] _hits = new RaycastHit[16];
        private readonly CombatEventDeduplicator _received = new CombatEventDeduplicator();
        private NetworkManager _manager;
        private CombatSimulation _simulation;
        private AbilityCatalog _catalog;

        public static CombatHost Instance { get; private set; }
        public CombatSimulation Simulation { get { return _simulation; } }
        public int DuplicateEventsDropped { get { return _received.Dropped; } }

        public void Configure(AbilityDefinitionAsset[] abilityAssets, NetworkObject dummy, Transform[] points)
        {
            abilities = abilityAssets ?? new AbilityDefinitionAsset[0];
            dummyPrefab = dummy;
            dummyPoints = points ?? new Transform[0];
        }

        private void Awake()
        {
            Instance = this;
            _manager = GetComponent<NetworkManager>();
            var runtime = new List<AbilityDefinition>();
            foreach (var asset in abilities)
            {
                if (asset != null) runtime.Add(asset.ToRuntime());
            }
            _catalog = new AbilityCatalog(runtime);
        }

        private void OnEnable()
        {
            if (_manager == null) _manager = GetComponent<NetworkManager>();
            _manager.OnServerStarted += OnServerStarted;
            _manager.OnServerStopped += OnServerStopped;
            _manager.OnClientStarted += OnClientStarted;
        }

        private void OnDisable()
        {
            if (_manager == null) return;
            _manager.OnServerStarted -= OnServerStarted;
            _manager.OnServerStopped -= OnServerStopped;
            _manager.OnClientStarted -= OnClientStarted;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnClientStarted()
        {
            _received.Reset();
            if (_manager.CustomMessagingManager == null) return;
            _manager.CustomMessagingManager.UnregisterNamedMessageHandler(CombatMessage);
            if (!_manager.IsServer) _manager.CustomMessagingManager.RegisterNamedMessageHandler(CombatMessage, OnCombatMessage);
        }

        private void OnServerStarted()
        {
            _simulation = new CombatSimulation(_catalog, CombatRules.CreateDefault(), this);
            _simulation.EventRaised += OnSimulationEvent;
            _simulation.CueRaised += cue => _pendingCues.Add(cue);
            foreach (var actor in FindObjectsByType<NetworkCombatActor>(FindObjectsSortMode.None))
            {
                if (actor.IsSpawned) Register(actor);
            }
            SpawnDummies();
        }

        private void OnServerStopped(bool wasHost)
        {
            if (_simulation != null) _simulation.EventRaised -= OnSimulationEvent;
            _simulation = null;
            _actors.Clear();
            _reviveAt.Clear();
            _pendingEvents.Clear();
            _pendingCues.Clear();
        }

        // ---- actors ----------------------------------------------------------------------------

        internal void Register(NetworkCombatActor actor)
        {
            if (_simulation == null) return;
            _actors[actor.ActorId] = actor;
            _simulation.RegisterActor(actor.ActorId, actor.Team, actor.MaxHealth, actor.MaxEnergy, actor.EnergyRegenPerSecond, actor.Loadout);
            foreach (var c in actor.GetComponentsInChildren<Collider>()) _ignored.Add(c);
            ActorCombatState state;
            if (_simulation.TryGetState(actor.ActorId, out state)) actor.WriteState(state);
        }

        internal void Unregister(NetworkCombatActor actor)
        {
            if (_simulation != null) _simulation.RemoveActor(actor.ActorId);
            _actors.Remove(actor.ActorId);
            foreach (var c in actor.GetComponentsInChildren<Collider>()) _ignored.Remove(c);
        }

        private void SpawnDummies()
        {
            if (dummyPrefab == null) return;
            foreach (var point in dummyPoints)
            {
                if (point == null) continue;
                var dummy = Instantiate(dummyPrefab, point.position, point.rotation);
                dummy.Spawn(true);
            }
        }

        // ---- casting (called by NetworkPlayerAvatar on the host) --------------------------------

        /// <summary>
        /// Turns an accepted Cast command into an AbilityRequest. The ability comes from the actor's loadout slot,
        /// the target from the aim direction (or facing when not aiming).
        /// </summary>
        internal RequestStatus HandleCastCommand(ulong actorId, in PlayerCommand command, Transform actorTransform)
        {
            NetworkCombatActor actor;
            if (_simulation == null || !_actors.TryGetValue(actorId, out actor)) return RequestStatus.RejectedInvalidState;
            var slot = command.ActionRefId;
            if (slot < 0 || slot >= actor.Loadout.Length) return RequestStatus.RejectedInvalidState;

            var aim = new Vector3(command.Aim.X, 0f, command.Aim.Y);
            if (aim.sqrMagnitude < 1e-4f) aim = actorTransform.forward;
            aim.y = 0f;
            aim.Normalize();
            var target = actorTransform.position + aim * 5f;
            var request = new AbilityRequest
            {
                ActorId = actorId, SequenceId = command.SequenceId, AbilityId = actor.Loadout[slot],
                TargetOnGround = new Float2(target.x, target.z), ClientTick = command.ClientTick
            };
            return _simulation.TryCast(request).Status;
        }

        // ---- simulation loop -------------------------------------------------------------------

        private void Update()
        {
            if (_simulation == null || !_manager.IsServer) return;
            _simulation.Tick(Time.deltaTime);

            foreach (var pair in _actors)
            {
                ActorCombatState state;
                if (pair.Value != null && _simulation.TryGetState(pair.Key, out state)) pair.Value.WriteState(state);
            }

            if (_reviveAt.Count > 0)
            {
                var due = new List<ulong>();
                foreach (var pair in _reviveAt) if (Time.time >= pair.Value) due.Add(pair.Key);
                foreach (var id in due)
                {
                    _reviveAt.Remove(id);
                    _simulation.Revive(id);
                }
            }

            Flush();
        }

        private void OnSimulationEvent(CombatEvent e)
        {
            _pendingEvents.Add(e);
            NetworkCombatActor actor;
            if (e.ResultKind == (int)CombatEventKind.ActorDefeated && _actors.TryGetValue(e.TargetActorId, out actor)
                && actor.Team == TeamKind.Enemies && dummyReviveSeconds > 0f)
            {
                _reviveAt[e.TargetActorId] = Time.time + dummyReviveSeconds;
            }
        }

        private void Flush()
        {
            if (_pendingEvents.Count == 0 && _pendingCues.Count == 0) return;
            // The host presents locally; remote clients get one batched message per frame.
            foreach (var e in _pendingEvents) Deliver(e);
            foreach (var c in _pendingCues) Deliver(c);

            if (_manager.CustomMessagingManager != null && _manager.ConnectedClientsIds.Count > 1)
            {
                var size = 8 + _pendingEvents.Count * 48 + _pendingCues.Count * 48;
                var writer = new FastBufferWriter(size, Allocator.Temp, size + 1024);
                try
                {
                    writer.WriteValueSafe(_pendingEvents.Count);
                    foreach (var e in _pendingEvents) CombatWire.Write(ref writer, e);
                    writer.WriteValueSafe(_pendingCues.Count);
                    foreach (var c in _pendingCues) CombatWire.Write(ref writer, c);
                    var targets = new List<ulong>();
                    foreach (var id in _manager.ConnectedClientsIds) if (id != NetworkManager.ServerClientId) targets.Add(id);
                    _manager.CustomMessagingManager.SendNamedMessage(CombatMessage, targets, writer, NetworkDelivery.ReliableFragmentedSequenced);
                }
                finally
                {
                    writer.Dispose();
                }
            }
            _pendingEvents.Clear();
            _pendingCues.Clear();
        }

        private void OnCombatMessage(ulong sender, FastBufferReader reader)
        {
            if (sender != NetworkManager.ServerClientId) return;
            int count;
            reader.ReadValueSafe(out count);
            for (var i = 0; i < count; i++) Deliver(CombatWire.ReadEvent(ref reader));
            reader.ReadValueSafe(out count);
            for (var i = 0; i < count; i++) Deliver(CombatWire.ReadCue(ref reader));
        }

        private void Deliver(in CombatEvent e)
        {
            if (_received.Accept(e.EventId)) PresentationBus.Publish(e);
        }

        private void Deliver(in PresentationCue cue)
        {
            if (_received.Accept(cue.EventId)) PresentationBus.Publish(cue);
        }

        // ---- ICombatWorld ----------------------------------------------------------------------

        public bool TryGetPosition(ulong actorId, out Float2 position)
        {
            NetworkCombatActor actor;
            if (_actors.TryGetValue(actorId, out actor) && actor != null)
            {
                var p = actor.transform.position;
                position = new Float2(p.x, p.z);
                return true;
            }
            position = default(Float2);
            return false;
        }

        public float FreeFraction(Float2 from, Float2 to)
        {
            var start = new Vector3(from.X, CastHeight, from.Y);
            var delta = new Vector3(to.X - from.X, 0f, to.Y - from.Y);
            var length = delta.magnitude;
            if (length < 1e-4f) return 1f;
            var count = Physics.RaycastNonAlloc(start, delta / length, _hits, length, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            var best = 1f;
            for (var i = 0; i < count; i++)
            {
                if (_ignored.Contains(_hits[i].collider)) continue;
                var fraction = _hits[i].distance / length;
                if (fraction < best) best = fraction;
            }
            return best;
        }

        public void ApplyKnockback(ulong actorId, Float2 displacement)
        {
            NetworkCombatActor actor;
            if (_actors.TryGetValue(actorId, out actor) && actor != null) actor.ApplyKnockback(new Vector3(displacement.X, 0f, displacement.Y));
        }
    }

    /// <summary>Drops events/cues whose id was already delivered (retransmission, late duplicates).</summary>
    public sealed class CombatEventDeduplicator
    {
        private ulong _highest;
        private readonly HashSet<ulong> _recent = new HashSet<ulong>();

        public int Dropped { get; private set; }

        public bool Accept(ulong eventId)
        {
            // Ids arrive in order on a sequenced channel; the set covers the rare out-of-order case.
            if (eventId <= _highest && !_recent.Add(eventId)) { Dropped++; return false; }
            if (eventId > _highest)
            {
                _highest = eventId;
                _recent.Add(eventId);
                if (_recent.Count > 4096) _recent.Clear();
            }
            return true;
        }

        public void Reset()
        {
            _highest = 0;
            _recent.Clear();
            Dropped = 0;
        }
    }

    internal static class CombatWire
    {
        public static void Write(ref FastBufferWriter w, in CombatEvent e)
        {
            w.WriteValueSafe(e.EventId);
            w.WriteValueSafe(e.ServerTick);
            w.WriteValueSafe(e.SourceActorId);
            w.WriteValueSafe(e.TargetActorId);
            w.WriteValueSafe(e.AbilityId);
            w.WriteValueSafe(e.EffectId);
            w.WriteValueSafe(e.Amount);
            w.WriteValueSafe(e.ResultKind);
        }

        public static CombatEvent ReadEvent(ref FastBufferReader r)
        {
            var e = new CombatEvent();
            r.ReadValueSafe(out e.EventId);
            r.ReadValueSafe(out e.ServerTick);
            r.ReadValueSafe(out e.SourceActorId);
            r.ReadValueSafe(out e.TargetActorId);
            r.ReadValueSafe(out e.AbilityId);
            r.ReadValueSafe(out e.EffectId);
            r.ReadValueSafe(out e.Amount);
            r.ReadValueSafe(out e.ResultKind);
            return e;
        }

        public static void Write(ref FastBufferWriter w, in PresentationCue c)
        {
            w.WriteValueSafe(c.EventId);
            w.WriteValueSafe(c.ServerTick);
            w.WriteValueSafe(c.CueId);
            w.WriteValueSafe(c.Position.X); w.WriteValueSafe(c.Position.Y); w.WriteValueSafe(c.Position.Z);
            w.WriteValueSafe(c.Direction.X); w.WriteValueSafe(c.Direction.Y); w.WriteValueSafe(c.Direction.Z);
            w.WriteValueSafe(c.VisualSeed);
        }

        public static PresentationCue ReadCue(ref FastBufferReader r)
        {
            var c = new PresentationCue();
            r.ReadValueSafe(out c.EventId);
            r.ReadValueSafe(out c.ServerTick);
            r.ReadValueSafe(out c.CueId);
            float x, y, z;
            r.ReadValueSafe(out x); r.ReadValueSafe(out y); r.ReadValueSafe(out z);
            c.Position = new Float3(x, y, z);
            r.ReadValueSafe(out x); r.ReadValueSafe(out y); r.ReadValueSafe(out z);
            c.Direction = new Float3(x, y, z);
            r.ReadValueSafe(out c.VisualSeed);
            return c;
        }
    }
}
