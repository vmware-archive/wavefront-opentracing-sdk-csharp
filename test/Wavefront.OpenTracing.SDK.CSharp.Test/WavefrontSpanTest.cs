using Moq;
using OpenTracing.Tag;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using Wavefront.OpenTracing.SDK.CSharp.Reporting;
using Wavefront.OpenTracing.SDK.CSharp.Sampling;
using Wavefront.SDK.CSharp.Common;
using Wavefront.SDK.CSharp.Entities.Histograms;
using Wavefront.SDK.CSharp.Entities.Tracing;
using Xunit;
using static Moq.It;
using static Wavefront.OpenTracing.SDK.CSharp.Test.Utils;

namespace Wavefront.OpenTracing.SDK.CSharp.Test
{
    /// <summary>
    ///     Unit tests to test spans, generated metrics, and component heartbeat.
    /// </summary>
    public class WavefrontSpanTest
    {
        [Fact]
        public void TestValidWavefrontSpan()
        {
            string operationName = "dummyOp";
            var pointTags = PointTags(operationName, new Dictionary<string, string>
            {
                { Tags.SpanKind.Key, Constants.NullTagValue }
            });
            var wfSenderMock = new Mock<IWavefrontSender>(MockBehavior.Strict);
            wfSenderMock.Reset();

            Expression<Action<IWavefrontSender>> sendSpan =
                sender => sender.SendSpan(operationName, IsAny<long>(),
                    IsAny<long>(), "source", IsAny<Guid>(), IsAny<Guid>(), new List<Guid>(),
                    new List<Guid>(), IsAny<IList<KeyValuePair<string, string>>>(),
                    new List<SpanLog>());
            Expression<Action<IWavefrontSender>> sendInvocationCount =
                sender => sender.SendMetric(
                    "∆tracing.derived.myApplication.myService.dummyOp.invocation.count", 1.0,
                    null, "source",
                    Is<IDictionary<string, string>>(dict => ContainsPointTags(dict, pointTags)));
            Expression<Action<IWavefrontSender>> sendTotalMillis =
                sender => sender.SendMetric(
                    "∆tracing.derived.myApplication.myService.dummyOp.total_time.millis.count",
                    IsAny<double>(), null, "source",
                    Is<IDictionary<string, string>>(dict => ContainsPointTags(dict, pointTags)));
            Expression<Action<IWavefrontSender>> sendDurationMicros =
                sender => sender.SendDistribution(
                    "tracing.derived.myApplication.myService.dummyOp.duration.micros",
                    IsAny<IList<KeyValuePair<double, int>>>(),
                    new HashSet<HistogramGranularity> { HistogramGranularity.Minute }, IsAny<long>(),
                    "source",
                    Is<IDictionary<string, string>>(dict => ContainsPointTags(dict, pointTags)));
            Expression<Action<IWavefrontSender>> sendHeartbeat =
                sender => sender.SendMetric(
                    "~component.heartbeat", 1.0, IsAny<long>(), "source",
                    IsAny<IDictionary<string, string>>());

            wfSenderMock.Setup(sendSpan);
            wfSenderMock.Setup(sendInvocationCount);
            wfSenderMock.Setup(sendTotalMillis);
            wfSenderMock.Setup(sendDurationMicros);
            wfSenderMock.Setup(sendHeartbeat);

            WavefrontSpanReporter spanReporter = new WavefrontSpanReporter.Builder()
                .WithSource("source").Build(wfSenderMock.Object);
            WavefrontTracer tracer = new WavefrontTracer.Builder(spanReporter,
                BuildApplicationTags()).SetReportFrequency(TimeSpan.FromMilliseconds(50)).Build();
            tracer.BuildSpan(operationName).StartActive(true).Span.Finish(DateTimeOffset.Now.AddMilliseconds(10));
            Console.WriteLine("Sleeping for 1 second zzzzz .....");
            Thread.Sleep(1000);
            Console.WriteLine("Resuming execution .....");

            wfSenderMock.Verify(sendSpan, Times.Once());
            wfSenderMock.Verify(sendInvocationCount, Times.AtLeastOnce());
            wfSenderMock.Verify(sendTotalMillis, Times.AtLeastOnce());
            /*
             * TODO: update WavefrontHistogramOptions.Builder to allow a clock to be passed in
             * so that we can advance minute bin and update the below call to Times.AtLeastOnce()           
             */
            wfSenderMock.Verify(sendDurationMicros, Times.AtMost(int.MaxValue));
            wfSenderMock.Verify(sendHeartbeat, Times.AtMost(int.MaxValue));
        }

