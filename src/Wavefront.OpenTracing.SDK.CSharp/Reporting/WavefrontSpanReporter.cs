using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Wavefront.SDK.CSharp.Common;
using Wavefront.SDK.CSharp.Common.Metrics;

namespace Wavefront.OpenTracing.SDK.CSharp.Reporting
{
    /// <summary>
    ///     Reporter that reports tracing spans to Wavefront via <see cref="IWavefrontSender"/>.
    /// </summary>
    public class WavefrontSpanReporter : IReporter
    {
        private static readonly ILogger logger =
            Logging.LoggerFactory.CreateLogger<WavefrontSpanReporter>();

        private readonly BlockingCollection<WavefrontSpan> spanBuffer;
        private readonly Random random;
        private readonly double logPercent;
        private readonly Task sendTask;

        private WavefrontSdkMetricsRegistry sdkMetricsRegistry;
        private WavefrontSdkCounter spansDropped;
        private WavefrontSdkCounter spansReceived;
        private WavefrontSdkCounter reportErrors;

        /// <summary>
        ///     Gets the Wavefront proxy or direct ingestion sender.
        /// </summary>
        /// <value>The Wavefront sender.</value>
        public IWavefrontSender WavefrontSender { get; }

        /// <summary>
        ///     Gets the source for this reporter.
        /// </summary>
        /// <value>The source of all spans.</value>
        public string Source { get; }

        /// <summary>
        ///     A builder for <see cref="WavefrontSpanReporter"/> instances.
        /// </summary>
        public sealed class Builder
        {
            private string source;
            private int maxQueueSize = 50000;
            private double logPercent = 0.1;

            /// <summary>
            ///     Initializes a new instance of the <see cref="Builder"/> class.
            /// </summary>
            public Builder()
            {
                source = Utils.GetDefaultSource();
            }

            /// <summary>
            ///     Sets the source for this reporter.
            /// </summary>
            /// <returns><see cref="this"/></returns>
            /// <param name="source">The source of all spans.</param>
            public Builder WithSource(string source)
            {
                this.source = source;
                return this;
            }

            /// <summary>
            ///     Sets the max queue size of in-memory buffer. Incoming spans are dropped if
            ///     buffer is full.
            /// </summary>
            /// <returns><see cref="this"/></returns>
            /// <param name="maxQueueSize">The max queue size of in-memory buffer.</param>
            public Builder WithMaxQueueSize(int maxQueueSize)
            {
                if (maxQueueSize <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(maxQueueSize),
                        "invalid max queue size");
                }
                this.maxQueueSize = maxQueueSize;
                return this;
            }

            /// <summary>
            ///     Sets the percent of log messages to be logged. Defaults to 10%.
            /// </summary>
            /// <returns><see cref="this"/></returns>
            /// <param name="logPercent">A value between 0.0 and 1.0.</param>
            public Builder WithLoggingPercent(double logPercent)
            {
                if (logPercent < 0.0 || logPercent > 1.0)
                {
                    throw new ArgumentOutOfRangeException(nameof(logPercent),
                        "invalid logging percent");
                }
                this.logPercent = logPercent;
                return this;
            }

            /// <summary>
            ///     Builds and returns a <see cref="WavefrontSpanReporter"/> for sending
            ///     OpenTracing spans to an <see cref="IWavefrontSender"/> that can send to
            ///     Wavefront via either proxy or direct ingestion.
            /// </summary>
            /// <returns>A <see cref="WavefrontSpanReporter"/>.</returns>
            /// <param name="wavefrontSender">The Wavefront sender.</param>
            public WavefrontSpanReporter Build(IWavefrontSender wavefrontSender)
            {
                return new WavefrontSpanReporter(wavefrontSender, source, maxQueueSize, logPercent);
            }
        }

        private WavefrontSpanReporter(IWavefrontSender wavefrontSender, string source,
            int maxQueueSize, double logPercent)
        {
            WavefrontSender = wavefrontSender;
            Source = source;
            spanBuffer = new BlockingCollection<WavefrontSpan>(maxQueueSize);
            random = new Random();
            this.logPercent = logPercent;
            sendTask = Task.Factory.StartNew(SendLoop, TaskCreationOptions.LongRunning);
        }


        /// <inheritdoc />
        public void Report(WavefrontSpan span)
        {
            spansReceived?.Inc();
            if (!spanBuffer.TryAdd(span))
            {
                spansDropped?.Inc();
                if (LoggingAllowed())
                {
                    logger.LogWarning("Buffer full, dropping span: " + span);
                    if (spansDropped != null)
                    {
                        logger.LogWarning("Total spans dropped: " + spansDropped.Count);
                    }
                }
            }
        }

        private void SendLoop()
        {
            foreach (WavefrontSpan span in spanBuffer.GetConsumingEnumerable())
            {
                try
                {
                    Send(span);
                }
                catch (Exception e)
                {
                    logger.LogWarning(0, e, "Error processing buffer");
                }
            }
        }

        private void Send(WavefrontSpan span)
        {
            try
            {
                var context = (WavefrontSpanContext)span.Context;
                var parentReferences = span.GetParents();
                var followReferences = span.GetFollows();

                var parents = parentReferences?
                .Select(parent => parent.SpanContext.GetSpanId())
                .ToList();

                var follows = followReferences?
                    .Select(follow => follow.SpanContext.GetSpanId())
                    .ToList();

                WavefrontSender.SendSpan(
                    span.GetOperationName(), span.GetStartTimeMicros() / 1000,
                    span.GetDurationMicros() / 1000, Source, context.GetTraceId(),
                    context.GetSpanId(), parents, follows, span.GetTagsAsList().ToList(),
                    span.GetSpanLogs().ToList()
                );
            }
            catch (IOException e)
            {
                if (LoggingAllowed())
                {
                    logger.LogWarning(0, e, "Error reporting span: " + span);
                }
                spansDropped?.Inc();
                reportErrors?.Inc();
            }
        }

        /// <inheritdoc />
        public int GetFailureCount()
        {
            return WavefrontSender.GetFailureCount();
        }

        internal void SetSdkMetricsRegistry(WavefrontSdkMetricsRegistry sdkMetricsRegistry)
        {
            this.sdkMetricsRegistry = sdkMetricsRegistry;

            // init internal metrics
            this.sdkMetricsRegistry.Gauge("reporter.queue.size", () => spanBuffer.Count);
            this.sdkMetricsRegistry.Gauge("reporter.queue.remaining_capacity",
                () => spanBuffer.BoundedCapacity - spanBuffer.Count);
            spansReceived = this.sdkMetricsRegistry.Counter("reporter.spans.received");
            spansDropped = this.sdkMetricsRegistry.Counter("reporter.spans.dropped");
            reportErrors = this.sdkMetricsRegistry.Counter("reporter.errors");
        }

        private bool LoggingAllowed()
        {
            return random.NextDouble() < logPercent;
        }

        /// <inheritdoc />
        public void Close()
        {
            spanBuffer.CompleteAdding();
            try
            {
                // wait for 5 secs max
                _ = Task.WhenAny(sendTask, Task.Delay(5000)).GetAwaiter().GetResult();
            }
            catch
            {
                // no-op
            }
        }
    }
}
