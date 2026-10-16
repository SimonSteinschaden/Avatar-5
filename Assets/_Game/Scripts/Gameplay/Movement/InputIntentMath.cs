using Avatar5.Contracts;

namespace Avatar5.Gameplay.Movement
{
    /// <summary>Turns raw device values into normalized, camera-relative world intents (all engine-free).</summary>
    public static class InputIntentMath
    {
        /// <summary>Applies a radial dead zone and clamps to unit length.</summary>
        public static Float2 NormalizeStick(Float2 raw, float deadZone)
        {
            return PlanarMath.Length(raw) < deadZone ? new Float2(0f, 0f) : PlanarMath.ClampToUnit(raw);
        }

        /// <summary>Normalized move stick/keys mapped to a world XZ intent relative to the camera yaw.</summary>
        public static Float2 CameraRelativeMove(Float2 raw, float cameraYawDegrees, float deadZone)
        {
            return PlanarMath.ClampToUnit(PlanarMath.RotateByYaw(NormalizeStick(raw, deadZone), cameraYawDegrees));
        }

        /// <summary>Right-stick aim as a unit world direction, or zero inside the dead zone.</summary>
        public static Float2 CameraRelativeAim(Float2 rawStick, float cameraYawDegrees, float deadZone)
        {
            var stick = NormalizeStick(rawStick, deadZone);
            return PlanarMath.IsZero(stick)
                ? new Float2(0f, 0f)
                : PlanarMath.Normalize(PlanarMath.RotateByYaw(stick, cameraYawDegrees));
        }

        /// <summary>Intersects a world ray with the horizontal plane y = planeY.</summary>
        public static bool TryProjectRayOnGround(Float3 origin, Float3 direction, float planeY, out Float3 hit)
        {
            hit = default;
            if (direction.Y > -PlanarMath.Epsilon && direction.Y < PlanarMath.Epsilon) return false;
            var distance = (planeY - origin.Y) / direction.Y;
            if (distance < 0f) return false;
            hit = new Float3(origin.X + direction.X * distance, planeY, origin.Z + direction.Z * distance);
            return true;
        }

        /// <summary>Unit XZ direction from actor to aim point, or zero if the point is (nearly) on the actor.</summary>
        public static Float2 AimDirection(Float3 from, Float3 to, float minDistance)
        {
            var delta = new Float2(to.X - from.X, to.Z - from.Z);
            return PlanarMath.Length(delta) < minDistance ? new Float2(0f, 0f) : PlanarMath.Normalize(delta);
        }
    }
}
