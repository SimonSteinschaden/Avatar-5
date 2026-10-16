using Avatar5.Contracts;
using UnityEngine;

namespace Avatar5.Foundation
{
    /// <summary>Maps engine-free contract vectors to Unity types. Float2 is a world XZ vector (X → x, Y → z).</summary>
    public static class ContractVectorExtensions
    {
        public static Vector2 ToVector2(this Float2 value) { return new Vector2(value.X, value.Y); }
        public static Float2 ToFloat2(this Vector2 value) { return new Float2(value.x, value.y); }

        public static Vector3 ToVector3(this Float3 value) { return new Vector3(value.X, value.Y, value.Z); }
        public static Float3 ToFloat3(this Vector3 value) { return new Float3(value.x, value.y, value.z); }

        public static Vector3 ToWorldXZ(this Float2 value, float y = 0f) { return new Vector3(value.X, y, value.Y); }
        public static Float2 ToPlanar(this Vector3 value) { return new Float2(value.x, value.z); }
    }
}
