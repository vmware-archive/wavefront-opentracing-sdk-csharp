using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using App.Metrics;
using App.Metrics.Counter;
using App.Metrics.Histogram;
using App.Metrics.Reporting.Wavefront.Builder;
using App.Metrics.Scheduling;
using Microsoft.Extensions.Logging;
using OpenTracing;
using OpenTracing.Propagation;
using OpenTracing.Util;
using Wavefront.OpenTracing.SDK.CSharp.Propagation;
using Wavefront.OpenTracing.SDK.CSharp.Reporting;
using Wavefront.OpenTracing.SDK.CSharp.Sampling;
using Wavefront.SDK.CSharp.Common;
using Wavefront.SDK.CSharp.Common.Application;
using static Wavefront.SDK.CSharp.Common.Constants;

namespace Wavefront.OpenTracing.SDK.CSharp
{
    /// <summary>
    ///     The Wavefront OpenTracing tracer for sending distributed traces to Wavefront.
    /// </summary>
    public class WavefrontTracer : ITracer
    {
        private static readonly ILogger logger =
            Logging.LoggerFactory.CreateLogger<WavefrontTracer>();

        private static readonly string DerivedMetricPrefix = "tracing.derived";
        private static readonly string WavefrontGeneratedComponent = "wavefront-generated";
        private static readonly string InvocationSuffix = ".invocation";
        private static readonly string ErrorSuffix = ".error";
        private static readonly string TotalTimeSuffix = ".total_time.millis";
        private static readonly string DurationSuffix = ".duration.micros";
        private static readonly string OperationNameTag = "operationName";

        private readonly PropagatorRegistry registry = new PropagatorRegistry();
        private readonly IReporter reporter;
        private readonly IList<ISampler> samplers;

        private readonly IMetricsRoot metricsRoot;
        private readonly AppMetricsTaskScheduler metricsScheduler;
        private readonly HeartbeaterService heartbeaterService;
        private readonly string applicationServicePrefix;

        /// <summary>
        ///     A builder for <see cref="WavefrontTracer"/>.
        /// </summary>
        public class Builder
        {
            private readonly IReporter reporter;
            // Application metadata, will not have repeated tags and will be low cardinality tags
            private readonly ApplicationTags applicationTags;
            // Tags can be repeated and include high-cardinality tags
            private readonly IList<KeyValuePair<string, string>> tags;
            private readonly IList<ISampler> samplers;
            // Default to 1 minute
            private TimeSpan reportFrequency = TimeSpan.FromMinutes(1);

            /// <summary>
            ///     Initializes a new instance of the <see cref="Builder"/> class.
            /// </summary>
            /// <param name="reporter">The reporter to report tracing spans with.</param>
            /// <param name="applicationTags">
            ///     Tags containing metadata about the application.
            /// </param>
            public Builder(IReporter reporter, ApplicationTags applicationTags)
            {
                this.reporter = reporter;
                this.applicationTags = applicationTags;
                tags = new List<KeyValuePair<string, string>>();
                samplers = new List<ISampler>();
            }

            /// <summary>
            ///     Adds a global tag that will be included with every reported span.
            /// </summary>
            /// <returns><see cref="this"/></returns>
            /// <param name="key">The tag's key</param>
            /// <param name="value">The tag's value.</param>
            public Builder WithGlobalTag(string key, string value)
            {
                if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(value))
                {
                    tags.Add(new KeyValuePair<string, string>(key, value));
                }
                return this;
            }

            /// <summary>
            ///     Adds global tags that will be included with every reported span.
            /// </summary>
            /// <returns><see cref="this"/></returns>
            /// <param name="tags">A dictionary of tag keys to tag values.</param>
            public Builder WithGlobalTags(IDictionary<string, string> tags)
            {
                if (tags != null && tags.Count > 0)
                {
                    foreach (var tag in tags)
                    {
                        WithGlobalTag(tag.Key, tag.Value);
                    }
                }
                return this;
            }

            /// <summary>
            ///     Adds multi-valued global tags that will be included with every reported span.
            /// </summary>
            /// <returns><see cref="this"/></returns>
            /// <param name="tags">A dictionary of multi-valued tags.</param>
            public Builder WithGlobalMultiValuedTags(IDictionary<string, IEnumerable<string>> tags)
            {
                if (tags != null && tags.Count > 0)
                {
                    foreach (var tag in tags)
                    {
                        foreach (string value in tag.Value)
                        {
                            WithGlobalTag(tag.Key, value);
                        }
                    }
                }
                return this;
            }

