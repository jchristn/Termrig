namespace Test.Nunit
{
    using System.Collections;
    using System.Threading;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using Test.Shared;
    using Touchstone.Core;
    using Touchstone.NunitAdapter;

    /// <summary>
    /// NUnit data-driven runner with one test per Touchstone descriptor.
    /// </summary>
    [TestFixture]
    public sealed class TermrigNunitTests
    {
        /// <summary>
        /// Run a single descriptor.
        /// </summary>
        /// <param name="testCase">Test case.</param>
        /// <returns>Task.</returns>
        [Test]
        [TestCaseSource(nameof(TestCases))]
        public async Task RunTest(TestCaseDescriptor testCase)
        {
            await testCase.ExecuteAsync(CancellationToken.None).ConfigureAwait(false);
        }

        private static IEnumerable TestCases()
        {
            return new TouchstoneTestCaseSource(TermrigSuites.All);
        }
    }
}
