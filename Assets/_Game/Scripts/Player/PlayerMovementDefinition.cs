using Avatar5.Gameplay.Movement;
using UnityEngine;

namespace Avatar5.Player
{
    /// <summary>Authoring asset for movement balancing; converted to engine-free MovementSettings at runtime.</summary>
    [CreateAssetMenu(menuName = "Avatar5/Player Movement Definition", fileName = "PlayerMovementDefinition")]
    public sealed class PlayerMovementDefinition : ScriptableObject
    {
        [Header("Speed")]
        [SerializeField, Min(0.1f)] private float maxSpeed = 6.5f;
        [SerializeField, Min(0.1f)] private float acceleration = 55f;
        [SerializeField, Min(0.1f)] private float deceleration = 70f;
        [SerializeField, Min(1f)] private float turnSpeedDegreesPerSecond = 1080f;

        [Header("Collision")]
        [SerializeField, Min(0.05f)] private float collisionRadius = 0.4f;
        [SerializeField, Min(0.2f)] private float collisionHeight = 1.8f;

        [Header("Simulation")]
        [SerializeField, Range(0.002f, 0.1f)] private float maxSimulationStep = 1f / 60f;
        [SerializeField, Range(1, 16)] private int maxSubstepsPerFrame = 8;

        public const float DefaultCollisionRadius = 0.4f;
        public const float DefaultCollisionHeight = 1.8f;

        public float CollisionRadius { get { return collisionRadius; } }
        public float CollisionHeight { get { return collisionHeight; } }

        public static MovementSettings DefaultSettings
        {
            get { return new MovementSettings(6.5f, 55f, 70f, 1080f, 1f / 60f, 8); }
        }

        public MovementSettings ToSettings()
        {
            return new MovementSettings(maxSpeed, acceleration, deceleration, turnSpeedDegreesPerSecond,
                maxSimulationStep, maxSubstepsPerFrame);
        }
    }
}
