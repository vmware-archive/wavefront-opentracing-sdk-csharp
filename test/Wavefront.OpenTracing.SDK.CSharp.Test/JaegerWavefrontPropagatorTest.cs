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
        private static readonly string BaggagePrefix = "uberctx-";
        private static readonly string JaegerHeader = "uber-trace-id";
        private static readonly string ParentIdKey = "parent-id";

        private readonly JaegerWavefrontPropagator wfJaegerPropagator =
            new JaegerWavefrontPropagator.Builder()
                .WithBaggagePrefix(BaggagePrefix)
                .WithTraceIdHeader(JaegerHeader)
                .Build();

        [Fact]
        public void TestTraceIdExtract()
        {
            string val = "3871de7e09c53ae8:7499dd16d98ab60e:3771de7e09c55ae8:1";
            var dictionary = new Dictionary<string, string>();
            dictionary.Add(JaegerHeader, val);
            var textMapExtractAdapter = new TextMapExtractAdapter(dictionary);
            WavefrontSpanContext ctx = wfJaegerPropagator.Extract(textMapExtractAdapter);
            Assert.NotNull(ctx);
            Assert.Equal("00000000-0000-0000-3871-de7e09c53ae8", ctx.TraceId);
            Assert.Equal("00000000-0000-0000-7499-dd16d98ab60e", ctx.SpanId);
            Assert.Equal("00000000-0000-0000-7499-dd16d98ab60e", ctx.GetBaggageItem(ParentIdKey));
            Assert.True(ctx.GetSamplingDecision() ?? false);
        }

        [Fact]
        public void TestTraceIdExtractEncoded()
        {
            string val = "3871de7e09c53ae8%3A7499dd16d98ab60e%3A3771de7e09c55ae8%3A1";
            var dictionary = new Dictionary<string, string>();
            dictionary.Add(JaegerHeader, val);
            var textMapExtractAdapter = new TextMapExtractAdapter(dictionary);
            WavefrontSpanContext ctx = wfJaegerPropagator.Extract(textMapExtractAdapter);
            Assert.NotNull(ctx);
            Assert.Equal("00000000-0000-0000-3871-de7e09c53ae8", ctx.TraceId);
            Assert.Equal("00000000-0000-0000-7499-dd16d98ab60e", ctx.SpanId);
            Assert.Equal("00000000-0000-0000-7499-dd16d98ab60e", ctx.GetBaggageItem(ParentIdKey));
            Assert.True(ctx.GetSamplingDecision() ?? false);
        }

        [Fact]
        public void TestInvalidTraceIdExtract()
        {
            string val = ":7499dd16d98ab60e:3771de7e09c55ae8:1";
            var dictionary = new Dictionary<string, string>();
            dictionary.Add(JaegerHeader, val);
            var textMapExtractAdapter = new TextMapExtractAdapter(dictionary);
            WavefrontSpanContext ctx = wfJaegerPropagator.Extract(textMapExtractAdapter);
            Assert.Null(ctx);
        }

        [Fact]
        public void TestTraceIdInject()
        {
            var traceId = Guid.Parse("00000000-0000-0000-3871-de7e09c53ae8");
            var spanId = Guid.Parse("00000000-0000-0000-7499-dd16d98ab60e");
            var baggage = new Dictionary<string, string>();
            baggage[ParentIdKey] = "ef27b4b9-f6e9-46f5-ab2b-47bbb24746c5";
            var dictionary = new Dictionary<string, string>();
            var textMapInjectAdapter = new TextMapInjectAdapter(dictionary);
            wfJaegerPropagator.Inject(new WavefrontSpanContext(traceId, spanId, baggage, true),
                textMapInjectAdapter);
            Assert.True(dictionary.ContainsKey(JaegerHeader));
            Assert.Equal("3871de7e09c53ae8:7499dd16d98ab60e:ef27b4b9f6e946f5ab2b47bbb24746c5:1",
                dictionary[JaegerHeader]);
            Assert.Equal("ef27b4b9-f6e9-46f5-ab2b-47bbb24746c5",
                dictionary[BaggagePrefix + ParentIdKey]);
        }

        [Fact]
        public void TestTraceIdInjectRoot()
        {
            var traceId = Guid.Parse("00000000-0000-0000-3871-de7e09c53ae8");
            var spanId = Guid.Parse("00000000-0000-0000-7499-dd16d98ab60e");
            var dictionary = new Dictionary<string, string>();
            var textMapInjectAdapter = new TextMapInjectAdapter(dictionary);
            wfJaegerPropagator.Inject(new WavefrontSpanContext(traceId, spanId, null, true),
                textMapInjectAdapter);
            Assert.True(dictionary.ContainsKey(JaegerHeader));
            Assert.Equal("3871de7e09c53ae8:7499dd16d98ab60e:0:1", dictionary[JaegerHeader]);
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
