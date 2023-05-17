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
            return $"{x}; {y}; {z}";
        }


        public static Vector3 zero { get { return new Vector3(0, 0, 0); } }
        public static Vector3 one { get { return new Vector3(1, 1, 1); } }

        public static Vector3 operator *(float d, Vector3 a) {
            return new Vector3(a.x * d, a.y * d, a.z * d);
        }

        public static Vector3 operator /(Vector3 a, float d) {
            return new Vector3(a.x / d, a.y / d, a.z / d);
        }
    }
}
