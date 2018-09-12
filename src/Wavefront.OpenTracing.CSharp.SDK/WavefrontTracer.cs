using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using Microsoft.Extensions.Logging;
using OpenTracing;
using OpenTracing.Propagation;
using OpenTracing.Util;
using Wavefront.CSharp.SDK.Common;
using Wavefront.OpenTracing.CSharp.SDK.Propagation;
using Wavefront.OpenTracing.CSharp.SDK.Reporting;

namespace Wavefront.OpenTracing.CSharp.SDK
{
    /// <summary>
    ///     The Wavefront OpenTracing tracer for sending distributed traces to Wavefront.
    /// </summary>
    public class WavefrontTracer : ITracer
    {
        private static readonly ILogger logger =
            Logging.LoggerFactory.CreateLogger<WavefrontTracer>();

        private readonly PropagatorRegistry registry = new PropagatorRegistry();
        private readonly IReporter reporter;

        /// <summary>
        ///     A builder for <see cref="WavefrontTracer"/>.
        /// </summary>
        public class Builder
        {
            private string source;
            private IReporter reporter;
            private readonly List<KeyValuePair<string, string>> tags;

            /// <summary>
            ///     Initializes a new instance of the <see cref="Builder"/> class.
            /// </summary>
            public Builder()
            {
                tags = new List<KeyValuePair<string, string>>();
            }

            /// <summary>
            ///     Sets the source attributed to reported spans.
            /// </summary>
            /// <returns><see cref="this"/></returns>
            /// <param name="source">The source string.</param>
            public Builder WithSource(string source)
            {
                this.source = source;
                return this;
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
            ///     Sets the reporter for this tracer.
            /// </summary>
            /// <returns><see cref="this"/></returns>
            /// <param name="reporter">The reporter for this tracer.</param>
            public Builder WithReporter(IReporter reporter)
            {
                this.reporter = reporter;
                return this;
            }

            /// <summary>
            ///     Builds and returns the <see cref="WavefrontTracer"/> instance based on the
            ///     provided configuration.
            /// </summary>
            /// <returns>A <see cref="WavefrontTracer"/>.</returns>
            public WavefrontTracer Build()
            {
                if (string.IsNullOrEmpty(source))
                {
                    source = GetDefaultSource();
                }
                if (reporter == null)
                {
                    reporter = new ConsoleReporter(source);
                }
                return new WavefrontTracer(reporter, tags);
            }

            private static string GetDefaultSource()
            {
                try
                {
                    return Dns.GetHostEntry("LocalHost").HostName;
                }
                catch (Exception)
                {
                    return "wavefront-tracer";
                }
            }
        }

        private WavefrontTracer(IReporter reporter, IList<KeyValuePair<string, string>> tags)
        {
            ScopeManager = new AsyncLocalScopeManager();
            this.reporter = reporter;
            Tags = tags;
            // TODO: figure out sampling
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

        internal DateTimeOffset CurrentTimestamp()
        {
            return DateTimeOffset.UtcNow;
        }

        /// <summary>
        ///     Closes the reporter for this tracer.
        /// </summary>
        public void Close()
        {
            reporter.Close();
        }
    }
}
