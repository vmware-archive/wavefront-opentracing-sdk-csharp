using System;
using System.Collections.Generic;
using OpenTracing.Propagation;

namespace Wavefront.OpenTracing.SDK.CSharp.Propagation
{
    /// <summary>
    ///     Propagates contexts within <see cref="ITextMap"/>'s.
    /// </summary>
    public class TextMapPropagator : IPropagator
    {
        private static readonly string BaggagePrefix = "wf-ot-";
        private static readonly string TraceId = BaggagePrefix + "traceid";
        private static readonly string SpanId = BaggagePrefix + "spanid";
        private static readonly string Sample = BaggagePrefix + "sample";

        /// <inheritdoc />
        public void Inject<TCarrier>(WavefrontSpanContext spanContext, TCarrier carrier)
        {
            if (carrier is ITextMap textMap)
            {
                textMap.Set(TraceId, spanContext.TraceId);
                textMap.Set(SpanId, spanContext.SpanId);
                foreach (var entry in spanContext.GetBaggageItems())
                {
                    textMap.Set(BaggagePrefix + entry.Key, entry.Value);
                }
                if (spanContext.IsSampled())
                {
                    textMap.Set(Sample, spanContext.GetSamplingDecision().ToString());
                }
            }
            else
            {
                throw new ArgumentException("Invalid carrier " + carrier.GetType());
            }
        }

        /// <inheritdoc />
        public WavefrontSpanContext Extract<TCarrier>(TCarrier carrier)
        {
            if (carrier is ITextMap textMap)
            {
                Guid? traceId = null;
                Guid? spanId = null;
                IDictionary<string, string> baggage = null;
                bool? samplingDecision = null;

                foreach (var entry in textMap)
                {
                    // normalize keys using invariant culture (i.e., independent of locale)
                    string key = entry.Key.ToLowerInvariant();

                    if (key.Equals(TraceId))
                    {
                        traceId = Guid.Parse(entry.Value);
                    }
                    else if (key.Equals(SpanId))
                    {
                        spanId = Guid.Parse(entry.Value);
                    }
                    else if (key.Equals(Sample))
                    {
                        samplingDecision = bool.Parse(entry.Value);
                    }
                    else if (key.StartsWith(BaggagePrefix, StringComparison.Ordinal))
                    {
                        if (baggage == null)
                        {
                            baggage = new Dictionary<string, string>();
                        }
                        baggage.Add(StripPrefix(key), entry.Value);
                    }
                }

                if (!traceId.HasValue || !spanId.HasValue)
                {
                    return null;
                }
                return new WavefrontSpanContext(
                    traceId.Value, spanId.Value, baggage, samplingDecision);
            }
            else
            {
                throw new ArgumentException("Invalid carrier " + carrier.GetType());
            }
        }

        private string StripPrefix(string key)
        {
            return key.Substring(BaggagePrefix.Length);
        }
    }
}
