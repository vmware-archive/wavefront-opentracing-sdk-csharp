using OpenTracing.Propagation;
using System;
using System.Collections.Generic;

namespace Wavefront.OpenTracing.SDK.CSharp.Propagation
{
    /// <summary>
    ///     Bridge for extracting/injecting Jaeger headers to/from a WavefrontSpanContext.
    ///     
    ///     Essentially allows for extracting Jaeger HTTP headers and creating a
    ///     WavefrontSpanContext injecting Jaeger aware HTTP headers from a WavefrontSpanContext.
    /// </summary>
    public class JaegerWavefrontPropagator : IPropagator
    {
        private static readonly string BaggagePrefix = "baggage-";
        private static readonly string TraceIdKey = "trace-id";
        private static readonly string ParentIdKey = "parent-id";
        private static readonly string SamplingDecisionKey = "sampling-decision";

        private readonly string traceIdHeader;
        private readonly string baggagePrefix;

        private JaegerWavefrontPropagator(string traceIdHeader, string baggagePrefix)
        {
            this.traceIdHeader = traceIdHeader;
            this.baggagePrefix = baggagePrefix;
        }

        /// <inheritdoc />
        public void Inject<TCarrier>(WavefrontSpanContext spanContext, TCarrier carrier)
        {
            if (carrier is ITextMap textMap)
            {
                textMap.Set(traceIdHeader, ContextToTraceIdHeader(spanContext));
                foreach (var entry in spanContext.GetBaggageItems())
                {
                    textMap.Set(baggagePrefix + entry.Key, entry.Value);
                }
                if (spanContext.IsSampled())
                {
                    textMap.Set(SamplingDecisionKey, spanContext.GetSamplingDecision().ToString());
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
                string parentId = null;
                var baggage = new Dictionary<string, string>();
                bool? samplingDecision = null;

                foreach (var entry in textMap)
                {
                    // normalize keys using invariant culture (i.e., independent of locale)
                    string key = entry.Key.ToLowerInvariant();
                    if (key.Equals(traceIdHeader.ToLowerInvariant()))
                    {
                        string[] traceData = ContextFromTraceIdHeader(entry.Value);
                        if (traceData == null)
                        {
                            continue;
                        }
                        traceId = HexToGuid(traceData[0]);
                        spanId = HexToGuid(traceData[1]);
                        // setting parentId as current spanId
                        parentId = spanId.HasValue ? spanId.Value.ToString() : null;
                        samplingDecision = traceData[3].Equals("1");
                    }
                    else if (key.StartsWith(baggagePrefix.ToLowerInvariant()))
                    {
                        baggage[StripPrefix(entry.Key)] = entry.Value;
                    }
                }

                if (!traceId.HasValue || !spanId.HasValue)
                {
                    return null;
                }
                if (parentId.Trim().Length > 0 && !parentId.Trim().Equals("null"))
                {
                    baggage[ParentIdKey] = parentId;
                }
                return new WavefrontSpanContext(
                    traceId.Value, spanId.Value, baggage, samplingDecision);
            }
            else
            {
                throw new ArgumentException("Invalid carrier " + carrier.GetType());
            }
        }

        /// <summary>
        ///     Extracts traceId, spanId, parentId and samplingDecision from the 'uber-trace-id'
        ///     HTTP header value that is in the format traceId:spanId:parentId:samplingDecision.
        /// </summary>
        private string[] ContextFromTraceIdHeader(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }
            // Jaeger HTTP headers may be URL encoded, so we need to unescape before splitting
            value = Uri.UnescapeDataString(value);
            string[] ctx = value.Split(':');
            if (ctx.Length != 4 || string.IsNullOrEmpty(ctx[0]))
            {
                return null;
            }
            return ctx;
        }

        /// <summary>
        ///     Extracts traceId and spanId from a WavefrontSpanContext and constructs a Jaeger
        ///     client compatible header of the form traceId:spanId:parentId:samplingDecision.
        /// </summary>
        private string ContextToTraceIdHeader(WavefrontSpanContext context)
        {
            string traceId = GuidToHex(context.GetTraceId());
            string spanId = GuidToHex(context.GetSpanId());
            bool samplingDecision = context.GetSamplingDecision() ?? false;
            string decision = samplingDecision ? "1" : "0";
            string parentId = Guid.TryParse(context.GetBaggageItem(ParentIdKey), out Guid guid) ?
                GuidToHex(guid) : "0";
            return $"{traceId}:{spanId}:{parentId}:{decision}";
        }

        internal static string GuidToHex(Guid guid)
        {
            string hex = guid.ToString("N").TrimStart('0');
            return hex.Length > 0 ? hex : "0";
        }

        internal static Guid? HexToGuid(string id)
        {
            if (id.Length > 32)
            {
                return null;
            }
            return Guid.Parse(new string('0', 32 - id.Length) + id);
        }

        private string StripPrefix(string key)
        {
            return key.Substring(baggagePrefix.Length);
        }

        /// <summary>
        ///     A builder for <see cref="JaegerWavefrontPropagator"/> instances.
        /// </summary>
        public class Builder
        {
            private string traceIdHeader = TraceIdKey;
            private string baggagePrefix = BaggagePrefix;

            /// <summary>
            ///     Sets a custom traceId header key. The default is "trace-id".
            /// </summary>
            /// <param name="traceIdHeader">The traceId header key.</param>
            /// <returns><see cref="this"/></returns>
            public Builder WithTraceIdHeader(string traceIdHeader)
            {
                this.traceIdHeader = traceIdHeader;
                return this;
            }

            /// <summary>
            ///     Sets a custom baggage prefix. The default is "baggage-".
            /// </summary>
            /// <param name="baggagePrefix">The baggage prefix.</param>
            /// <returns><see cref="this"/></returns>
            /// 
            public Builder WithBaggagePrefix(string baggagePrefix)
            {
                this.baggagePrefix = baggagePrefix;
                return this;
            }

            /// <summary>
            ///     Builds and returns a <see cref="JaegerWavefrontPropagator"/> instance based on
            ///     the given configuration.
            /// </summary>
            /// <returns>A <see cref="JaegerWavefrontPropagator"/>.</returns>
            public JaegerWavefrontPropagator Build()
            {
                return new JaegerWavefrontPropagator(traceIdHeader, baggagePrefix);
            }
        }
    }
}
