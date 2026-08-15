namespace MinCms.Test.Nunit
{
    using System.Collections;
    using System.Threading;
    using System.Threading.Tasks;
    using MinCms.Test.Shared;
    using NUnit.Framework;
    using Touchstone.Core;
    using Touchstone.NunitAdapter;

    /// <summary>
    /// Exposes each shared Touchstone test case as an individual NUnit test via TestCaseSource.
    /// </summary>
    [TestFixture]
    public sealed class MinCmsNunitTests
    {
        private static IEnumerable TestCases()
        {
            return new TouchstoneTestCaseSource(MinCmsSuites.All);
        }

        /// <summary>Run a single shared test case.</summary>
        /// <param name="testCase">Shared test case descriptor.</param>
        [Test]
        [TestCaseSource(nameof(TestCases))]
        public async Task RunTest(TestCaseDescriptor testCase)
        {
            await testCase.ExecuteAsync(CancellationToken.None);
        }
    }
}
