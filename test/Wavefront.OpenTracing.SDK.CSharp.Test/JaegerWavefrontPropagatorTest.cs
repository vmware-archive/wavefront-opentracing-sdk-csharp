using OpenTracing.Propagation;
using System;
using System.Collections.Generic;
using Wavefront.OpenTracing.SDK.CSharp.Propagation;
using Xunit;

namespace Wavefront.OpenTracing.SDK.CSharp.Test
{
    /// <summary>
    ///     Unit tests for <see cref="JaegerWavefrontPropagator" />.
    /// </summary>
    public class JaegerWavefrontPropagatorTest
    {
        private static readonly string jaegerHeader = "uber-trace-id";

        private readonly JaegerWavefrontPropagator wfJaegerPropagator =
            new JaegerWavefrontPropagator.Builder()
                .WithBaggagePrefix("uberctx-")
                .WithTraceIdHeader(jaegerHeader)
                .Build();

        [Fact]
        public void TestTraceIdExtract()
        {
            string val = "3871de7e09c53ae8:7499dd16d98ab60e:3771de7e09c55ae8:1";
            var dictionary = new Dictionary<string, string>();
            dictionary.Add(jaegerHeader, val);
            var textMapExtractAdapter = new TextMapExtractAdapter(dictionary);
            WavefrontSpanContext ctx = wfJaegerPropagator.Extract(textMapExtractAdapter);
            Assert.NotNull(ctx);
            Assert.Equal("00000000-0000-0000-3871-de7e09c53ae8", ctx.TraceId);
            Assert.Equal("00000000-0000-0000-7499-dd16d98ab60e", ctx.SpanId);
            Assert.Equal("00000000-0000-0000-7499-dd16d98ab60e", ctx.GetBaggageItem("parent-id"));
            Assert.True(ctx.GetSamplingDecision() ?? false);
        }

        [Fact]
        public void TestInvalidTraceIdExtract()
        {
            string val = ":7499dd16d98ab60e:3771de7e09c55ae8:1";
            var dictionary = new Dictionary<string, string>();
            dictionary.Add(jaegerHeader, val);
            var textMapExtractAdapter = new TextMapExtractAdapter(dictionary);
            WavefrontSpanContext ctx = wfJaegerPropagator.Extract(textMapExtractAdapter);
            Assert.Null(ctx);
        }

        [Fact]
        public void TestTraceIdInject()
        {
            var traceId = Guid.Parse("00000000-0000-0000-3871-de7e09c53ae8");
            var spanId = Guid.Parse("00000000-0000-0000-7499-dd16d98ab60e");
            var dictionary = new Dictionary<string, string>();
            var textMapInjectAdapter = new TextMapInjectAdapter(dictionary);
            wfJaegerPropagator.Inject(new WavefrontSpanContext(traceId, spanId, null, true),
                textMapInjectAdapter);
            Assert.True(dictionary.ContainsKey(jaegerHeader));
            Assert.Equal("3871de7e09c53ae8:7499dd16d98ab60e::1", dictionary[jaegerHeader]);
        }

        [Fact]
        public void TestHexToGuid()
        {
            string hex = "ef27b4b9f6e946f5ab2b47bbb24746c5";
            Guid? guid = JaegerWavefrontPropagator.HexToGuid(hex);
            Assert.Equal("ef27b4b9-f6e9-46f5-ab2b-47bbb24746c5",
                guid.HasValue ? guid.Value.ToString() : null);
        }

        [Fact]
        public void TestGuidToHex()
        {
            Guid guid = Guid.Parse("ef27b4b9-f6e9-46f5-ab2b-47bbb24746c5");
            string hex = JaegerWavefrontPropagator.GuidToHex(guid);
            Assert.Equal("ef27b4b9f6e946f5ab2b47bbb24746c5", hex);
        }
    }
}
