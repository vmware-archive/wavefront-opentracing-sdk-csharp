using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using OpenTracing;
using OpenTracing.Tag;

namespace Wavefront.OpenTracing.CSharp.SDK
{
    /// <summary>
    ///     Represents a thread-safe Wavefront trace span based on OpenTracing's
    ///     <see cref="ISpan"/>.
    /// </summary>
    public class WavefrontSpan : ISpan
    {
        private readonly WavefrontTracer tracer;
        private readonly DateTime startTimestampUtc;
        private readonly IList<KeyValuePair<string, string>> tags;
        private readonly IList<Reference> parents;
        private readonly IList<Reference> follows;

        private string operationName;
        private TimeSpan duration;
        private WavefrontSpanContext spanContext;
        private bool finished;

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
            this.tags = tags;
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
                tags.Add(new KeyValuePair<string, string>(key, value.ToString()));
            }
            return this;
        }

        /// <summary>
        ///     Not supported.
        /// </summary>
        /// <returns><see cref="this"/></returns>
        public ISpan Log(IEnumerable<KeyValuePair<string, object>> fields)
        {
            // No-op
            return this;
        }

        /// <summary>
        ///     Not supported.
        /// </summary>
        /// <returns><see cref="this"/></returns>
        public ISpan Log(DateTimeOffset timestamp, IEnumerable<KeyValuePair<string, object>> fields)
        {
            // No-op
            return this;
        }

        /// <summary>
        ///     Not supported.
        /// </summary>
        /// <returns><see cref="this"/></returns>
        public ISpan Log(string @event)
        {
            // No-op
            return this;
        }

        /// <summary>
        ///     Not supported.
        /// </summary>
        /// <returns><see cref="this"/></returns>
        public ISpan Log(DateTimeOffset timestamp, string @event)
        {
            // No-op
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
            tracer.ReportSpan(this);
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
        ///     Get the start timestamp of the span, in milliseconds elapsed since the epoch. 
        /// </summary>
        /// <returns>The start timestamp in milliseconds.</returns>
        public long GetStartTimeMillis()
        {
            return ((DateTimeOffset)startTimestampUtc).ToUnixTimeMilliseconds();
        }

        /// <summary>
        ///     Gets the duration of the span in milliseconds.
        /// </summary>
        /// <returns>The span duration in milliseconds.</returns>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public long GetDurationMillis()
        {
            return (long)duration.TotalMilliseconds;
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
    }
}
