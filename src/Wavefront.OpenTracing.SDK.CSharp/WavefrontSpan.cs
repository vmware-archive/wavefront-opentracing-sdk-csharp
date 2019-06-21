using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using OpenTracing;
using OpenTracing.Tag;
using Wavefront.SDK.CSharp.Common;
using Wavefront.SDK.CSharp.Common.Metrics;
using Wavefront.SDK.CSharp.Entities.Tracing;

namespace Wavefront.OpenTracing.SDK.CSharp
{
    /// <summary>
    ///     Represents a thread-safe Wavefront trace span based on OpenTracing's
    ///     <see cref="ISpan"/>.
    /// </summary>
    public class WavefrontSpan : ISpan
    {
        private readonly WavefrontTracer tracer;
        private readonly DateTime startTimestampUtc;
        private readonly IList<Reference> parents;
        private readonly IList<Reference> follows;
        private readonly IList<SpanLog> spanLogs;

        private readonly WavefrontSdkCounter spansDiscarded;

        private IList<KeyValuePair<string, string>> tags;
        private IDictionary<string, KeyValuePair<string, string>> singleValuedTags;
        private string operationName;
        private TimeSpan duration;
        private WavefrontSpanContext spanContext;
        private bool? forceSampling;
        private bool finished;
        private bool isError;

        // Store it as a member variable so that we can efficiently retrieve the component tag.
        private string componentTagValue = Constants.NullTagValue;

        private static readonly ISet<string> SingleValuedTagKeys = new HashSet<string>
        {
            Constants.ApplicationTagKey,
            Constants.ServiceTagKey,
            Constants.ClusterTagKey,
            Constants.ShardTagKey
        };

        internal WavefrontSpan(
            WavefrontTracer tracer, string operationName, WavefrontSpanContext spanContext,
            DateTime startTimestampUtc, IList<Reference> parents, IList<Reference> follows,
            IList<KeyValuePair<string, string>> tags)
        {
            this.tracer = tracer;
            this.operationName = operationName;
            this.spanContext = spanContext;
            this.startTimestampUtc = startTimestampUtc;
            this.parents = parents;
            this.follows = follows;
            this.spanLogs = new List<SpanLog>();

            IList<KeyValuePair<string, string>> globalTags = tracer.Tags;
            if ((globalTags == null || globalTags.Count == 0) && (tags == null || tags.Count == 0))
            {
                this.tags = null;
            }
            else
            {
                this.tags = new List<KeyValuePair<string, string>>();
            }
            this.singleValuedTags = null;
            if (globalTags != null)
            {
                foreach (var tag in globalTags)
                {
                    SetTagObject(tag.Key, tag.Value);
                }
            }
            if (tags != null)
            {
                foreach (var tag in tags)
                {
                    SetTagObject(tag.Key, tag.Value);
                }
            }

            spansDiscarded = tracer.SdkMetricsRegistry?.Counter("spans.discarded");
        }

        /// <inheritdoc />
        public ISpanContext Context
        {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get => spanContext;
        }

        /// <inheritdoc />
        public ISpan SetTag(string key, string value)
        {
            return SetTagObject(key, value);
        }

        /// <inheritdoc />
        public ISpan SetTag(string key, bool value)
        {
            return SetTagObject(key, value);
        }

        /// <inheritdoc />
        public ISpan SetTag(string key, int value)
        {
            return SetTagObject(key, value);
        }

        /// <inheritdoc />
        public ISpan SetTag(string key, double value)
        {
            return SetTagObject(key, value);
        }

        /// <inheritdoc />
        public ISpan SetTag(BooleanTag tag, bool value)
        {
            return tag == null ? this : SetTagObject(tag.Key, value);
        }

        /// <inheritdoc />
        public ISpan SetTag(IntOrStringTag tag, string value)
        {
            return tag == null ? this : SetTagObject(tag.Key, value);
        }

        /// <inheritdoc />
        public ISpan SetTag(IntTag tag, int value)
        {
            return tag == null ? this : SetTagObject(tag.Key, value);
        }

