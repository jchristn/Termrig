namespace Test.Publisher
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using Touchstone.XunitAdapter;
    using Xunit;

    /// <summary>
    /// Runs every publisher case: manifest validation, installer templates, artwork, archives, and release checks.
    /// </summary>
    public sealed class PublisherTests : TouchstoneFactBase
    {
        /// <summary>
        /// Test suites.
        /// </summary>
        protected override IReadOnlyList<TestSuiteDescriptor> Suites
        {
            get
            {
                return PublisherCases.All;
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
