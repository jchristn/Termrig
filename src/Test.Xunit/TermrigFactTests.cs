namespace Test.Xunit
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Test.Shared;
    using Touchstone.Core;
    using Touchstone.XunitAdapter;
    using Xunit;

    /// <summary>
    /// xUnit fact-style runner for all Touchstone descriptors.
    /// </summary>
    public sealed class TermrigFactTests : TouchstoneFactBase
    {
        /// <summary>
        /// Test suites.
        /// </summary>
        protected override IReadOnlyList<TestSuiteDescriptor> Suites
        {
            get
            {
                return TermrigSuites.All;
            }
        }

        /// <summary>
        /// Run all descriptors.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task RunAll()
        {
            await RunAllAsync().ConfigureAwait(true);
        }
    }
}
