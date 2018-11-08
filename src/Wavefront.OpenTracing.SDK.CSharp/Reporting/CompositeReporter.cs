using System.Collections.Generic;

namespace Wavefront.OpenTracing.SDK.CSharp.Reporting
{
    /// <summary>
    ///     Reporter that delegates to multiple other reporters for reporting.
    ///     Useful for debugging by reporting spans to console and to backend reporters.
    /// </summary>
    public class CompositeReporter : IReporter
    {
        private readonly IList<IReporter> reporters = new List<IReporter>();

        /// <summary>
        ///     Initializes a new instance of the <see cref="CompositeReporter"/> class
        ///     that is composed of the given reporters.
        /// </summary>
        /// <param name="reporters">Reporters.</param>
        public CompositeReporter(params IReporter[] reporters)
        {
            foreach (var reporter in reporters)
            {
                this.reporters.Add(reporter);
            }
        }

        /// <summary>
        ///     Reports an OpenTracing span using each of the delegate reporters.
        /// </summary>
        /// <param name="span">The <see cref="WavefrontSpan"/> to report.</param>
        public void Report(WavefrontSpan span)
        {
            foreach (var reporter in reporters)
            {
                reporter.Report(span);
            }
        }

        /// <summary>
        ///     Gets a sum of the failure counts across all the delegate reporters.
        /// </summary>
        /// <returns>The total failure count.</returns>
        public int GetFailureCount()
        {
            int result = 0;
            foreach (var reporter in reporters)
            {
                result += reporter.GetFailureCount();
            }
            return result;
        }

        /// <summary>
        ///     Closes each of the delegate reporters.
        /// </summary>
        public void Close()
        {
            foreach (var reporter in reporters)
            {
                reporter.Close();
            }
        }
    }
}