        [Fact]
        public void TestDebugWavefrontSpan()
        {
            string operationName = "dummyOp";
            var wfSenderMock = new Mock<IWavefrontSender>(MockBehavior.Strict);
            wfSenderMock.Reset();

            Expression<Action<IWavefrontSender>> sendSpan =
                sender => sender.SendSpan(operationName, IsAny<long>(),
                    IsAny<long>(), "source", IsAny<Guid>(), IsAny<Guid>(), new List<Guid>(),
                    new List<Guid>(), IsAny<IList<KeyValuePair<string, string>>>(),
                    new List<SpanLog>());
            Expression <Action<IWavefrontSender>> sendInvocationCount =
                sender => sender.SendMetric(
                    "∆tracing.derived.myApplication.myService.dummyOp.invocation.count", 1.0,
                    null, "source", IsAny<IDictionary<string, string>>());
            Expression<Action<IWavefrontSender>> sendTotalMillis =
                sender => sender.SendMetric(
                    "∆tracing.derived.myApplication.myService.dummyOp.total_time.millis.count",
                    IsAny<double>(), null, "source", IsAny<IDictionary<string, string>>());
            Expression<Action<IWavefrontSender>> sendDurationMicros =
                sender => sender.SendDistribution(
                    "tracing.derived.myApplication.myService.dummyOp.duration.micros",
                    IsAny<IList<KeyValuePair<double, int>>>(),
                    new HashSet<HistogramGranularity> { HistogramGranularity.Minute }, IsAny<long>(),
                    "source", IsAny<IDictionary<string, string>>());
            Expression<Action<IWavefrontSender>> sendHeartbeat =
                sender => sender.SendMetric(
                    "~component.heartbeat", 1.0, IsAny<long>(), "source",
                    IsAny<IDictionary<string, string>>());

            wfSenderMock.Setup(sendSpan);
            wfSenderMock.Setup(sendInvocationCount);
            wfSenderMock.Setup(sendTotalMillis);
            wfSenderMock.Setup(sendDurationMicros);
            wfSenderMock.Setup(sendHeartbeat);

            WavefrontSpanReporter spanReporter = new WavefrontSpanReporter.Builder()
                .WithSource("source").Build(wfSenderMock.Object);
            WavefrontTracer tracer = new WavefrontTracer.Builder(spanReporter,
                BuildApplicationTags()).SetReportFrequency(TimeSpan.FromMilliseconds(50))
                .WithSampler(new RateSampler(0.0)).Build();
            tracer.BuildSpan(operationName).WithTag("debug", true)
                .StartActive(true).Span.Finish(DateTimeOffset.Now.AddMilliseconds(10));
            Console.WriteLine("Sleeping for 1 second zzzzz .....");
            Thread.Sleep(1000);
            Console.WriteLine("Resuming execution .....");

            wfSenderMock.Verify(sendSpan, Times.Once());
            wfSenderMock.Verify(sendInvocationCount, Times.AtLeastOnce());
            wfSenderMock.Verify(sendTotalMillis, Times.AtLeastOnce());
            /*
             * TODO: update WavefrontHistogramOptions.Builder to allow a clock to be passed in
             * so that we can advance minute bin and update the below call to Times.AtLeastOnce()
             */
            wfSenderMock.Verify(sendDurationMicros, Times.AtMost(int.MaxValue));
            wfSenderMock.Verify(sendHeartbeat, Times.AtMost(int.MaxValue));
        }

