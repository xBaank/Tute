

using UnityEngine;

namespace Assets.Scripts.Extensions
{
    public static class Vector3Extensions
    {
        public static Vector2 ToVector2(this Vector3 v) => new(v.x, v.y);
        public static Vector3 ToVector3(this Vector2 v) => new(v.x, v.y);
    }
}
