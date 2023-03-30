using System;

namespace Netamite.Helper {
    public class TimeHelper {
        public static long Ticks { get { return DateTime.UtcNow.Ticks; } }
        public static long Millis { get { return Ticks / TimeSpan.TicksPerMillisecond; } }
    }
}
