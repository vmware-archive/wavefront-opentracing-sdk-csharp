using OpenTracing;
using OpenTracing.Propagation;
using OpenTracing.Util;
using System.Collections.Generic;
using System.Threading.Tasks;
using Wavefront.OpenTracing.SDK.CSharp.Reporting;
using Wavefront.OpenTracing.SDK.CSharp.Sampling;
using Wavefront.SDK.CSharp.Common.Application;
using Xunit;
using static Wavefront.OpenTracing.SDK.CSharp.Test.Utils;
using static Wavefront.SDK.CSharp.Common.Constants;

namespace Wavefront.OpenTracing.SDK.CSharp.Test
{
    /// <summary>
    ///     Unit tests for <see cref="WavefrontTracer"/>.
    /// </summary>
    public class WavefrontTracerTest
    {
        [Fact]
        public void TestInjectExtract()
        {
            var tracer = new WavefrontTracer
                .Builder(new ConsoleReporter("source"), BuildApplicationTags())
                .WithSampler(new ConstantSampler(true))
                .Build();

            var span = tracer.BuildSpan("testOp").Start();
            Assert.NotNull(span);

            span.SetBaggageItem("customer", "testCustomer");
            span.SetBaggageItem("requestType", "mobile");

            var dictionary = new Dictionary<string, string>();
            var textMapInjectAdapter = new TextMapInjectAdapter(dictionary);
            tracer.Inject(span.Context, BuiltinFormats.TextMap, textMapInjectAdapter);

            var textMapExtractAdapter = new TextMapExtractAdapter(dictionary);
            var context =
                (WavefrontSpanContext)tracer.Extract(BuiltinFormats.TextMap, textMapExtractAdapter);

            Assert.Equal("testCustomer", context.GetBaggageItem("customer"));
            Assert.Equal("mobile", context.GetBaggageItem("requesttype"));
            Assert.True(context.IsSampled());
            Assert.True(context.GetSamplingDecision());
        }

        [Fact]
        public void TestSampling()
        {
            var tracer = new WavefrontTracer
                .Builder(new ConsoleReporter("source"), BuildApplicationTags())
                .WithSampler(new ConstantSampler(true))
                .Build();
            Assert.True(tracer.Sample("testOp", 1L, 0));

            tracer = new WavefrontTracer
                .Builder(new ConsoleReporter("source"), BuildApplicationTags())
                .WithSampler(new ConstantSampler(false))
                .Build();
            Assert.False(tracer.Sample("testOp", 1L, 0));
        }

        [Fact]
        public void TestActiveSpan()
        {
            var tracer = new WavefrontTracer
                .Builder(new ConsoleReporter("source"), BuildApplicationTags())
                .Build();
            var scope = tracer.BuildSpan("testOp").StartActive();
            var span = tracer.ActiveSpan;
            Assert.NotNull(span);
            Assert.Equal(span, scope.Span);
        }

        [Fact]
        public void TestApplicationTags()
        {
            var customTags = new Dictionary<string, string>
            {
                { "customTag1", "customValue1" },
                { "customTag2", "customValue2" }
            };
            var applicationTags =
                new ApplicationTags.Builder("myApplication", "myService")
                                   .Cluster("myCluster")
                                   .Shard("myShard")
                                   .CustomTags(customTags)
                                   .Build();
            var tracer = new WavefrontTracer
                .Builder(new ConsoleReporter("source"), applicationTags)
                .Build();
            var span = (WavefrontSpan)tracer.BuildSpan("testOp").Start();
            Assert.NotNull(span);
            var spanTags = span.GetTagsAsMap();
            Assert.NotNull(spanTags);
            Assert.Equal(6, spanTags.Count);
            Assert.Contains(applicationTags.Application, spanTags[ApplicationTagKey]);
            Assert.Contains(applicationTags.Service, spanTags[ServiceTagKey]);
            Assert.Contains(applicationTags.Cluster, spanTags[ClusterTagKey]);
            Assert.Contains(applicationTags.Shard, spanTags[ShardTagKey]);
            Assert.Contains("customValue1", spanTags["customTag1"]);
            Assert.Contains("customValue2", spanTags["customTag2"]);
        }

        [Fact]
        public void TestGlobalTags()
        {
            var tracer = new WavefrontTracer
                .Builder(new ConsoleReporter("source"), BuildApplicationTags())
                .WithGlobalTag("foo", "bar")
                .Build();
            var span = (WavefrontSpan)tracer.BuildSpan("testOp").Start();
            Assert.NotNull(span);
            var spanTags = span.GetTagsAsMap();
            Assert.NotNull(spanTags);
            Assert.Equal(5, spanTags.Count);
            Assert.Contains("bar", spanTags["foo"]);

            var tags = new Dictionary<string, string>{ {"foo1", "bar1"}, {"foo2", "bar2"} };
            tracer = new WavefrontTracer
                .Builder(new ConsoleReporter("source"), BuildApplicationTags())
                .WithGlobalTags(tags)
                .Build();
            span = (WavefrontSpan)tracer.BuildSpan("testOp")
                                        .WithTag("foo3", "bar3")
                                        .Start();
            Assert.NotNull(span);
            spanTags = span.GetTagsAsMap();
            Assert.NotNull(spanTags);
            Assert.Equal(7, spanTags.Count);
            Assert.Contains("bar1", spanTags["foo1"]);
            Assert.Contains("bar2", spanTags["foo2"]);
            Assert.Contains("bar3", spanTags["foo3"]);
        }

