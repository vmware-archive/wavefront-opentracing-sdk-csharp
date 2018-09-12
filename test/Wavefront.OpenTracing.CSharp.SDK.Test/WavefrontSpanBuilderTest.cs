using Xunit;

namespace Wavefront.OpenTracing.CSharp.SDK.Test
{
    /// <summary>
    ///     Unit tests for <see cref="WavefrontSpanBuilder"/>.
    /// </summary>
    public class WavefrontSpanBuilderTest
    {
        [Fact]
        public void TestIgnoreActiveSpan()
        {
            var tracer = new WavefrontTracer.Builder().Build();
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
            var tracer = new WavefrontTracer.Builder().Build();
            var span = (WavefrontSpan)tracer.BuildSpan("testOp")
                                            .WithTag("key1", "value1")
                                            .WithTag("key1", "value2")
                                            .Start();

            Assert.NotNull(span);
            Assert.NotNull(span.GetTagsAsMap());
            Assert.Equal(1, span.GetTagsAsMap().Count);
            Assert.Contains("value1", span.GetTagsAsMap()["key1"]);
            Assert.Contains("value2", span.GetTagsAsMap()["key1"]);
        }
    }
}
