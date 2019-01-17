using System;
using Xunit;
using static Wavefront.OpenTracing.SDK.CSharp.Common.Utils;

namespace Wavefront.OpenTracing.SDK.CSharp.Test
{
    /// <summary>
    ///     Unit tests for Utils.
    /// </summary>
    public class UtilsTest
    {
        [Fact]
        public void TestTraceIdToLong()
        {
            Guid guid = Guid.Parse("11223344-5566-7788-9900-AABBCCDDEEFF");
            Assert.Equal(Convert.ToInt64("9900AABBCCDDEEFF", 16), TraceIdToLong(guid));
        }

        [Fact]
        public void TestUnixTimeMicroseconds()
        {
            DateTime utcTimestamp = new DateTime(2019, 1, 1, 23, 59, 59, 999, DateTimeKind.Utc);
            Assert.Equal(1546387199999000L, UnixTimeMicroseconds(utcTimestamp));

            utcTimestamp = new DateTime(636818976012345670L);
            Assert.Equal(1546300801234567L, UnixTimeMicroseconds(utcTimestamp));
        }

        [Fact]
        public void TestTimeSpanToMicroseconds()
        {
            DateTime timestamp1 = new DateTime(636818976012345670L);
            DateTime timestamp2 = new DateTime(636819839999990000L);
            TimeSpan duration = timestamp2 - timestamp1;
            Assert.Equal(86398764433L, TimeSpanToMicroseconds(duration));
        }
    }
}
