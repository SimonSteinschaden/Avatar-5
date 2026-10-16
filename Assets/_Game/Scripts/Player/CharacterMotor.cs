using System;
using Avatar5.Contracts;
using Avatar5.Foundation;
using Avatar5.Gameplay.Movement;
using UnityEngine;

namespace Avatar5.Player
{
    /// <summary>
    /// Unity adapter around <see cref="PlayerMotorCore"/>: CharacterController collision, gravity and transform.
    /// Receives intents only through <see cref="IPlayerCommandSink"/>; knows nothing about devices, HUD or sessions.
    /// In P01 it simulates locally (acting as its own host); P03 decides who runs it on the network.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [DisallowMultipleComponent]
    public sealed class CharacterMotor : MonoBehaviour, IPlayerCommandSink, IPlayerPoseSource, IPlanarMover
    {
        [SerializeField] private PlayerMovementDefinition definition;
        [SerializeField] private ulong actorId = 1;
        [SerializeField] private bool simulateInUpdate = true;
        [SerializeField] private float gravity = -25f;

        private CharacterController _controller;
        private PlayerMotorCore _core;
        private float _verticalVelocity;

        /// <summary>Raised after a hard reposition (respawn); argument is the position delta.</summary>
        public event Action<Vector3> Teleported;

        public ulong ActorId { get { return actorId; } }
        public PlayerMotorCore Core { get { EnsureInitialized(); return _core; } }
        public bool SimulateInUpdate { get { return simulateInUpdate; } set { simulateInUpdate = value; } }
        public bool IsGrounded { get { return _controller != null && _controller.isGrounded; } }

        public PlayerPose CurrentPose
        {
            get
            {
                EnsureInitialized();
                return new PlayerPose
                {
                    ActorId = actorId,
                    LastProcessedSequenceId = _core.LastProcessedSequenceId,
                    Position = transform.position.ToFloat3(),
                    Velocity = new Float3(_core.Velocity.X, _verticalVelocity, _core.Velocity.Y),
                    FacingYawDegrees = _core.FacingYawDegrees,
                    AimDirection = _core.AimDirection
                };
            }
        }

        /// <summary>Configures an instance created from code (tests, spawners) before its first simulation.</summary>
        public void Configure(PlayerMovementDefinition movementDefinition, ulong id)
        {
            definition = movementDefinition;
            actorId = id;
            _core = null;
            EnsureInitialized();
        }

        public RequestStatus Submit(in PlayerCommand command)
        {
            EnsureInitialized();
            return _core.Submit(command);
        }

        public void Simulate(float deltaTime)
        {
            EnsureInitialized();
            if (!(deltaTime > 0f) || !_controller.enabled) return;

            _core.Advance(deltaTime, this);

            _verticalVelocity = _controller.isGrounded ? -1f : _verticalVelocity + gravity * deltaTime;
            _controller.Move(new Vector3(0f, _verticalVelocity * Mathf.Min(deltaTime, 0.1f), 0f));

            transform.rotation = Quaternion.Euler(0f, _core.FacingYawDegrees, 0f);
        }

        /// <summary>Hard reposition for spawn/respawn: clears velocity and notifies listeners such as the camera.</summary>
        public void Teleport(Vector3 position, float yawDegrees)
        {
            EnsureInitialized();
            var delta = position - transform.position;
            _controller.enabled = false;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yawDegrees, 0f));
            _controller.enabled = true;
            _core.Halt();
            _verticalVelocity = 0f;
            Teleported?.Invoke(delta);
        }

        Float2 IPlanarMover.Move(Float2 desiredDisplacement)
        {
            var before = transform.position;
            _controller.Move(desiredDisplacement.ToWorldXZ());
            return (transform.position - before).ToPlanar();
        }

        private void Awake()
        {
            EnsureInitialized();
        }

        private void Update()
        {
            if (simulateInUpdate) Simulate(Time.deltaTime);
        }

        private void EnsureInitialized()
        {
            if (_core != null) return;
            _controller = GetComponent<CharacterController>();
            var settings = definition != null ? definition.ToSettings() : PlayerMovementDefinition.DefaultSettings;
            var radius = definition != null ? definition.CollisionRadius : PlayerMovementDefinition.DefaultCollisionRadius;
            var height = definition != null ? definition.CollisionHeight : PlayerMovementDefinition.DefaultCollisionHeight;
            _controller.radius = radius;
            _controller.height = height;
            _controller.center = new Vector3(0f, height * 0.5f, 0f);
            // Default 0.001 m silently drops per-frame moves at high frame rates (6 m/s at 6000+ FPS).
            _controller.minMoveDistance = 0f;

            _core = new PlayerMotorCore(actorId, settings, transform.eulerAngles.y);
        }
    }
}
