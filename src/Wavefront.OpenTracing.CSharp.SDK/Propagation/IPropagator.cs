namespace Wavefront.OpenTracing.CSharp.SDK.Propagation
{
    /// <summary>
    ///     Interface for propagating span contexts over carriers.
    /// </summary>
    public interface IPropagator : IInjector, IExtractor
    {
    }
}