            /// <summary>
            ///     Sampler for sampling traces.
            /// 
            ///     Samplers can be chained by calling this method multiple times.  Sampling
            ///     decisions are OR'd when multiple samplers are used.
            /// </summary>
            /// <returns><see cref="this"/></returns>
            /// <param name="sampler">Sampler.</param>
            public Builder WithSampler(ISampler sampler)
            {
                samplers.Add(sampler);
                return this;
            }

            /// <summary>
            ///     Visible for testing only.
            /// </summary>
            /// <returns><see cref="this"/></returns>
            /// <param name="reportFrequency">Frequency to report data to Wavefront.</param>
            internal Builder SetReportFrequency(TimeSpan reportFrequency)
            {
                this.reportFrequency = reportFrequency;
                return this;
            }

            private void ApplyApplicationTags()
            {
                WithGlobalTag(ApplicationTagKey, applicationTags.Application);
                WithGlobalTag(ServiceTagKey, applicationTags.Service);
                WithGlobalTag(ClusterTagKey, applicationTags.Cluster ?? NullTagValue);
                WithGlobalTag(ShardTagKey, applicationTags.Shard ?? NullTagValue);
                WithGlobalTags(applicationTags.CustomTags);
            }

            /// <summary>
            ///     Builds and returns the <see cref="WavefrontTracer"/> instance based on the
            ///     provided configuration.
            /// </summary>
            /// <returns>A <see cref="WavefrontTracer"/>.</returns>
            public WavefrontTracer Build()
            {
                ApplyApplicationTags();
                return new WavefrontTracer(
                    reporter, tags, samplers, applicationTags, reportFrequency);
            }
        }

        private WavefrontTracer(
            IReporter reporter, IList<KeyValuePair<string, string>> tags, IList<ISampler> samplers,
            ApplicationTags applicationTags, TimeSpan reportFrequency)
        {
            ScopeManager = new AsyncLocalScopeManager();
            this.reporter = reporter;
            Tags = tags;
            this.samplers = samplers;
            applicationServicePrefix = $"{applicationTags.Application}.{applicationTags.Service}.";

            WavefrontSpanReporter spanReporter = GetWavefrontSpanReporter(reporter);
            if (spanReporter != null)
            {
                /*
                 * Tracing spans will be converted to metrics and histograms and will be reported
                 * to Wavefront only if you use the WavefrontSpanReporter.
                 */
                InitMetricsHistogramsReporting(spanReporter, applicationTags, reportFrequency,
                    out metricsRoot, out metricsScheduler, out heartbeaterService);
            }
        }

        private WavefrontSpanReporter GetWavefrontSpanReporter(IReporter reporter)
        {
            if (reporter is WavefrontSpanReporter)
            {
                return (WavefrontSpanReporter)reporter;
            }
            else if (reporter is CompositeReporter)
            {
                foreach (var delegateReporter in ((CompositeReporter)reporter).GetReporters())
                {
                    if (delegateReporter is WavefrontSpanReporter)
                    {
                        // only one delegate reporter from the list is WavefrontSpanReporter
                        return (WavefrontSpanReporter)delegateReporter;
                    }
                }
            }
            return null;
        }

        private void InitMetricsHistogramsReporting(
            WavefrontSpanReporter wfSpanReporter, ApplicationTags applicationTags,
            TimeSpan reportFrequency, out IMetricsRoot metricsRoot,
            out AppMetricsTaskScheduler metricsScheduler, out HeartbeaterService heartbeaterService)
        {
            var tempMetricsRoot = new MetricsBuilder()
                .Configuration.Configure(
                    options =>
                    {
                        options.DefaultContextLabel = DerivedMetricPrefix;
                    })
                .Report.ToWavefront(
                    options =>
                    {
                        options.WavefrontSender = wfSpanReporter.WavefrontSender;
                        options.Source = wfSpanReporter.Source;
                        options.ApplicationTags = applicationTags;
                        options.WavefrontHistogram.ReportMinuteDistribution = true;
                    })
                .Build();
            metricsRoot = tempMetricsRoot;

            metricsScheduler = new AppMetricsTaskScheduler(
                reportFrequency,
                async () =>
                {
                    await Task.WhenAll(tempMetricsRoot.ReportRunner.RunAllAsync());
                });
            metricsScheduler.Start();

            heartbeaterService = new HeartbeaterService(
                wfSpanReporter.WavefrontSender, applicationTags, WavefrontGeneratedComponent,
                wfSpanReporter.Source);
            heartbeaterService.Start();
        }

