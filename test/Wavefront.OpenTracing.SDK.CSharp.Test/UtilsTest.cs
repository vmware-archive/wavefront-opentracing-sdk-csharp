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
    }
}
