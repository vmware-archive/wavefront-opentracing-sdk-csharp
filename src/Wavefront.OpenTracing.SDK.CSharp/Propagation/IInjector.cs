namespace Wavefront.OpenTracing.SDK.CSharp.Propagation
{
    /// <summary>
    ///     Interface for injecting span contexts into carriers.
    /// </summary>
    public interface IInjector
    {
        /// <summary>
        ///     Inject the given context into the given carrier.
        /// </summary>
        /// <param name="spanContext">The span context to serialize.</param>
        /// <param name="carrier">The carrier to inject the span context into.</param>
        /// <typeparam name="TCarrier">The type of the carrier.</typeparam>
        void Inject<TCarrier>(WavefrontSpanContext spanContext, TCarrier carrier);
    }
}
