namespace Wavefront.OpenTracing.CSharp.SDK
{
    /// <summary>
    ///     Represents a parent context reference.
    /// </summary>
    public class Reference
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="Reference"/> class for a given
        ///     span context and a given reference type.
        /// </summary>
        /// <param name="spanContext">The <see cref="WavefrontSpanContext"/>.</param>
        /// <param name="type">The reference type.</param>
        public Reference(WavefrontSpanContext spanContext, string type)
        {
            SpanContext = spanContext;
            Type = type;
        }

        /// <summary>
        ///     Gets the span context.
        /// </summary>
        /// <value>The span context.</value>
        public WavefrontSpanContext SpanContext { get; }

        /// <summary>
        ///     Gets the reference type.
        /// </summary>
        /// <value>The reference type.</value>
        public string Type { get; }
    }
}
