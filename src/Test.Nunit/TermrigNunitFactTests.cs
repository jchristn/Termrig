namespace Test.Nunit
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using Test.Shared;
    using Touchstone.Core;
    using Touchstone.NunitAdapter;

    /// <summary>
    /// NUnit fact-style runner for all Touchstone descriptors.
    /// </summary>
    [TestFixture]
    public sealed class TermrigNunitFactTests : TouchstoneNunitBase
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
        [Test]
        public async Task RunAll()
        {
            await RunAllAsync().ConfigureAwait(false);
        }
    }
}
