using System;
using Avatar5.Contracts;

namespace Avatar5.Gameplay.Movement
{
    /// <summary>Engine-free XZ-plane math shared by input, motor and aim. Yaw is degrees clockwise from +Z.</summary>
    public static class PlanarMath
    {
        public const float Epsilon = 1e-5f;
        private const float Deg2Rad = (float)(Math.PI / 180.0);
        private const float Rad2Deg = (float)(180.0 / Math.PI);

        public static float Length(Float2 v)
        {
            return (float)Math.Sqrt(v.X * v.X + v.Y * v.Y);
        }

        public static bool IsZero(Float2 v)
        {
            return v.X * v.X + v.Y * v.Y <= Epsilon * Epsilon;
        }

        public static Float2 Scale(Float2 v, float s)
        {
            return new Float2(v.X * s, v.Y * s);
        }

        public static Float2 Add(Float2 a, Float2 b)
        {
            return new Float2(a.X + b.X, a.Y + b.Y);
        }

        public static Float2 Normalize(Float2 v)
        {
            var length = Length(v);
            return length <= Epsilon ? new Float2(0f, 0f) : Scale(v, 1f / length);
        }

        /// <summary>Clamps to length 1 so diagonal keyboard input is never faster than axial input.</summary>
        public static Float2 ClampToUnit(Float2 v)
        {
            var length = Length(v);
            return length > 1f ? Scale(v, 1f / length) : v;
        }

        public static Float2 MoveTowards(Float2 current, Float2 target, float maxDelta)
        {
            var delta = new Float2(target.X - current.X, target.Y - current.Y);
            var distance = Length(delta);
            if (distance <= maxDelta || distance <= Epsilon) return target;
            return Add(current, Scale(delta, maxDelta / distance));
        }

        public static float YawFromDirection(Float2 direction)
        {
            return (float)Math.Atan2(direction.X, direction.Y) * Rad2Deg;
        }

        public static Float2 DirectionFromYaw(float yawDegrees)
        {
            var radians = yawDegrees * Deg2Rad;
            return new Float2((float)Math.Sin(radians), (float)Math.Cos(radians));
        }

        /// <summary>Signed shortest difference target - current in (-180, 180].</summary>
        public static float DeltaAngle(float current, float target)
        {
            var delta = (target - current) % 360f;
            if (delta > 180f) delta -= 360f;
            if (delta <= -180f) delta += 360f;
            return delta;
        }

        public static float MoveTowardsAngle(float current, float target, float maxDelta)
        {
            var delta = DeltaAngle(current, target);
            if (Math.Abs(delta) <= maxDelta) return Normalize360(target);
            return Normalize360(current + Math.Sign(delta) * maxDelta);
        }

        public static float Normalize360(float degrees)
        {
            var result = degrees % 360f;
            return result < 0f ? result + 360f : result;
        }

        /// <summary>Rotates a screen-style input (x = right, y = up/forward) by the camera yaw onto the XZ plane.</summary>
        public static Float2 RotateByYaw(Float2 input, float yawDegrees)
        {
            var radians = yawDegrees * Deg2Rad;
            var sin = (float)Math.Sin(radians);
            var cos = (float)Math.Cos(radians);
            return new Float2(input.X * cos + input.Y * sin, -input.X * sin + input.Y * cos);
        }
    }
}
