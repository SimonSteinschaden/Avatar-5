using System.Collections.Generic;
using Avatar5.Contracts;
using Avatar5.Gameplay.Enemies;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace Avatar5.Networking
{
    /// <summary>
    /// Host-side driver of one <see cref="EnemyBrain"/>: collects player candidates, walks with a NavMeshAgent,
    /// announces telegraphs and releases attacks through the combat core. Clients only see the replicated
    /// transform, the replicated <see cref="EnemyState"/> and the cues.
    /// </summary>
    [RequireComponent(typeof(NetworkCombatActor))]
    public sealed class EnemyBrainHost : NetworkBehaviour
    {
        [SerializeField] private int enemyTypeId = StarterEnemies.BruteTypeId;

        private readonly NetworkVariable<byte> _state = new NetworkVariable<byte>();
        private readonly List<EnemyTargetCandidate> _candidates = new List<EnemyTargetCandidate>();
        private NetworkCombatActor _actor;
        private NavMeshAgent _agent;
        private EnemyBrain _brain;
        private Vector3 _home;
        private uint _sequence;
        private bool _wasFrozen;

        public EnemyState State { get { return (EnemyState)_state.Value; } }
        public ulong Target { get { return _brain != null ? _brain.Target : 0; } }
        public int EnemyTypeId { get { return enemyTypeId; } }

        private void Awake()
        {
            _actor = GetComponent<NetworkCombatActor>();
            _agent = GetComponent<NavMeshAgent>();
        }

        public override void OnNetworkSpawn()
        {
            if (!IsServer)
            {
                if (_agent != null) _agent.enabled = false;
                return;
            }
            var definition = EnemyCatalog.Get(enemyTypeId);
            _home = transform.position;
            _brain = new EnemyBrain(definition, new Float2(_home.x, _home.z));
            if (_agent != null)
            {
                _agent.speed = definition.MoveSpeed;
                _agent.stoppingDistance = definition.AttackStartRange * 0.8f;
                if (!_agent.isOnNavMesh) _agent.Warp(_home);
            }
            if (CombatHost.Instance != null && CombatHost.Instance.Simulation != null) CombatHost.Instance.Simulation.EventRaised += OnCombatEvent;
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && CombatHost.Instance != null && CombatHost.Instance.Simulation != null)
                CombatHost.Instance.Simulation.EventRaised -= OnCombatEvent;
        }

        private void OnCombatEvent(CombatEvent e)
        {
            if (e.TargetActorId != _actor.ActorId) return;
            if (e.ResultKind == (int)CombatEventKind.ActorDefeated)
            {
                _brain.OnDefeated();
                if (_agent != null && _agent.isOnNavMesh) _agent.ResetPath();
            }
            else if (e.ResultKind == (int)CombatEventKind.ActorRevived)
            {
                _brain.OnRevived();
                if (_agent != null) _agent.Warp(_home);
                else transform.position = _home;
            }
        }

        private void Update()
        {
            if (!IsServer || _brain == null || CombatHost.Instance == null || CombatHost.Instance.Simulation == null) return;
            var host = CombatHost.Instance;
            host.CollectPlayerCandidates(_actor.ActorId, _candidates);
            var self = new Float2(transform.position.x, transform.position.z);
            var frozen = (_actor.CombatState.Status & StatusFlags.Frozen) != 0;
            if (frozen)
            {
                // Frozen: stand still; the first frozen frame cancels a running telegraph (co-op interrupt).
                if (!_wasFrozen && _brain.Stagger(0.6f)) Debug.Log("[Avatar5.Enemy] telegraph interrupted by freeze");
                _wasFrozen = true;
                if (_agent != null && _agent.isOnNavMesh && _agent.hasPath) _agent.ResetPath();
                return;
            }
            _wasFrozen = false;
            var output = _brain.Tick(Time.deltaTime, self, _candidates);
            host.ReportEnemyTarget(_actor.ActorId, output.TargetActorId);

            if (_agent != null && _agent.isOnNavMesh)
            {
                if (output.HasDestination) _agent.SetDestination(new Vector3(output.Destination.X, transform.position.y, output.Destination.Y));
                else if (_agent.hasPath) _agent.ResetPath();
            }

            var definition = _brain.Definition;
            if (output.TelegraphStarted)
            {
                FaceTowards(output.AttackAimPoint);
                host.Simulation.RaiseTelegraph(_actor.ActorId, definition.AttackAbilityId, definition.TelegraphCueId,
                    output.AttackOrigin, output.AttackAimPoint, definition.TelegraphDuration);
            }
            if (output.ReleaseAttack)
            {
                var aim = output.AttackAimPoint;
                // Cone attacks need a direction; a target standing exactly on the brute gets a forward aim.
                if (Mathf.Abs(aim.X - output.AttackOrigin.X) + Mathf.Abs(aim.Y - output.AttackOrigin.Y) < 0.1f)
                    aim = new Float2(output.AttackOrigin.X + transform.forward.x, output.AttackOrigin.Y + transform.forward.z);
                host.Simulation.TryCast(new AbilityRequest
                {
                    ActorId = _actor.ActorId, SequenceId = ++_sequence, AbilityId = definition.AttackAbilityId, TargetOnGround = aim
                });
            }
            var replicated = (byte)output.State;
            if (_state.Value != replicated) _state.Value = replicated;
        }

        private void FaceTowards(Float2 point)
        {
            var direction = new Vector3(point.X - transform.position.x, 0f, point.Y - transform.position.z);
            if (direction.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        }
    }

    /// <summary>Runtime archetype lookup (until enemy definitions get their own assets with P04 S04-02).</summary>
    public static class EnemyCatalog
    {
        public static EnemyDefinition Get(int enemyTypeId)
        {
            switch (enemyTypeId)
            {
                case StarterEnemies.BruteTypeId: return StarterEnemies.Brute();
                default: throw new System.ArgumentException("Unknown enemy type " + enemyTypeId);
            }
        }
    }
}
