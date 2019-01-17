using System;
namespace Wavefront.OpenTracing.SDK.CSharp.Common
{
    /// <summary>
    ///     Common Util methods
    /// </summary>
    public static class Utils
    {
        private static readonly long UnixEpochTicks =
            new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;
        private static readonly long TicksPerMicrosecond = TimeSpan.TicksPerMillisecond / 1000;
        private static readonly long UnixEpochMicroseconds = UnixEpochTicks / TicksPerMicrosecond;

        /// <summary>
        ///     Converts a trace id from a 128-bit guid to an 64-bit long by taking the least
        ///     significant bits.
        /// </summary>
        /// <returns>The trace id as a long.</returns>
        /// <param name="traceId">The trace id as a guid.</param>
        public static long TraceIdToLong(Guid traceId)
        {
            string leastSignificantHex = traceId.ToString().Replace("-", "").Substring(16);
            return Convert.ToInt64(leastSignificantHex, 16);
        }

        /// <summary>
        ///     Converts a UTC timestamp to the number of microseconds elapsed since the Unix epoch.
        /// </summary>
        /// <returns>The timestamp as microseconds elapsed since the epoch.</returns>
        /// <param name="utcTimestamp">A UTC timestamp.</param>
        public static long UnixTimeMicroseconds(DateTime utcTimestamp)
        {
            long microseconds = utcTimestamp.Ticks / TicksPerMicrosecond;
            return microseconds - UnixEpochMicroseconds;
        }

        /// <summary>
        ///     Converts a duration from TimeSpan to microseconds elapsed.
        /// </summary>
        /// <returns>The duration in microseconds.</returns>
        /// <param name="duration">The duration as a TimeSpan.</param>
        public static long TimeSpanToMicroseconds(TimeSpan duration)
        {
            return duration.Ticks / TicksPerMicrosecond;
        }
    }
}
