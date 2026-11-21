namespace Termrig.Publisher
{
    using System;

    /// <summary>A packaging failure with a message that tells the operator what to fix.</summary>
    public sealed class PublisherException : Exception
    {
        /// <summary>Creates an exception with an actionable message.</summary>
        /// <param name="message">What failed and how to correct it.</param>
        public PublisherException(string message) : base(message)
        {
        }

        /// <summary>Creates an exception that wraps the underlying failure.</summary>
        /// <param name="message">What failed and how to correct it.</param>
        /// <param name="innerException">The original failure.</param>
        public PublisherException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
