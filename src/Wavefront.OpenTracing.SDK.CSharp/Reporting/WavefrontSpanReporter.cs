using System;
using System.IO;
using System.Linq;
using System.Net;
using Microsoft.Extensions.Logging;
using Wavefront.SDK.CSharp.Common;
using static Wavefront.OpenTracing.SDK.CSharp.Common.Constants;

namespace Wavefront.OpenTracing.SDK.CSharp.Reporting
{
    /// <summary>
    ///     Reporter that reports tracing spans to Wavefront via <see cref="IWavefrontSender"/>.
    /// </summary>
    public class WavefrontSpanReporter : IReporter
    {
        private static readonly ILogger logger =
            Logging.LoggerFactory.CreateLogger<WavefrontSpanReporter>();

        private readonly IWavefrontSender wavefrontSender;
        private readonly string source;

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
                source = GetDefaultSource();
            }

            private static string GetDefaultSource()
            {
                try
                {
                    return Dns.GetHostEntry("LocalHost").HostName;
                }
                catch (Exception)
                {
                    return DefaultSource;
                }
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
            this.wavefrontSender = wavefrontSender;
            this.source = source;
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

                wavefrontSender.SendSpan(
                    span.GetOperationName(), span.GetStartTimeMillis(), span.GetDurationMillis(),
                    source, context.GetTraceId(), context.GetSpanId(), parents, follows,
                    span.GetTagsAsList().ToList(), null
                );
            }
            catch (IOException)
            {
                if (logger.IsEnabled(LogLevel.Debug))
                {
                    logger.Log(LogLevel.Debug, "Dropping span " + span);
                }
            }
        }

        /// <inheritdoc />
        public int GetFailureCount()
        {
            return wavefrontSender.GetFailureCount();
        }

        /// <inheritdoc />
        public void Close()
        {
            // Flush buffer & close client.
            wavefrontSender.Close();
        }
    }
}
