namespace Test.Publisher
{
    using System;

    /// <summary>Assertion helpers for the publisher test cases.</summary>
    internal static class Fixture
    {
        internal static void Check(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
        }

        internal static void Throws<T>(Action action) where T : Exception
        {
            try { action(); }
            catch (T) { return; }
            throw new InvalidOperationException("Expected exception: " + typeof(T).Name);
        }

        internal static async System.Threading.Tasks.Task ThrowsAsync<T>(Func<System.Threading.Tasks.Task> action) where T : Exception
        {
            try { await action().ConfigureAwait(false); }
            catch (T) { return; }
            throw new InvalidOperationException("Expected exception: " + typeof(T).Name);
        }
    }
}
