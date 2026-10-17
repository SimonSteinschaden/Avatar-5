using System.Collections.Generic;
using Avatar5.Contracts;
using Avatar5.Foundation;
using Avatar5.Gameplay.Enemies;
using Avatar5.Gameplay.Save;
using Avatar5.Gameplay.World;
using Avatar5.Persistence;
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
    /// <summary>Where and what the host saves (v1: one host-owned campaign file). Overridable for tests.</summary>
    public static class CampaignSaveSettings
    {
        public static string SaveId = "campaign";
        public static string Directory;
        /// <summary>When set before hosting, the host loads the save instead of starting the level fresh.</summary>
        public static bool ContinueRequested;

        public static string ResolveDirectory()
        {
            return string.IsNullOrEmpty(Directory) ? System.IO.Path.Combine(Application.persistentDataPath, "Saves") : Directory;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ReadCommandLine()
        {
            var args = System.Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length; i++)
            {
                if (args[i] == "-a5-continue") ContinueRequested = true;
                if (i + 1 >= args.Length) continue;
                if (args[i] == "-a5-save-dir") Directory = args[i + 1];
                if (args[i] == "-a5-save-id") SaveId = args[i + 1];
            }
        }
    }

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
        private readonly RewardLedger _rewards = new RewardLedger();
        private AtomicSaveStore _store;
        private LevelDefinition _definition;
        private EncounterDirector _encounters;
        private readonly List<Float2> _livingPlayers = new List<Float2>();
        private bool _combatHooked;

        public const string SaveLogTag = "A5SAVE";
        public SaveLoadResult LastLoad { get; private set; }

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
            _definition = _level.BuildDefinition();
            _service = new WorldStateService(_definition);
            _rewards.Load(null);
            _store = new AtomicSaveStore(CampaignSaveSettings.ResolveDirectory());
            if (CampaignSaveSettings.ContinueRequested) LoadCampaign();
            _service.Changed += OnWorldChanged;
            _states.Clear();
            foreach (var pair in _service.Snapshot())
            {
                _states[pair.Key] = pair.Value;
                _level.Apply(pair.Key, pair.Value);
            }
            IsCompleted = _service.IsCompleted;

            _encounters = new EncounterDirector(_level.BuildEncounters());
            foreach (var o in _definition.Objects)
            {
                // Encounters finished before a checkpoint stay finished after "continue".
                if (o.Kind == WorldObjectKind.Encounter && _service.GetState(o.Id) == WorldStateCodes.On) _encounters.MarkCompleted(o.Id);
            }
            _encounters.Completed += id =>
            {
                Debug.Log("[Avatar5.World] encounter " + id + " completed");
                if (_service != null) _service.CompleteEncounter(id);
            };
        }

        private void OnServerStopped(bool wasHost)
        {
            if (_service != null) _service.Changed -= OnWorldChanged;
            _service = null;
            _impactHooked = false;
            _combatHooked = false;
            _encounters = null;
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
            if (!_combatHooked && combat != null && combat.Simulation != null && _encounters != null)
            {
                combat.Simulation.EventRaised += e =>
                {
                    if (e.ResultKind == (int)CombatEventKind.ActorDefeated && _encounters != null) _encounters.OnActorDefeated(e.TargetActorId);
                };
                _combatHooked = true;
            }
            if (IsCompleted) return;
            _livingPlayers.Clear();
            var playerCount = 0;
            foreach (var avatar in FindObjectsByType<NetworkPlayerAvatar>(FindObjectsSortMode.None))
            {
                if (!avatar.IsSpawned) continue;
                playerCount++;
                if (combat != null && combat.IsDefeated(avatar.ActorId)) continue;
                var p = avatar.transform.position;
                _livingPlayers.Add(new Float2(p.x, p.z));
                _service.ServerTick = (uint)(Time.timeAsDouble * 20d);
                _service.CheckGoal(avatar.ActorId, new Float2(p.x, p.z));
            }
            if (_encounters != null && combat != null)
            {
                foreach (var order in _encounters.Tick(_livingPlayers, playerCount))
                {
                    var actorId = combat.SpawnEnemy(order.EnemyTypeId, new Vector3(order.Position.X, 0f, order.Position.Y), order.HealthMultiplier);
                    if (actorId != 0) _encounters.RegisterSpawned(order.EncounterId, actorId);
                    Debug.Log("[Avatar5.World] encounter " + order.EncounterId + " spawns type " + order.EnemyTypeId
                        + " x" + order.HealthMultiplier.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " HP");
                }
            }
        }

        // ---- replication -----------------------------------------------------------------------

        private void LoadCampaign()
        {
            LastLoad = _store.Load(CampaignSaveSettings.SaveId, _definition);
            if (LastLoad.Success)
            {
                SaveSnapshots.Restore(LastLoad.Save, _service, _rewards);
                Debug.Log(SaveLogTag + " loaded id=" + LastLoad.Save.saveId + " checkpoint=" + LastLoad.Save.checkpointId
                    + " usedBackup=" + LastLoad.UsedBackup + (string.IsNullOrEmpty(LastLoad.Detail) ? string.Empty : " detail=" + LastLoad.Detail));
            }
            else
            {
                // A save we cannot use never gets overwritten silently by a fresh run's first checkpoint.
                Debug.LogWarning(SaveLogTag + " load-failed error=" + LastLoad.Error + " detail=" + LastLoad.Detail);
                if (LastLoad.Error != SaveLoadError.NotFound) CampaignSaveSettings.SaveId = CampaignSaveSettings.SaveId + "-new";
            }
        }

        /// <summary>Checkpoints of v1: a gate opening and finishing the level.</summary>
        private void SaveCheckpoint(int checkpointId)
        {
            if (_store == null) return;
            var players = new List<SavePlayerRecord>();
            var combat = CombatHost.Instance;
            foreach (var actor in FindObjectsByType<NetworkCombatActor>(FindObjectsSortMode.None))
            {
                if (!actor.IsSpawned || actor.Team != TeamKind.Players) continue;
                players.Add(new SavePlayerRecord { slot = (int)actor.ActorId - 1, health = actor.CombatState.Health });
            }
            var save = SaveSnapshots.Capture(CampaignSaveSettings.SaveId, _service, checkpointId, _rewards, players, System.DateTime.UtcNow);
            var result = _store.Save(save);
            Debug.Log(SaveLogTag + (result.Success ? " saved" : " save-failed error=" + result.Error + " detail=" + result.Detail)
                + " id=" + save.saveId + " checkpoint=" + checkpointId + " completed=" + save.levelCompleted);
        }

        private void OnWorldChanged(WorldEvent e)
        {
            Apply(e);
            if (_manager.IsServer)
            {
                WorldObjectKind kind;
                if (e.StateCode == WorldStateCodes.LevelCompletedCode)
                {
                    _rewards.TryGrant(RewardLedger.LevelCompleted(e.LevelId));
                    SaveCheckpoint(e.WorldObjectId);
                }
                else if (e.StateCode == WorldStateCodes.On && TryKind(e.WorldObjectId, out kind) && kind == WorldObjectKind.Gate)
                {
                    SaveCheckpoint(e.WorldObjectId);
                }
            }
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

        private bool TryKind(int objectId, out WorldObjectKind kind)
        {
            foreach (var o in _definition.Objects)
            {
                if (o.Id == objectId) { kind = o.Kind; return true; }
            }
            kind = WorldObjectKind.Lever;
            return false;
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
