using System;
using System.Linq;
using Wavefront.SDK.CSharp.Common;

namespace Wavefront.OpenTracing.SDK.CSharp.Reporting
{
    public class ConsoleReporter : IReporter
    {
        private readonly string source;
        private readonly string defaultSource = Utils.GetDefaultSource();

        public ConsoleReporter(string source)
        {
            this.source = source;
        }

        public void Report(WavefrontSpan span)
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

            var spanLine = Utils.TracingSpanToLineData(
                span.GetOperationName(), span.GetStartTimeMicros(), span.GetDurationMicros(),
                source, context.GetTraceId(), context.GetSpanId(), parents, follows,
                span.GetTagsAsList().ToList(), null, defaultSource
            );

            Console.WriteLine($"Finished span: sampling={context.GetSamplingDecision()} {spanLine}");

            var spanLogs = span.GetSpanLogs().ToList();
            if (spanLogs.Count > 0)
            {
                var spanLogsLine = Utils.SpanLogsToLineData(
                    //span.GetStartTimeMicros() / 1000, span.GetDurationMicros() / 1000,
                    context.GetTraceId(), context.GetSpanId(), spanLogs);

                Console.WriteLine($"span logs: {spanLogsLine}");
            }
        }

        public int GetFailureCount()
        {
            // No-op
            return 0;
        }

        public void Close()
        {
            // No-op
        }
    }
}
