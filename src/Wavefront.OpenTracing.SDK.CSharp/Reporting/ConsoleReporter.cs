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

            var spanLogs = span.GetSpanLogs();
            if (spanLogs != null && spanLogs.Count > 0)
            {
                try
                {
                    Console.WriteLine("Span logs: " + Utils.SpanLogsToLineData(
                        context.GetTraceId(), context.GetSpanId(), spanLogs.ToList()));
                }
                catch (Exception e)
                {
                    Console.WriteLine("Error processing span logs: " + e);
                }
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
