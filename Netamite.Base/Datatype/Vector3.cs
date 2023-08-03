using System;

namespace Netamite.Datatype {
    [System.Serializable]
    public class Vector3 {
        public float x, y, z;

        public Vector3(float x, float y, float z) {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public Vector3() {

        }

        public override string ToString() {
            return $"{x}; {y}; {z}";
        }


        public static Vector3 zero { get { return new Vector3(0, 0, 0); } }
        public static Vector3 one { get { return new Vector3(1, 1, 1); } }

        public static Vector3 operator *(Vector3 a, float d) {
            return new Vector3(a.x * d, a.y * d, a.z * d);
        }

        public static Vector3 operator /(Vector3 a, float d) {
            return new Vector3(a.x / d, a.y / d, a.z / d);
        }

        public static bool operator ==(Vector3 a, Vector3 b) {
            return a.x == b.x && a.y == b.y && a.z == b.z;
        }
        public static bool operator !=(Vector3 a, Vector3 b) {
            return !(a == b);
        }

        public void NormalizeAngles() {
            x = NormalizeAngle(x);
            y = NormalizeAngle(y);
            z = NormalizeAngle(z);
        }

        private static float NormalizeAngle(float angle) {
            while(angle > 360)
                angle -= 360;
            while(angle < 0)
                angle += 360;
            return angle;
        }
    }
}
