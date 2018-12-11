using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using OpenTracing;

namespace Wavefront.OpenTracing.SDK.CSharp
{
    /// <summary>
    ///     Represents a Wavefront span context based on OpenTracing's <see cref="ISpanContext"/>. 
    /// </summary>
    public class WavefrontSpanContext : ISpanContext
    {
        private readonly Guid traceId;
        private readonly Guid spanId;
        private readonly IDictionary<string, string> baggage;

        /// <summary>
        ///     Initializes a new instance of the <see cref="WavefrontSpanContext"/> class.
        /// </summary>
        /// <param name="traceId">The trace ID.</param>
        /// <param name="spanId">The span ID.</param>
        public WavefrontSpanContext(Guid traceId, Guid spanId)
            : this(traceId, spanId, null)
        {
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="WavefrontSpanContext"/> class.
        /// </summary>
        /// <param name="traceId">The trace ID.</param>
        /// <param name="spanId">The span ID.</param>
        /// <param name="baggage">The baggage items.</param>
        public WavefrontSpanContext(Guid traceId, Guid spanId,
                                    IDictionary<string, string> baggage)
        {
            this.traceId = traceId;
            this.spanId = spanId;

            // Expected that most contexts will have no baggage items except when propagated.
            this.baggage = baggage ?? new Dictionary<string, string>();
        }

        /// <inheritdoc />
        public string TraceId { get => traceId.ToString(); }

        /// <inheritdoc />
        public string SpanId { get => spanId.ToString(); }

        /// <inheritdoc />
        public IEnumerable<KeyValuePair<string, string>> GetBaggageItems()
        {
            return baggage.ToImmutableDictionary();
        }

        /// <summary>
        ///     Gets the value of the baggage item associated with the given key.
        /// </summary>
        /// <returns>The baggage item's value.</returns>
        /// <param name="key">The baggage item's key.</param>
        public string GetBaggageItem(string key)
        {
            return baggage[key];
        }

        /// <summary>
        ///     Returns a <see cref="WavefrontSpanContext"/> based off of <see cref="this"/>
        ///     with the addition of the given key and value as an additional baggage item.
        /// </summary>
        /// <returns>A copy of <see cref="this"/> with the additional baggage item.</returns>
        /// <param name="key">The baggage item's key.</param>
        /// <param name="value">The baggage item's value.</param>
        public WavefrontSpanContext WithBaggageItem(string key, string value)
        {
            var items = new Dictionary<string, string>(baggage);
            items.Add(key, value);
            return new WavefrontSpanContext(traceId, spanId, items);
        }

        /// <summary>
        ///     Gets the trace ID as a <see cref="Guid"/>.
        /// </summary>
        /// <returns>The trace ID as a Guid.</returns>
        public Guid GetTraceId()
        {
            return traceId;
        }

        /// <summary>
        ///     Gets the span ID as a <see cref="Guid"/>.
        /// </summary>
        /// <returns>The span ID as a Guid.</returns>
        public Guid GetSpanId()
        {
            return spanId;
        }

        /// <summary>
        ///     Returns a string that represents the current <see cref="WavefrontSpanContext"/>.
        /// </summary>
        /// <returns>
        ///     A string that represents the current <see cref="WavefrontSpanContext"/>.
        /// </returns>
        public override string ToString()
        {
            return "WavefrontSpanContext{" +
                "traceId=" + traceId +
                ", spanId=" + spanId +
                '}';
        }
    }
}