        [Fact]
        public void TestErrorWavefrontSpan()
        {
            string operationName = "dummyOp";
            var pointTags = PointTags(operationName, new Dictionary<string, string>
            {
                { Tags.SpanKind.Key, Constants.NullTagValue },
                { Tags.HttpStatus.Key, "404" }
            });
            var histogramTags = PointTags(operationName, new Dictionary<string, string>
            {
                { Tags.SpanKind.Key, Constants.NullTagValue },
                { Tags.HttpStatus.Key, "404" },
                { "error", "true" }
            });
            var wfSenderMock = new Mock<IWavefrontSender>(MockBehavior.Strict);
            wfSenderMock.Reset();
            Expression<Action<IWavefrontSender>> sendSpan =
                sender => sender.SendSpan(operationName, IsAny<long>(),
                    IsAny<long>(), "source", IsAny<Guid>(), IsAny<Guid>(), new List<Guid>(),
                    new List<Guid>(), IsAny<IList<KeyValuePair<string, string>>>(),
                    new List<SpanLog>());
            Expression<Action<IWavefrontSender>> sendInvocationCount =
                sender => sender.SendMetric(
                    "∆tracing.derived.myApplication.myService.dummyOp.invocation.count", 1.0,
                    null, "source",
                    Is<IDictionary<string, string>>(dict => ContainsPointTags(dict, pointTags)));
            Expression<Action<IWavefrontSender>> sendErrorCount =
                sender => sender.SendMetric(
                    "∆tracing.derived.myApplication.myService.dummyOp.error.count", 1.0,
                    null, "source",
                    Is<IDictionary<string, string>>(dict => ContainsPointTags(dict, pointTags)));
            Expression<Action<IWavefrontSender>> sendTotalMillis =
                sender => sender.SendMetric(
                    "∆tracing.derived.myApplication.myService.dummyOp.total_time.millis.count",
                    IsAny<double>(), null, "source",
                    Is<IDictionary<string, string>>(dict => ContainsPointTags(dict, pointTags)));
            Expression<Action<IWavefrontSender>> sendDurationMicros =
                sender => sender.SendDistribution(
                    "tracing.derived.myApplication.myService.dummyOp.duration.micros",
                    IsAny<IList<KeyValuePair<double, int>>>(),
                    new HashSet<HistogramGranularity> { HistogramGranularity.Minute }, IsAny<long>(),
                    "source",
                    Is<IDictionary<string, string>>(dict => ContainsPointTags(dict, histogramTags)));
            Expression<Action<IWavefrontSender>> sendHeartbeat =
                sender => sender.SendMetric(
                    "~component.heartbeat", 1.0, IsAny<long>(), "source",
                    IsAny<IDictionary<string, string>>());

            wfSenderMock.Setup(sendSpan);
            wfSenderMock.Setup(sendInvocationCount);
            wfSenderMock.Setup(sendErrorCount);
            wfSenderMock.Setup(sendTotalMillis);
            wfSenderMock.Setup(sendDurationMicros);
            wfSenderMock.Setup(sendHeartbeat);

            WavefrontSpanReporter spanReporter = new WavefrontSpanReporter.Builder()
                .WithSource("source").Build(wfSenderMock.Object);
            WavefrontTracer tracer = new WavefrontTracer.Builder(spanReporter,
                BuildApplicationTags()).SetReportFrequency(TimeSpan.FromMilliseconds(50)).Build();
            tracer.BuildSpan(operationName).WithTag(Tags.Error, true).WithTag(Tags.HttpStatus, 404)
                .StartActive(true).Span.Finish(DateTimeOffset.Now.AddMilliseconds(10));
            Console.WriteLine("Sleeping for 1 second zzzzz .....");
            Thread.Sleep(1000);
            Console.WriteLine("Resuming execution .....");

            wfSenderMock.Verify(sendSpan, Times.Once());
            wfSenderMock.Verify(sendInvocationCount, Times.AtLeastOnce());
            wfSenderMock.Verify(sendErrorCount, Times.AtLeastOnce());
            wfSenderMock.Verify(sendTotalMillis, Times.AtLeastOnce());
            /*
             * TODO: update WavefrontHistogramOptions.Builder to allow a clock to be passed in
             * so that we can advance minute bin and update the below call to Times.AtLeastOnce()           
             */
            wfSenderMock.Verify(sendDurationMicros, Times.AtMost(int.MaxValue));
            wfSenderMock.Verify(sendHeartbeat, Times.AtMost(int.MaxValue));
        }

