using System.Runtime.CompilerServices;

namespace Netamite.Datatype {
    [System.Serializable]
    public class Color {
        public float r, g, b, a;


        public static Color red { get { return new Color(1f, 0f, 0f, 1f); } }
        public static Color green { get { return new Color(0f, 1f, 0f, 1f); } }
        public static Color blue { get { return new Color(0f, 0f, 1f, 1f); } }
        public static Color white { get { return new Color(1f, 1f, 1f, 1f); } }
        public static Color black { get { return new Color(0f, 0f, 0f, 1f); } }
        public static Color yellow { get { return new Color(1f, 47f / 51f, 4f / 255f, 1f); } }
        public static Color cyan { get { return new Color(0f, 1f, 1f, 1f); } }
        public static Color magenta { get { return new Color(1f, 0f, 1f, 1f); } }
        public static Color gray { get { return new Color(0.5f, 0.5f, 0.5f, 1f); } }
        public static Color grey { get { return new Color(0.5f, 0.5f, 0.5f, 1f); } }
        public static Color clear { get { return new Color(0f, 0f, 0f, 0f); } }
        public float grayscale { get { return 0.299f * r + 0.587f * g + 0.114f * b; } }

        public Color(float r, float g, float b) {
            this.r = r;
            this.g = g;
            this.b = b;
        }

        public Color(float r, float g, float b, float a) : this(r, g, b) {
            this.a = a;
        }

#if UNITY
        public static implicit operator Color(UnityEngine.Color n) {
            return new Color(n.r, n.g, n.b);
        }


        public static explicit operator UnityEngine.Color(Color o) {
            return new UnityEngine.Color(o.r, o.g, o.b);
        }
#endif
    }
}
