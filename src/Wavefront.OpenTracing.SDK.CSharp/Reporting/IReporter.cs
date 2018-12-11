namespace Wavefront.OpenTracing.SDK.CSharp.Reporting
{
    /// <summary>
    ///     Interface for reporting finished spans.
    /// </summary>
    public interface IReporter
    {
        /// <summary>
        ///     Reports OpenTracing span to Wavefront.
        /// </summary>
        /// <param name="span">The <see cref="WavefrontSpan"/> to report.</param>
        void Report(WavefrontSpan span);

        /// <summary>
        ///     Gets total failure count reported by this reporter.
        /// </summary>
        /// <returns>The total failure count.</returns>
        int GetFailureCount();

        /// <summary>
        ///     Closes the reporter. Will flush in-flight buffer before closing.
        /// </summary>
        void Close();
    }
}
