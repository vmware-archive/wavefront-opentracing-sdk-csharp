using Wavefront.SDK.CSharp.Common.Application;

namespace Wavefront.OpenTracing.SDK.CSharp.Test
{
    /// <summary>
    ///     Utils class for various test methods to leverage.
    /// </summary>
    public static class Utils
    {
        public static ApplicationTags BuildApplicationTags()
        {
            return new ApplicationTags.Builder("myApplication", "myService").Build();
        }
    }
}
