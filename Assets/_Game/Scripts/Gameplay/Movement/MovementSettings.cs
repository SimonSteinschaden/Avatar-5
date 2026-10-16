using System;

namespace Avatar5.Gameplay.Movement
{
    /// <summary>Validated runtime copy of PlayerMovementDefinition (balancing data lives in the asset, not here).</summary>
    [Serializable]
    public struct MovementSettings
    {
        public float MaxSpeed;
        public float Acceleration;
        public float Deceleration;
        public float TurnSpeedDegreesPerSecond;
        public float MaxSimulationStep;
        public int MaxSubstepsPerFrame;

        public MovementSettings(float maxSpeed, float acceleration, float deceleration,
            float turnSpeedDegreesPerSecond, float maxSimulationStep, int maxSubstepsPerFrame)
        {
            MaxSpeed = maxSpeed;
            Acceleration = acceleration;
            Deceleration = deceleration;
            TurnSpeedDegreesPerSecond = turnSpeedDegreesPerSecond;
            MaxSimulationStep = maxSimulationStep;
            MaxSubstepsPerFrame = maxSubstepsPerFrame;
        }

        public bool IsValid(out string error)
        {
            error = null;
            if (!(MaxSpeed > 0f)) error = "MaxSpeed must be > 0";
            else if (!(Acceleration > 0f)) error = "Acceleration must be > 0";
            else if (!(Deceleration > 0f)) error = "Deceleration must be > 0";
            else if (!(TurnSpeedDegreesPerSecond > 0f)) error = "TurnSpeedDegreesPerSecond must be > 0";
            else if (!(MaxSimulationStep > 0f)) error = "MaxSimulationStep must be > 0";
            else if (MaxSubstepsPerFrame < 1) error = "MaxSubstepsPerFrame must be >= 1";
            return error == null;
        }

        public void Validate()
        {
            string error;
            if (!IsValid(out error)) throw new ArgumentException("Invalid MovementSettings: " + error);
        }
    }
}
