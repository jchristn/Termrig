namespace Test.Xunit
{
    using System.Threading;
    using System.Threading.Tasks;
    using Test.Shared;
    using Touchstone.Core;
    using Xunit;
    using global::Xunit.Abstractions;

    /// <summary>
    /// xUnit theory-style runner with one row per Touchstone descriptor.
    /// </summary>
    public sealed class TermrigTheoryTests
    {
        #region Private-Members

        private readonly ITestOutputHelper _Output;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the runner.
        /// </summary>
        /// <param name="output">xUnit output helper.</param>
        public TermrigTheoryTests(ITestOutputHelper output)
        {
            _Output = output;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Enumerate test cases.
        /// </summary>
        /// <returns>Theory data.</returns>
        public static TheoryData<TestCaseDescriptor> TestCases()
        {
            TheoryData<TestCaseDescriptor> data = new TheoryData<TestCaseDescriptor>();

            foreach (TestSuiteDescriptor suite in TermrigSuites.All)
            {
                foreach (TestCaseDescriptor testCase in suite.Cases)
                {
                    if (!testCase.Skip) data.Add(testCase);
                }
            }

            return data;
        }

        /// <summary>
        /// Run a single descriptor.
        /// </summary>
        /// <param name="testCase">Test case.</param>
        /// <returns>Task.</returns>
        [Theory]
        [MemberData(nameof(TestCases))]
        public async Task RunTest(TestCaseDescriptor testCase)
        {
            _Output.WriteLine("Running: " + testCase.DisplayName);
            await testCase.ExecuteAsync(CancellationToken.None).ConfigureAwait(true);
        }

        #endregion
    }
}
