// Minimal UnityEngine shims so we can compile and exercise pure-logic source files
// outside the Unity editor. Only what's referenced by linked sources lives here.
using System;
using System.Collections.Generic;

namespace UnityEngine
{
    public struct Vector2 : IEquatable<Vector2>
    {
        public float x;
        public float y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 zero => new Vector2(0f, 0f);
        public static Vector2 Min(Vector2 a, Vector2 b) => new Vector2(Math.Min(a.x, b.x), Math.Min(a.y, b.y));
        public static Vector2 Max(Vector2 a, Vector2 b) => new Vector2(Math.Max(a.x, b.x), Math.Max(a.y, b.y));
        public static Vector2 operator -(Vector2 a, Vector2 b) => new Vector2(a.x - b.x, a.y - b.y);
        public bool Equals(Vector2 o) => x == o.x && y == o.y;
        public override bool Equals(object o) => o is Vector2 v && Equals(v);
        public override int GetHashCode() => HashCode.Combine(x, y);
        public override string ToString() => $"({x}, {y})";
    }

    public static class Mathf
    {
        public const float Deg2Rad = (float)(Math.PI / 180.0);
        public const float Rad2Deg = (float)(180.0 / Math.PI);
        public static float Pow(float b, float e) => (float)Math.Pow(b, e);
        public static float Max(float a, float b) => Math.Max(a, b);
        public static float Min(float a, float b) => Math.Min(a, b);
        public static float Abs(float v) => Math.Abs(v);
        public static float Sqrt(float v) => (float)Math.Sqrt(v);
        public static float Exp(float v) => (float)Math.Exp(v);
        public static float Cos(float v) => (float)Math.Cos(v);
        public static float Sin(float v) => (float)Math.Sin(v);
        public static float Acos(float v) => (float)Math.Acos(v);
        public static float Clamp(float v, float lo, float hi) => Math.Max(lo, Math.Min(hi, v));
        public static float Clamp01(float v) => Math.Max(0f, Math.Min(1f, v));
        public static float MoveTowards(float current, float target, float maxDelta)
        {
            if (Math.Abs(target - current) <= maxDelta) return target;
            return current + Math.Sign(target - current) * maxDelta;
        }
        public static int FloorToInt(float v) => (int)Math.Floor(v);
    }

    public class Object
    {
        public string name = "";
        public override string ToString() => string.IsNullOrEmpty(name) ? base.ToString() : name;
        public static void DestroyImmediate(Object _) { /* no-op outside Unity */ }
    }

    public class MonoBehaviour : Object
    {
        // Test harness invokes Awake() manually after construction.
        internal void InvokeAwake()
        {
            var m = GetType().GetMethod("Awake",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Public);
            m?.Invoke(this, null);
        }
    }

    public class ScriptableObject : Object
    {
        public static T CreateInstance<T>() where T : ScriptableObject, new() => new T();
    }

    public class GameObject : Object { public GameObject(string n) { name = n; } public GameObject() { } }
    public class Transform { }
    public class Material { }
    public class Mesh { public string name; }
    public class MeshFilter { }
    public class MeshRenderer { }
    public class BoxCollider { }
    public class Vector3 { }
    public class Quaternion { }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class SerializeField : Attribute { }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class HeaderAttribute : Attribute { public HeaderAttribute(string _) { } }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class CreateAssetMenuAttribute : Attribute
    {
        public string fileName { get; set; }
        public string menuName { get; set; }
    }

    namespace Rendering
    {
        public enum IndexFormat { UInt16, UInt32 }
    }
}
