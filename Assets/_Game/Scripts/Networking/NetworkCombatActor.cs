using System;
using Avatar5.Contracts;
using Avatar5.Foundation;
using Unity.Netcode;
using UnityEngine;

namespace Avatar5.Networking
{
    /// <summary>Replicated combat state; written by the host only.</summary>
    public struct NetCombatState : INetworkSerializable, IEquatable<NetCombatState>
    {
        public float Health;
        public float MaxHealth;
        public float Energy;
        public float MaxEnergy;
        public byte Status;
        public bool Defeated;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Health);
            serializer.SerializeValue(ref MaxHealth);
            serializer.SerializeValue(ref Energy);
            serializer.SerializeValue(ref MaxEnergy);
            serializer.SerializeValue(ref Status);
            serializer.SerializeValue(ref Defeated);
        }

        public bool Equals(NetCombatState other)
        {
            return Mathf.Abs(Health - other.Health) < 0.01f && Mathf.Abs(MaxHealth - other.MaxHealth) < 0.01f
                && Mathf.Abs(Energy - other.Energy) < 0.5f && Mathf.Abs(MaxEnergy - other.MaxEnergy) < 0.01f
                && Status == other.Status && Defeated == other.Defeated;
        }
    }

    /// <summary>
    /// Combat participant on a network object (players and dummies). On the host it registers with
    /// <see cref="CombatHost"/>; every peer reads the replicated <see cref="ActorCombatState"/>.
    /// </summary>
    public sealed class NetworkCombatActor : NetworkBehaviour, ICombatStateSource
    {
        /// <summary>Non-player actor ids start here so they never collide with player actor ids (1..4).</summary>
        public const ulong NonPlayerIdBase = 1000;

        [SerializeField] private TeamKind team = TeamKind.Players;
        [SerializeField, Min(1f)] private float maxHealth = 100f;
        [SerializeField, Min(0f)] private float maxEnergy = 100f;
        [SerializeField, Min(0f)] private float energyRegenPerSecond = 15f;
        [SerializeField] private int[] loadout = new int[0];
        /// <summary>Seconds until the host revives this actor after defeat; 0 = stays defeated.</summary>
        [SerializeField, Min(0f)] private float reviveSeconds = 5f;

        private readonly NetworkVariable<NetCombatState> _state = new NetworkVariable<NetCombatState>();
        private NetworkPlayerAvatar _player;
        private CharacterController _controller;
        private UnityEngine.AI.NavMeshAgent _agent;

        public TeamKind Team { get { return team; } }
        public float MaxHealth { get { return maxHealth; } }
        public float MaxEnergy { get { return maxEnergy; } }
        public float EnergyRegenPerSecond { get { return energyRegenPerSecond; } }
        public int[] Loadout { get { return loadout; } }
        public float ReviveSeconds { get { return reviveSeconds; } }

        public ulong ActorId
        {
            get { return _player != null ? _player.ActorId : NonPlayerIdBase + NetworkObjectId; }
        }

        public ActorCombatState CombatState
        {
            get
            {
                var s = _state.Value;
                return new ActorCombatState
                {
                    ActorId = ActorId, Team = team, Health = s.Health, MaxHealth = s.MaxHealth, Energy = s.Energy,
                    MaxEnergy = s.MaxEnergy, Status = (StatusFlags)s.Status, Defeated = s.Defeated
                };
            }
        }

        public void Configure(TeamKind actorTeam, float health, float energy, float regen, int[] abilities, float revive = 5f)
        {
            reviveSeconds = revive;
            team = actorTeam;
            maxHealth = health;
            maxEnergy = energy;
            energyRegenPerSecond = regen;
            loadout = abilities ?? new int[0];
        }

        private void Awake()
        {
            _player = GetComponent<NetworkPlayerAvatar>();
            _controller = GetComponent<CharacterController>();
            _agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        }

        public override void OnNetworkSpawn()
        {
            ActorRegistry.Register(ActorId, transform);
            if (IsServer && CombatHost.Instance != null) CombatHost.Instance.Register(this);
        }

        public override void OnNetworkDespawn()
        {
            ActorRegistry.Unregister(ActorId, transform);
            if (IsServer && CombatHost.Instance != null) CombatHost.Instance.Unregister(this);
        }

        /// <summary>Host only: copies the simulation state into the replicated variable.</summary>
        internal void WriteState(in ActorCombatState state)
        {
            var next = new NetCombatState
            {
                Health = state.Health, MaxHealth = state.MaxHealth, Energy = state.Energy, MaxEnergy = state.MaxEnergy,
                Status = (byte)state.Status, Defeated = state.Defeated
            };
            if (!next.Equals(_state.Value)) _state.Value = next;
        }

        /// <summary>Host only: pushes the actor, respecting walls when it has a CharacterController.</summary>
        internal void ApplyKnockback(Vector3 displacement)
        {
            if (_agent != null && _agent.enabled && _agent.isOnNavMesh) _agent.Move(displacement);
            else if (_controller != null && _controller.enabled) _controller.Move(displacement);
            else transform.position += displacement;
        }
    }
}
