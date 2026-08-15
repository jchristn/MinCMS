namespace MinCms.Test.Shared
{
    using System.Collections.Generic;
    using Touchstone.Core;

    /// <summary>
    /// Central source of truth for the MinCMS test suite. Every runner
    /// (Test.Automated CLI, Test.Xunit, Test.Nunit) consumes <see cref="All"/>.
    /// </summary>
    public static partial class MinCmsSuites
    {
        /// <summary>
        /// All test suites exercised by every adapter.
        /// </summary>
        public static IReadOnlyList<TestSuiteDescriptor> All
        {
            get
            {
                return new List<TestSuiteDescriptor>
                {
                    ModelSuite(),
                    SettingsSuite(),
                    SerializationSuite(),
                    S3ServiceSuite(),
                    CollectionServiceSuite(),
                    ApiHostSuite()
                };
            }
        }
    }
}
