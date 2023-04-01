using System;
using System.Runtime.CompilerServices;

namespace Netamite.Datatype {
    [System.Serializable]
    public class Vector3 {
        public float x, y, z;

        public Vector3(float x, float y, float z) {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public override string ToString() {
            return $"{x},{y},{z}";
        }


        public static Vector3 zero { get { return new Vector3(0, 0, 0); } }
        public static Vector3 one { get { return new Vector3(1, 1, 1); } }

        public static Vector3 operator *(float d, Vector3 a) {
            return new Vector3(a.x * d, a.y * d, a.z * d);
        }

        public static Vector3 operator /(Vector3 a, float d) {
            return new Vector3(a.x / d, a.y / d, a.z / d);
        }

#if UNITY
        public static implicit operator Vector3(UnityEngine.Vector3 n) {
            return new Vector3(n.x, n.y, n.z);
        }


        public static explicit operator UnityEngine.Vector3(Vector3 o) {
            return new UnityEngine.Vector3(o.x, o.y, o.z);
        }
#endif
    }
}
