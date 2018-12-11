using System.Collections.Generic;
using OpenTracing.Propagation;

namespace Wavefront.OpenTracing.SDK.CSharp.Propagation
{
    /// <summary>
    ///     Registry of available propagators.
    /// </summary>
    public class PropagatorRegistry
    {
        private readonly IDictionary<object, IPropagator> propagators =
                new Dictionary<object, IPropagator>();

        /// <summary>
        ///     Initializes a new instance of the <see cref="PropagatorRegistry"/> class and
        ///     registers a <see cref="TextMapPropagator"/> and a <see cref="HttpPropagator"/>.
        /// </summary>
        public PropagatorRegistry()
        {
            Register(BuiltinFormats.TextMap, new TextMapPropagator());
            Register(BuiltinFormats.HttpHeaders, new HttpPropagator());
        }

        /// <summary>
        ///     Gets the propagator that is registered for the given carrier format.
        /// </summary>
        /// <returns>The propagator for the given carrier format.</returns>
        /// <param name="format">The format for the given carrier type.</param>
        /// <typeparam name="TCarrier">The type of the carrier.</typeparam>
        public IPropagator Get<TCarrier>(IFormat<TCarrier> format)
        {
            return propagators[format];
        }

        /// <summary>
        ///     Registers the given propagator for the given carrier format.
        /// </summary>
        /// <param name="format">The format for the given carrier type.</param>
        /// <param name="propagator">The propagator to register.</param>
        /// <typeparam name="TCarrier">The type of the carrier.</typeparam>
        public void Register<TCarrier>(IFormat<TCarrier> format, IPropagator propagator)
        {
            propagators.Add(format, propagator);
        }
    }
}
