using Wavefront.OpenTracing.SDK.CSharp.Reporting;
using Wavefront.SDK.CSharp.Common.Application;
using Xunit;
using static Wavefront.OpenTracing.SDK.CSharp.Common.Constants;

namespace Wavefront.OpenTracing.SDK.CSharp.Test
{
    /// <summary>
    ///     Unit tests for <see cref="WavefrontSpanBuilder"/>.
    /// </summary>
    public class WavefrontSpanBuilderTest
    {
        [Fact]
        public void TestIgnoreActiveSpan()
        {
            var tracer = new WavefrontTracer
                .Builder(new ConsoleReporter(DefaultSource), BuildApplicationTags())
                .Build();
            var scope = tracer.BuildSpan("testOp").StartActive(true);
            var activeSpan = scope.Span;

            // Span created without invoking IgnoreActiveSpan() on WavefrontSpanBuilder
            var childSpan = tracer.BuildSpan("childOp").Start();
            string activeTraceId =
                ((WavefrontSpanContext)activeSpan.Context).GetTraceId().ToString();
            string childTraceId =
                ((WavefrontSpanContext)childSpan.Context).GetTraceId().ToString();
            Assert.Equal(activeTraceId, childTraceId);

            // Span created with IgnoreActiveSpan() on WavefrontSpanBuilder
            childSpan = tracer.BuildSpan("childOp").IgnoreActiveSpan().Start();
            childTraceId = ((WavefrontSpanContext)childSpan.Context).GetTraceId().ToString();
            Assert.NotEqual(activeTraceId, childTraceId);
        }

        [Fact]
        public void TestMultiValuedTags()
        {
            var tracer = new WavefrontTracer
                .Builder(new ConsoleReporter(DefaultSource), BuildApplicationTags())
                .Build();
            var span = (WavefrontSpan)tracer.BuildSpan("testOp")
                                            .WithTag("key1", "value1")
                                            .WithTag("key1", "value2")
                                            .Start();

            Assert.NotNull(span);
            var spanTags = span.GetTagsAsMap();
            Assert.NotNull(spanTags);
            Assert.Equal(5, spanTags.Count);
            Assert.Contains("value1", spanTags["key1"]);
            Assert.Contains("value2", spanTags["key1"]);
        }

        private static ApplicationTags BuildApplicationTags()
        {
            return new ApplicationTags.Builder("myApplication", "myService").Build();
        }
    }
}