        /// <inheritdoc />
        public IScopeManager ScopeManager { get; }

        /// <inheritdoc />
        public ISpan ActiveSpan => ScopeManager.Active?.Span;

        internal IList<KeyValuePair<string, string>> Tags { get; }

        /// <inheritdoc />
        public ISpanBuilder BuildSpan(string operationName)
        {
            return new WavefrontSpanBuilder(operationName, this);
        }

        /// <inheritdoc />
        public void Inject<TCarrier>(
            ISpanContext spanContext, IFormat<TCarrier> format, TCarrier carrier)
        {
            var propagator = registry.Get(format);
            if (propagator == null)
            {
                throw new ArgumentException("invalid format: " + format);
            }
            propagator.Inject((WavefrontSpanContext)spanContext, carrier);
        }

        /// <inheritdoc />
        public ISpanContext Extract<TCarrier>(IFormat<TCarrier> format, TCarrier carrier)
        {
            var propagator = registry.Get(format);
            if (propagator == null)
            {
                throw new ArgumentException("invalid format: " + format);
            }
            return propagator.Extract(carrier);
        }

        internal bool Sample(string operationName, long traceId, long duration)
        {
            if (samplers == null || samplers.Count == 0)
            {
                return true;
            }
            bool earlySampling = (duration == 0);
            foreach (var sampler in samplers)
            {
                bool doSample = (earlySampling == sampler.IsEarly);
                if (doSample && sampler.Sample(operationName, traceId, duration))
                {
                    if (logger.IsEnabled(LogLevel.Debug))
                    {
                        logger.Log(LogLevel.Debug,
                            $"{sampler.GetType().Name}={true} op={operationName}");
                    }
                    return true;
                }
                if (logger.IsEnabled(LogLevel.Debug))
                {
                    logger.Log(LogLevel.Debug,
                        $"{sampler.GetType().Name}={false} op={operationName}");
                }
            }
            return false;
        }

        internal void ReportWavefrontGeneratedData(WavefrontSpan span)
        {
            if (metricsRoot == null)
            {
                /*
                 * WavefrontSpanReporter not set, so no tracing spans will be reported as
                 * metrics/histograms.
                 */
                return;
            }

            /*
             * TODO: Need to update the sanitizing of context + metric name as application, service,
             * and operation names can have spaces and other invalid metric name characters.
             */
            var pointTags = new MetricTags(OperationNameTag, span.GetOperationName());
            var namePrefix = applicationServicePrefix + span.GetOperationName();
            metricsRoot.Measure.Counter.Increment(new CounterOptions
            {
                Name = namePrefix + InvocationSuffix,
                Tags = pointTags
            });
            if (span.IsError())
            {
                metricsRoot.Measure.Counter.Increment(new CounterOptions
                {
                    Name = namePrefix + ErrorSuffix,
                    Tags = pointTags
                });
            }
            // TODO: implement a span duration timer that has higher resolution than millis
            long spanDurationMillis = span.GetDurationMillis();
            // Add duration in millis to duration counter
            metricsRoot.Measure.Counter.Increment(new CounterOptions
            {
                Name = namePrefix + TotalTimeSuffix,
                Tags = pointTags
            }, spanDurationMillis);
            // Update histogram with duration in micros
            metricsRoot.Measure.Histogram.Update(
                new WavefrontHistogramOptions.Builder(namePrefix + DurationSuffix)
                    .Tags(pointTags)
                    .Build(),
                spanDurationMillis * 1000);
        }

        internal void ReportSpan(WavefrontSpan span)
        {
            // Reporter will flush it to Wavefront/proxy.
            try
            {
                reporter.Report(span);
            }
            catch (IOException e)
            {
                logger.Log(LogLevel.Warning, "Error reporting span", e);
            }
        }

        internal DateTime CurrentTimestamp()
        {
            return DateTime.UtcNow;
        }

        /// <summary>
        ///     Closes the reporter for this tracer.
        /// </summary>
        public void Close()
        {
            reporter.Close();
            if (metricsScheduler != null)
            {
                metricsScheduler.Dispose();
            }
            if (heartbeaterService != null)
            {
                heartbeaterService.Dispose();
            }
        }
    }
}