        [Fact]
        public void TestGlobalMultiValuedTags()
        {
            var tracer = new WavefrontTracer
                .Builder(new ConsoleReporter("source"), BuildApplicationTags())
                .WithGlobalTag("key1", "value1")
                .WithGlobalTag("key1", "value2")
                .Build();
            var span = (WavefrontSpan)tracer.BuildSpan("testOp").Start();
            Assert.NotNull(span);
            var spanTags = span.GetTagsAsMap();
            Assert.NotNull(spanTags);
            Assert.Equal(5, spanTags.Count);
            Assert.Contains("value1", spanTags["key1"]);
            Assert.Contains("value2", spanTags["key1"]);
        }

        [Fact]
        public async Task TestActiveSpanReplacement()
        {
            var tracer = new WavefrontTracer
                .Builder(new ConsoleReporter("source"), BuildApplicationTags())
                .Build();

            // Start an isolated task and query for its result in another task/thread
            ISpan initialSpan = tracer.BuildSpan("initial").Start();

            // Explicitly pass a Span to be finished once a late calculation is done.
            var spans = await SubmitAnotherTask(tracer, initialSpan);

            Assert.Equal(3, spans.Count);
            Assert.Equal("initial", spans[0].GetOperationName()); // Isolated task
            Assert.Equal("subtask", spans[1].GetOperationName());
            Assert.Equal("task", spans[2].GetOperationName());

            var initialContext = (WavefrontSpanContext)spans[0].Context;
            var subtaskContext = (WavefrontSpanContext)spans[1].Context;
            var subtaskParentContext = spans[1].GetParents()[0].SpanContext;
            var taskContext = (WavefrontSpanContext)spans[2].Context;

            // task/subtask are part of the same trace, and subtask is a child of task
            Assert.Equal(subtaskContext.GetTraceId(), taskContext.GetTraceId());
            Assert.Equal(taskContext.GetSpanId(), subtaskParentContext.GetSpanId());

            // initial task is not related in any way to those two tasks
            Assert.NotEqual(initialContext.GetTraceId(), subtaskContext.GetTraceId());
            Assert.Empty(spans[0].GetParents());

            Assert.Null(tracer.ScopeManager.Active);
        }

        private async Task<IList<WavefrontSpan>> SubmitAnotherTask(
            ITracer tracer, ISpan initialSpan)
        {
            var spans = new List<WavefrontSpan>();

            // Create a new Span for this task
            using (var taskScope = tracer.BuildSpan("task").StartActive(true))
            {
                // Simulate work strictly related to the initial Span and finish it.
                using (var initialScope = tracer.ScopeManager.Activate(initialSpan, true))
                {
                    await Task.Delay(50);
                    spans.Add((WavefrontSpan)initialScope.Span);
                }

                // Restore the span for this task and create a subspan
                using (var subTaskScope = tracer.BuildSpan("subtask").StartActive(true))
                {
                    spans.Add((WavefrontSpan)subTaskScope.Span);
                }

                spans.Add((WavefrontSpan)taskScope.Span);
            }

            return spans;
        }

        [Fact]
        public async Task TestLateSpanFinish()
        {
            var tracer = new WavefrontTracer
                .Builder(new ConsoleReporter("source"), BuildApplicationTags())
                .Build();

            // Create a Span manually and use it as parent of a pair of subtasks
            var parentSpan = tracer.BuildSpan("parent").Start();
            var spans = new List<WavefrontSpan>();
            using (var scope = tracer.ScopeManager.Activate(parentSpan, false))
            {
                await SubmitTasks(tracer, spans);
                spans.Add((WavefrontSpan)parentSpan);
            }

            // Late-finish the parent Span now
            parentSpan.Finish();

            Assert.Equal(3, spans.Count);
            Assert.Equal("task1", spans[0].GetOperationName());
            Assert.Equal("task2", spans[1].GetOperationName());
            Assert.Equal("parent", spans[2].GetOperationName());

            AssertSameTrace(spans);

            Assert.Null(tracer.ActiveSpan);
        }


        /// <summary>
        ///     Fire away a few subtasks, passing a parent <see cref="ISpan"/> whose lifetime
        ///     is not tied at all to the children. There is no need to reactivate the parent Span,
        ///     as the context is propagated by <see cref="AsyncLocalScopeManager"/>.
        /// </summary>
        private Task SubmitTasks(WavefrontTracer tracer, IList<WavefrontSpan> spans)
        {
            var task1 = Task.Run(async () =>
            {
                using (var childScope1 = tracer.BuildSpan("task1").StartActive(true))
                {
                    await Task.Delay(55);
                    spans.Add((WavefrontSpan)childScope1.Span);
                }
            });

            var task2 = Task.Run(async () =>
            {
                using (var childScope2 = tracer.BuildSpan("task2").StartActive(true))
                {
                    await Task.Delay(85);
                    spans.Add((WavefrontSpan)childScope2.Span);
                }
            });

            return Task.WhenAll(task1, task2);
        }

        private static void AssertSameTrace(IList<WavefrontSpan> spans)
        {
            var rootSpan = spans[spans.Count - 1];
            var rootSpanContext = (WavefrontSpanContext)rootSpan.Context;

            for (int i = 0; i < spans.Count - 1; i++)
            {
                var spanContext = (WavefrontSpanContext)spans[i].Context;
                var parentContext = spans[i].GetParents()[0].SpanContext;

                Assert.Equal(rootSpanContext.GetTraceId(), spanContext.GetTraceId());
                Assert.Equal(rootSpanContext.GetSpanId(), parentContext.GetSpanId());
            }
        }
    }
}
