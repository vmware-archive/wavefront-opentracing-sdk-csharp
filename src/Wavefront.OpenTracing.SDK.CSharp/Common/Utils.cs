using System;

namespace Wavefront.OpenTracing.SDK.CSharp.Common
{
    /// <summary>
    ///     Common Util methods
    /// </summary>
    public static class Utils
    {
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
    }
}