        [Fact]
        public void TestCustomRedMetricsTagsWavefrontSpan()
        {
            string operationName = "dummyOp";
            var pointTags = PointTags(operationName, new Dictionary<string, string>
            {
                { "tenant", "tenant1" },
                { "env", "Staging" },
                { Tags.SpanKind.Key, Tags.SpanKindServer }
            });
            var wfSenderMock = new Mock<IWavefrontSender>(MockBehavior.Strict);
            wfSenderMock.Reset();
            Expression<Action<IWavefrontSender>> sendSpan =
                sender => sender.SendSpan(operationName, IsAny<long>(),
                    IsAny<long>(), "source", IsAny<Guid>(), IsAny<Guid>(), new List<Guid>(),
                    new List<Guid>(), IsAny<IList<KeyValuePair<string, string>>>(),
                    new List<SpanLog>());
            Expression<Action<IWavefrontSender>> sendInvocationCount =
                sender => sender.SendMetric(
                    "∆tracing.derived.myApplication.myService.dummyOp.invocation.count", 1.0,
                    null, "source",
                    Is<IDictionary<string, string>>(dict => 
                        ContainsPointTags(dict, pointTags) && !dict.ContainsKey("customId")));
            Expression<Action<IWavefrontSender>> sendTotalMillis =
                sender => sender.SendMetric(
                    "∆tracing.derived.myApplication.myService.dummyOp.total_time.millis.count",
                    IsAny<double>(), null, "source",
                    Is<IDictionary<string, string>>(dict =>
                        ContainsPointTags(dict, pointTags) && !dict.ContainsKey("customId")));
            Expression<Action<IWavefrontSender>> sendDurationMicros =
                sender => sender.SendDistribution(
                    "tracing.derived.myApplication.myService.dummyOp.duration.micros",
                    IsAny<IList<KeyValuePair<double, int>>>(),
                    new HashSet<HistogramGranularity> { HistogramGranularity.Minute }, IsAny<long>(),
                    "source",
                    Is<IDictionary<string, string>>(dict =>
                        ContainsPointTags(dict, pointTags) && !dict.ContainsKey("customId")));
            Expression<Action<IWavefrontSender>> sendHeartbeat =
                sender => sender.SendMetric(
                    "~component.heartbeat", 1.0, IsAny<long>(), "source",
                    IsAny<IDictionary<string, string>>());

            wfSenderMock.Setup(sendSpan);
            wfSenderMock.Setup(sendInvocationCount);
            wfSenderMock.Setup(sendTotalMillis);
            wfSenderMock.Setup(sendDurationMicros);
            wfSenderMock.Setup(sendHeartbeat);

            WavefrontSpanReporter spanReporter = new WavefrontSpanReporter.Builder()
                .WithSource("source").Build(wfSenderMock.Object);
            WavefrontTracer tracer = new WavefrontTracer.Builder(spanReporter,
                BuildApplicationTags()).SetReportFrequency(TimeSpan.FromMilliseconds(50))
                .RedMetricsCustomTagKeys(new HashSet<string>{ "tenant", "env" }).Build();
            tracer.BuildSpan(operationName).WithTag("tenant", "tenant1").WithTag("env", "Staging")
                .WithTag("customId", "abc123").WithTag(Tags.SpanKind, Tags.SpanKindServer)
                .StartActive(true).Span.Finish(DateTimeOffset.Now.AddMilliseconds(10));
            Console.WriteLine("Sleeping for 1 second zzzzz .....");
            Thread.Sleep(1000);
            Console.WriteLine("Resuming execution .....");

            wfSenderMock.Verify(sendSpan, Times.Once());
            wfSenderMock.Verify(sendInvocationCount, Times.AtLeastOnce());
            wfSenderMock.Verify(sendTotalMillis, Times.AtLeastOnce());
            /*
             * TODO: update WavefrontHistogramOptions.Builder to allow a clock to be passed in
             * so that we can advance minute bin and update the below call to Times.AtLeastOnce()           
             */
            wfSenderMock.Verify(sendDurationMicros, Times.AtMost(int.MaxValue));
            wfSenderMock.Verify(sendHeartbeat, Times.AtMost(int.MaxValue));
        }

        private bool ContainsPointTags(
            IDictionary<string, string> tags, IDictionary<string, string> pointTags)
        {
            return pointTags.All(entry => tags.ContainsKey(entry.Key) && 
                tags[entry.Key].Equals(entry.Value));
        }

        private IDictionary<string, string> PointTags(string operationName,
            IDictionary<string, string> customTags)
        {
            var pointTags = new Dictionary<string, string>
            {
                { "application", "myApplication" },
                { "service", "myService" },
                { "cluster", "none" },
                { "shard", "none" },
                { "component", "none" },
                { "operationName", operationName }
            };
            foreach (var customTag in customTags)
            {
                pointTags.Add(customTag.Key, customTag.Value);
            }
            return pointTags;
        }
    }
}