        /// <inheritdoc />
        public ISpan SetTag(StringTag tag, string value)
        {
            return tag == null ? this : SetTagObject(tag.Key, value);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private WavefrontSpan SetTagObject(string key, object value)
        {
            if (!string.IsNullOrEmpty(key) && value != null)
            {
                if (tags == null)
                {
                    tags = new List<KeyValuePair<string, string>>();
                }

                KeyValuePair<string, string> tag;
                if (value is bool)
                {
                    tag = new KeyValuePair<string, string>(key, (bool)value ? "true" : "false");
                }
                else
                {
                    tag = new KeyValuePair<string, string>(key, value.ToString());
                }

                if (IsSingleValuedTagKey(key))
                {
                    if (singleValuedTags == null)
                    {
                        singleValuedTags = new Dictionary<string, KeyValuePair<string, string>>();
                    }
                    if (singleValuedTags.ContainsKey(key))
                    {
                        tags.Remove(singleValuedTags[key]);
                    }
                    singleValuedTags[key] = tag;
                }

                tags.Add(tag);

                if (Tags.Component.Key.Equals(key))
                {
                    componentTagValue = value.ToString();
                }

                // allow span to be reported if sampling.priority is > 0.
                if (Tags.SamplingPriority.Key.Equals(key) && value is int)
                {
                    forceSampling = (int)value > 0;
                    spanContext = spanContext.WithSamplingDecision(forceSampling.Value);
                }

                // allow span to be reported if error tag is set.
                if (forceSampling == null && Tags.Error.Key.Equals(key))
                {
                    if (value is bool && (bool)value)
                    {
                        forceSampling = true;
                        spanContext = spanContext.WithSamplingDecision(forceSampling.Value);
                    }
                }

                if (Tags.Error.Key.Equals(key))
                {
                    isError = true;
                }
            }
            return this;
        }

        public bool IsError()
        {
            return isError;
        }

        public string GetComponentTagValue()
        {
            return componentTagValue;
        }

        /// <inheritdoc />
        public ISpan Log(IEnumerable<KeyValuePair<string, object>> fields)
        {
            return Log(tracer.CurrentTimestamp(), fields);
        }

        /// <inheritdoc />
        public ISpan Log(DateTimeOffset timestamp, IEnumerable<KeyValuePair<string, object>> fields)
        {
            return Log(timestamp.UtcDateTime, fields);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private ISpan Log(DateTime timestampUtc, IEnumerable<KeyValuePair<string, object>> fields)
        {
            if (fields == null)
            {
                return this;
            }

            long timestampMicros = DateTimeUtils.UnixTimeMicroseconds(timestampUtc);
            IDictionary<string, string> fieldsDict = new Dictionary<string, string>();
            foreach (var field in fields)
            {
                fieldsDict[field.Key] = field.Value.ToString();
            }

            spanLogs.Add(new SpanLog(timestampMicros, fieldsDict));
            return this;
        }

        /// <inheritdoc />
        public ISpan Log(string @event)
        {
            return Log(tracer.CurrentTimestamp(), @event);
        }

        /// <inheritdoc />
        public ISpan Log(DateTimeOffset timestamp, string @event)
        {
            return Log(timestamp.UtcDateTime, @event);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private ISpan Log(DateTime timestampUtc, string @event)
        {
            if (!string.IsNullOrEmpty(@event))
            {
                long timestampMicros = DateTimeUtils.UnixTimeMicroseconds(timestampUtc);
                var fields = new Dictionary<string, string> { { LogFields.Event, @event } };
                spanLogs.Add(new SpanLog(timestampMicros, fields));
            }
            return this;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.Synchronized)]
        public ISpan SetBaggageItem(string key, string value)
        {
            spanContext = spanContext.WithBaggageItem(key, value);
            return this;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.Synchronized)]
        public string GetBaggageItem(string key)
        {
            return spanContext.GetBaggageItem(key);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.Synchronized)]
        public ISpan SetOperationName(string operationName)
        {
            this.operationName = operationName;
            return this;
        }

        /// <inheritdoc />
        public void Finish()
        {
            Finish(tracer.CurrentTimestamp());
        }

        /// <inheritdoc />
        public void Finish(DateTimeOffset finishTimestamp)
        {
            Finish(finishTimestamp.UtcDateTime);
        }

        private void Finish(DateTime finishTimestampUtc)
        {
            DoFinish(finishTimestampUtc - startTimestampUtc);
        }

        private void DoFinish(TimeSpan duration)
        {
            lock(this)
            {
                if (finished)
                {
                    return;
                }
                this.duration = duration;
                finished = true;
            }

            // perform another sampling for duration based samplers.
            if (forceSampling == null && 
                (!spanContext.IsSampled() || !spanContext.GetSamplingDecision().Value))
            {
                long traceId = Common.Utils.TraceIdToLong(spanContext.GetTraceId());
                bool decision = tracer.Sample(operationName, traceId, GetDurationMicros() / 1000);
                spanContext = decision ? spanContext.WithSamplingDecision(decision) : spanContext;
            }

            // only report spans if the sampling decision allows it.
            if (spanContext.IsSampled() && spanContext.GetSamplingDecision().Value)
            {
                tracer.ReportSpan(this);
            }
            else
            {
                spansDiscarded?.Inc();
            }

            // irrespective of sampling, report wavefront-generated metrics/histograms to Wavefront
            tracer.ReportWavefrontGeneratedData(this);
        }

