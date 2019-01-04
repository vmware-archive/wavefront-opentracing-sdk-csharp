using System;
using OpenTracing.Tag;
using Wavefront.OpenTracing.SDK.CSharp.Common;
using Wavefront.OpenTracing.SDK.CSharp.Reporting;
using Wavefront.OpenTracing.SDK.CSharp.Sampling;
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

        [Fact]
        public void TestForcedSampling()
        {
            // Create tracer with constant sampler set to false
            var tracer = new WavefrontTracer
                .Builder(new ConsoleReporter(DefaultSource), BuildApplicationTags())
                .WithSampler(new ConstantSampler(false))
                .Build();

            var span = (WavefrontSpan)tracer.BuildSpan("testOp").Start();
            Assert.NotNull(span);
            Assert.NotNull(span.Context);
            bool? samplingDecision = ((WavefrontSpanContext)span.Context).GetSamplingDecision();
            Assert.True(samplingDecision.HasValue);
            Assert.False(samplingDecision.Value);

            Tags.SamplingPriority.Set(span, 1);
            samplingDecision = ((WavefrontSpanContext)span.Context).GetSamplingDecision();
            Assert.True(samplingDecision.HasValue);
            Assert.True(samplingDecision.Value);

            span = (WavefrontSpan)tracer.BuildSpan("testOp").Start();
            Assert.NotNull(span);
            Assert.NotNull(span.Context);
            samplingDecision = ((WavefrontSpanContext)span.Context).GetSamplingDecision();
            Assert.True(samplingDecision.HasValue);
            Assert.False(samplingDecision.Value);

            Tags.Error.Set(span, true);
            samplingDecision = ((WavefrontSpanContext)span.Context).GetSamplingDecision();
            Assert.True(samplingDecision.HasValue);
            Assert.True(samplingDecision.Value);
        }

        [Fact]
        public void TestRootSampling()
        {
            // Create tracer with constant sampler set to false
            var tracer = new WavefrontTracer
                .Builder(new ConsoleReporter(DefaultSource), BuildApplicationTags())
                .WithSampler(new ConstantSampler(false))
                .Build();

            var span = (WavefrontSpan)tracer.BuildSpan("testOp").Start();
            Assert.NotNull(span);
            Assert.NotNull(span.Context);
            Assert.Empty(span.GetParents());
            Assert.Empty(span.GetFollows());
            Assert.True(((WavefrontSpanContext)span.Context).IsSampled());
            bool? samplingDecision = ((WavefrontSpanContext)span.Context).GetSamplingDecision();
            Assert.True(samplingDecision.HasValue);
            Assert.False(samplingDecision.Value);

            // Create tracer with constant sampler set to false
            tracer = new WavefrontTracer
                .Builder(new ConsoleReporter(DefaultSource), BuildApplicationTags())
                .WithSampler(new ConstantSampler(true))
                .Build();

            span = (WavefrontSpan)tracer.BuildSpan("testOp").Start();
            Assert.NotNull(span);
            Assert.NotNull(span.Context);
            Assert.Empty(span.GetParents());
            Assert.Empty(span.GetFollows());
            Assert.True(((WavefrontSpanContext)span.Context).IsSampled());
            samplingDecision = ((WavefrontSpanContext)span.Context).GetSamplingDecision();
            Assert.True(samplingDecision.HasValue);
            Assert.True(samplingDecision.Value);
        }

        [Fact]
        public void TestPositiveChildSampling()
        {
            // Create tracer with constant sampler set to false
            var tracer = new WavefrontTracer
                .Builder(new ConsoleReporter(DefaultSource), BuildApplicationTags())
                .WithSampler(new ConstantSampler(false))
                .Build();

            // Create parentContext with sampled set to true
            var parentContext =
                new WavefrontSpanContext(Guid.NewGuid(), Guid.NewGuid(), null, true);

            // Verify span created AsChildOf parentContext inherits parent sampling decision
            var span = (WavefrontSpan)tracer.BuildSpan("testOp").AsChildOf(parentContext).Start();

            var spanContext = (WavefrontSpanContext)span.Context;
            long traceId = Utils.TraceIdToLong(spanContext.GetTraceId());
            Assert.False(tracer.Sample(span.GetOperationName(), traceId, 0));
            Assert.NotNull(span);
            Assert.Equal(parentContext.TraceId, spanContext.TraceId);
            Assert.True(spanContext.IsSampled());
            bool? samplingDecision = spanContext.GetSamplingDecision();
            Assert.True(samplingDecision.HasValue);
            Assert.True(samplingDecision.Value);
        }

        [Fact]
        public void TestNegativeChildSampling()
        {
            // Create tracer with constant sampler set to true
            var tracer = new WavefrontTracer
                .Builder(new ConsoleReporter(DefaultSource), BuildApplicationTags())
                .WithSampler(new ConstantSampler(true))
                .Build();

            // Create parentContext with sampled set to false
            var parentContext =
                new WavefrontSpanContext(Guid.NewGuid(), Guid.NewGuid(), null, false);

            // Verify span created AsChildOf parentContext inherits parent sampling decision
            var span = (WavefrontSpan)tracer.BuildSpan("testOp").AsChildOf(parentContext).Start();

            var spanContext = (WavefrontSpanContext)span.Context;
            long traceId = Utils.TraceIdToLong(spanContext.GetTraceId());
            Assert.True(tracer.Sample(span.GetOperationName(), traceId, 0));
            Assert.NotNull(span);
            Assert.Equal(parentContext.TraceId, spanContext.TraceId);
            Assert.True(spanContext.IsSampled());
            bool? samplingDecision = spanContext.GetSamplingDecision();
            Assert.True(samplingDecision.HasValue);
            Assert.False(samplingDecision.Value);
        }

        private static ApplicationTags BuildApplicationTags()
        {
            return new ApplicationTags.Builder("myApplication", "myService").Build();
        }
    }
}
