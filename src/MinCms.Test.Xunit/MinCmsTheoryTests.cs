namespace MinCms.Test.Xunit
{
    using System.Threading;
    using System.Threading.Tasks;
    using MinCms.Test.Shared;
    using Touchstone.Core;
    using global::Xunit;

    /// <summary>
    /// Exposes each shared Touchstone test case as an individual xUnit theory row.
    /// </summary>
    public sealed class MinCmsTheoryTests
    {
        /// <summary>Non-skipped shared test cases projected as theory data.</summary>
        public static TheoryData<TestCaseDescriptor> TestCases()
        {
            TheoryData<TestCaseDescriptor> data = new TheoryData<TestCaseDescriptor>();

            foreach (TestSuiteDescriptor suite in MinCmsSuites.All)
            {
                foreach (TestCaseDescriptor testCase in suite.Cases)
                {
                    if (!testCase.Skip)
                        data.Add(testCase);
                }
            }

            return data;
        }

        /// <summary>Run a single shared test case.</summary>
        /// <param name="testCase">Shared test case descriptor.</param>
        [Theory]
        [MemberData(nameof(TestCases))]
        public async Task RunTest(TestCaseDescriptor testCase)
        {
            await testCase.ExecuteAsync(CancellationToken.None);
        }
    }
}