        /// <summary>
        ///     Gets the string name for the operation this span represents.
        /// </summary>
        /// <returns>The name for the operation.</returns>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public string GetOperationName()
        {
            return operationName;
        }

        /// <summary>
        ///     Get the start timestamp of the span, in microseconds elapsed since the epoch. 
        /// </summary>
        /// <returns>The start timestamp in microseconds.</returns>
        public long GetStartTimeMicros()
        {
            return DateTimeUtils.UnixTimeMicroseconds(startTimestampUtc);
        }

        /// <summary>
        ///     Gets the duration of the span in microseconds.
        /// </summary>
        /// <returns>The span duration in microseconds.</returns>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public long GetDurationMicros()
        {
            return DateTimeUtils.TimeSpanToMicroseconds(duration);
        }

        /// <summary>
        ///     Gets the span's tags as an
        ///     <see cref="IReadOnlyList{KeyValuePair{string, string}}"/>.
        /// </summary>
        /// <returns>The tags as a readonly list.</returns>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public IReadOnlyList<KeyValuePair<string, string>> GetTagsAsList()
        {
            return tags == null ? ImmutableList.Create<KeyValuePair<string, string>>()
                    : tags.ToImmutableList();
        }

        /// <summary>
        ///     Gets the span's tags as an
        ///     <see cref="IReadOnlyDictionary{string, ICollection{string}}"/>.
        /// </summary>
        /// <returns>The tags as a readonly dictionary.</returns>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public IReadOnlyDictionary<string, ICollection<string>> GetTagsAsMap()
        {
            if (tags == null)
            {
                return ImmutableDictionary.Create<string, ICollection<string>>();
            }

            var dictionary = new Dictionary<string, ICollection<string>>();
            foreach (var tag in tags)
            {
                if (!dictionary.ContainsKey(tag.Key))
                {
                    dictionary.Add(tag.Key, new List<string>());
                }
                dictionary[tag.Key].Add(tag.Value);
            }
            return dictionary.ToImmutableDictionary();
        }

        /// <summary>
        ///     Returns the tag value for the given single-valued tag key. Returns null if no such
        ///     tag exists.
        /// </summary>
        /// <returns>The tag value.</returns>
        /// <param name="key">The single-valued tag key.</param>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public string GetSingleValuedTagValue(string key)
        {
            if (singleValuedTags == null || !singleValuedTags.ContainsKey(key))
            {
                return null;
            }
            return singleValuedTags[key].Value;
        }


        /// <summary>
        ///     Gets the context references that the span is a "child of", as an
        ///     <see cref="IReadOnlyList{Reference}"/>.
        /// </summary>
        /// <returns>The references that the span is a child of.</returns>
        public IReadOnlyList<Reference> GetParents()
        {
            return parents == null ? ImmutableList.Create<Reference>() : parents.ToImmutableList();
        }

        /// <summary>
        ///     Gets the context references that the span "follows from", as an
        ///     <see cref="IReadOnlyList{Reference}"/>.
        /// </summary>
        /// <returns>The references that the span follows from.</returns>
        public IReadOnlyList<Reference> GetFollows()
        {
            return follows == null ? ImmutableList.Create<Reference>() : follows.ToImmutableList();
        }

        /// <summary>
        ///     Gets a list of the span logs.
        /// </summary>
        /// <returns>The span logs.</returns>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public IReadOnlyList<SpanLog> GetSpanLogs()
        {
            return spanLogs.ToImmutableList();
        }

        /// <summary>
        ///     Returns a string that represents the current <see cref="WavefrontSpan"/>.
        /// </summary>
        /// <returns>A string that represents the current <see cref="WavefrontSpan"/>.</returns>
        public override string ToString()
        {
            return "WavefrontSpan{" +
                "operationName='" + operationName + '\'' +
                ", startTimestampUtc=" + startTimestampUtc +
                ", duration=" + duration +
                ", tags=" + tags +
                ", spanContext=" + spanContext +
                ", parents=" + parents +
                ", follows=" + follows +
                '}';
        }

        /// <summary>
        ///     Returns a boolean indicating whether the given tag key must be single-valued or not.
        /// </summary>
        /// <returns>
        ///     <c>true</c>, if the key must be single-valued, <c>false</c> otherwise.
        /// </returns>
        /// <param name="key">The tag key.</param>
        public static bool IsSingleValuedTagKey(string key)
        {
            return SingleValuedTagKeys.Contains(key);
        }
    }
}
