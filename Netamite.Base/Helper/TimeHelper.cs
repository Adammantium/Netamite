using System;

namespace Netamite.Helper {
    public class TimeHelper {
        public static long Ticks { get { return DateTime.UtcNow.Ticks; } }
        public static long Millis { get { return Ticks / TimeSpan.TicksPerMillisecond; } }

        public const long SECOND = 1000;
        public const long MINUTE = 60 * SECOND;
        public const long HOUR   = 60 * MINUTE;
        public const long DAY    = 24 * HOUR;
    }
}
