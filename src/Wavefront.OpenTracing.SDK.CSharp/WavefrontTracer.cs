using App.Metrics;
using App.Metrics.Counter;
using App.Metrics.Histogram;
using App.Metrics.Reporting.Wavefront.Builder;
using App.Metrics.Scheduling;
using Microsoft.Extensions.Logging;
using OpenTracing;
using OpenTracing.Propagation;
using OpenTracing.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Wavefront.OpenTracing.SDK.CSharp.Propagation;
using Wavefront.OpenTracing.SDK.CSharp.Reporting;
using Wavefront.OpenTracing.SDK.CSharp.Sampling;
using Wavefront.SDK.CSharp.Common;
using Wavefront.SDK.CSharp.Common.Application;
using Wavefront.SDK.CSharp.Common.Metrics;

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
        private static readonly string InvocationSuffix = ".invocation";
        private static readonly string ErrorSuffix = ".error";
        private static readonly string TotalTimeSuffix = ".total_time.millis";
        private static readonly string DurationSuffix = ".duration.micros";
        private static readonly string OperationNameTag = "operationName";

        private static readonly List<string> HeartbeaterComponents =
            new List<string> { "wavefront-generated", "opentracing", "csharp" };

        private readonly PropagatorRegistry registry = new PropagatorRegistry();
        private readonly IReporter reporter;
        private readonly IList<ISampler> samplers;
        private readonly ISet<string> redMetricsCustomTagKeys;
        private readonly ApplicationTags applicationTags;

        private readonly IMetricsRoot metricsRoot;
        private readonly AppMetricsTaskScheduler metricsScheduler;
        private readonly HeartbeaterService heartbeaterService;

        private readonly WavefrontSdkMetricsRegistry sdkMetricsRegistry;

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
            private readonly ISet<string> redMetricsCustomTagKeys;

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
                redMetricsCustomTagKeys = new HashSet<string>();
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
            ///     Set custom RED metrics tags. If the span has any of the tags, then those get
            ///     reported to the span generated RED metrics.
            /// 
            ///     Example - If you have a span tag of 'tenant-id' that you also want to be
            ///     propagated to the RED metrics, then you would call this method and pass in
            ///     an enumerable containing 'tenant-id'.
            /// 
            ///     Caveat - Ensure that redMetricsCustomTagKeys are low cardinality tags.
            /// </summary>
            /// <returns><see cref="this"/></returns>
            /// <param name="redMetricsCustomTagKeys">
            ///     Custom tags you want to report for the span-generated RED metrics.
            /// </param>
            public Builder RedMetricsCustomTagKeys(IEnumerable<string> redMetricsCustomTagKeys)
            {
                if (redMetricsCustomTagKeys != null)
                {
                    foreach (string customTagKey in redMetricsCustomTagKeys)
                    {
                        this.redMetricsCustomTagKeys.Add(customTagKey);
                    }
                }
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
                if (applicationTags != null)
                {
                    WithGlobalTags(applicationTags.ToPointTags());
                }
            }

            /// <summary>
            ///     Builds and returns the <see cref="WavefrontTracer"/> instance based on the
            ///     provided configuration.
            /// </summary>
            /// <returns>A <see cref="WavefrontTracer"/>.</returns>
            public WavefrontTracer Build()
            {
                ApplyApplicationTags();
                return new WavefrontTracer(reporter, tags, samplers, applicationTags,
                    redMetricsCustomTagKeys,reportFrequency);
            }
        }

        private WavefrontTracer(
            IReporter reporter, IList<KeyValuePair<string, string>> tags, IList<ISampler> samplers,
            ApplicationTags applicationTags, ISet<string> redMetricsCustomTagKeys,
            TimeSpan reportFrequency)
        {
            ScopeManager = new AsyncLocalScopeManager();
            this.reporter = reporter;
            Tags = tags;
            this.samplers = samplers;
            this.redMetricsCustomTagKeys = redMetricsCustomTagKeys;
            this.applicationTags = applicationTags;

            WavefrontSpanReporter spanReporter = GetWavefrontSpanReporter(reporter);
            if (spanReporter != null)
            {
                /*
                 * Tracing spans will be converted to metrics and histograms and will be reported
                 * to Wavefront only if you use the WavefrontSpanReporter.
                 */
                InitMetricsHistogramsReporting(spanReporter, applicationTags, reportFrequency,
                    out metricsRoot, out metricsScheduler, out heartbeaterService,
                    out sdkMetricsRegistry);
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
            out AppMetricsTaskScheduler metricsScheduler, out HeartbeaterService heartbeaterService,
            out WavefrontSdkMetricsRegistry sdkMetricsRegistry)
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
                wfSpanReporter.WavefrontSender, applicationTags, HeartbeaterComponents,
                wfSpanReporter.Source);
            heartbeaterService.Start();

            sdkMetricsRegistry = new WavefrontSdkMetricsRegistry
                .Builder(wfSpanReporter.WavefrontSender)
                .Prefix(Constants.SdkMetricPrefix + ".opentracing")
                .Source(wfSpanReporter.Source)
                .Tags(applicationTags.ToPointTags())
                .Build();
            wfSpanReporter.SetSdkMetricsRegistry(sdkMetricsRegistry);
        }

        public WavefrontSdkMetricsRegistry SdkMetricsRegistry
        {
            get
            {
                return sdkMetricsRegistry;
            }
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
                        logger.LogDebug($"{sampler.GetType().Name}={true} op={operationName}");
                    }
                    return true;
                }
                if (logger.IsEnabled(LogLevel.Debug))
                {
                    logger.LogDebug($"{sampler.GetType().Name}={false} op={operationName}");
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

            var pointTagsDict = new Dictionary<string, string>
            {
                { OperationNameTag, span.GetOperationName() },
                { Constants.ComponentTagKey, span.GetComponentTagValue() }
            };

            bool customTagMatch = false;
            if (redMetricsCustomTagKeys.Count > 0)
            {
                var spanTags = span.GetTagsAsMap();
                foreach (string customTagKey in redMetricsCustomTagKeys)
                {
                    if (spanTags.ContainsKey(customTagKey))
                    {
                        customTagMatch = true;
                        // Assuming at least one value exists
                        pointTagsDict[customTagKey] = spanTags[customTagKey].First();
                    }
                }
            }

            string application = OverrideWithSingleValuedSpanTag(span, pointTagsDict,
                Constants.ApplicationTagKey, applicationTags.Application);
            string service = OverrideWithSingleValuedSpanTag(span, pointTagsDict,
               Constants.ServiceTagKey, applicationTags.Service);
            OverrideWithSingleValuedSpanTag(span, pointTagsDict, Constants.ClusterTagKey,
                applicationTags.Cluster);
            OverrideWithSingleValuedSpanTag(span, pointTagsDict, Constants.ShardTagKey,
                applicationTags.Shard);

            // Propagate custom tags to ~component.heartbeat
            if (heartbeaterService != null && customTagMatch)
            {
                heartbeaterService.ReportCustomTags(pointTagsDict);
            }

            var pointTags = MetricTags.Concat(MetricTags.Empty, pointTagsDict);

            string metricNamePrefix = $"{application}.{service}.{span.GetOperationName()}";
            metricsRoot.Measure.Counter.Increment(new CounterOptions
            {
                Name = metricNamePrefix + InvocationSuffix,
                Tags = pointTags
            });
            if (span.IsError())
            {
                metricsRoot.Measure.Counter.Increment(new CounterOptions
                {
                    Name = metricNamePrefix + ErrorSuffix,
                    Tags = pointTags
                });
            }
            long spanDurationMicros = span.GetDurationMicros();
            // Convert duration from micros to millis and add to duration counter
            metricsRoot.Measure.Counter.Increment(new CounterOptions
            {
                Name = metricNamePrefix + TotalTimeSuffix,
                Tags = pointTags
            }, spanDurationMicros / 1000);
            // Update histogram with duration in micros
            if (span.IsError())
            {
                var errorPointTags = MetricTags.Concat(pointTags, new MetricTags("error", "true"));
                metricsRoot.Measure.Histogram.Update(
                    new WavefrontHistogramOptions.Builder(metricNamePrefix + DurationSuffix)
                        .Tags(errorPointTags)
                        .Build(),
                    spanDurationMicros);
            }
            else
            {
                metricsRoot.Measure.Histogram.Update(
                    new WavefrontHistogramOptions.Builder(metricNamePrefix + DurationSuffix)
                        .Tags(pointTags)
                        .Build(),
                    spanDurationMicros);
            }
        }

        private string OverrideWithSingleValuedSpanTag(WavefrontSpan span,
            IDictionary<string, string> pointTagsDict, string key, string defaultValue)
        {
            string spanTagValue = span.GetSingleValuedTagValue(key);
            if (spanTagValue == null)
            {
                return defaultValue;
            }
            if (!spanTagValue.Equals(defaultValue))
            {
                pointTagsDict[key] = spanTagValue;
            }
            return spanTagValue;
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
                logger.LogWarning(0, e, "Error reporting span");
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
            sdkMetricsRegistry?.Dispose();
            metricsScheduler?.Dispose();
            heartbeaterService?.Dispose();
        }
    }
}
