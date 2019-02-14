using Microsoft.Extensions.Logging;
using System.IO;
using System.Linq;
using Wavefront.SDK.CSharp.Common;

namespace Wavefront.OpenTracing.SDK.CSharp.Reporting
{
    /// <summary>
    ///     Reporter that reports tracing spans to Wavefront via <see cref="IWavefrontSender"/>.
    /// </summary>
    public class WavefrontSpanReporter : IReporter
    {
        private static readonly ILogger logger =
            Logging.LoggerFactory.CreateLogger<WavefrontSpanReporter>();

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
            ///     Builds and returns a <see cref="WavefrontSpanReporter"/> for sending
            ///     OpenTracing spans to an <see cref="IWavefrontSender"/> that can send to
            ///     Wavefront via either proxy or direct ingestion.
            /// </summary>
            /// <returns>A <see cref="WavefrontSpanReporter"/>.</returns>
            /// <param name="wavefrontSender">The Wavefront sender.</param>
            public WavefrontSpanReporter Build(IWavefrontSender wavefrontSender)
            {
                return new WavefrontSpanReporter(wavefrontSender, source);
            }
        }

        private WavefrontSpanReporter(IWavefrontSender wavefrontSender, string source)
        {
            WavefrontSender = wavefrontSender;
            Source = source;
        }

        /// <inheritdoc />
        public void Report(WavefrontSpan span)
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
                    context.GetSpanId(), parents, follows, span.GetTagsAsList().ToList(), null
                );
            }
            catch (IOException)
            {
                if (logger.IsEnabled(LogLevel.Debug))
                {
                    logger.LogDebug("Dropping span " + span);
                }
            }
        }

        /// <inheritdoc />
        public int GetFailureCount()
        {
            return WavefrontSender.GetFailureCount();
        }

        /// <inheritdoc />
        public void Close()
        {
            // Flush buffer & close client.
            WavefrontSender.Close();
        }
    }
}
