namespace Wavefront.OpenTracing.CSharp.SDK.Propagation
{
    /// <summary>
    ///     Interface for extracting span contexts from carriers.
    /// </summary>
    public interface IExtractor
    {
        /// <summary>
        ///     Extracts a span context for the given carrier.
        /// </summary>
        /// <returns>The extracted span context.</returns>
        /// <param name="carrier">The carrier to extract the span context from.</param>
        /// <typeparam name="TCarrier">The type of the carrier.</typeparam>
        WavefrontSpanContext Extract<TCarrier>(TCarrier carrier);
    }
}
