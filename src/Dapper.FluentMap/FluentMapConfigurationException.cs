using System;

namespace Dapper.FluentMap
{
    /// <summary>
    /// Represents an invalid Dapper.FluentMap configuration.
    /// </summary>
    public class FluentMapConfigurationException : InvalidOperationException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FluentMapConfigurationException"/> class.
        /// </summary>
        public FluentMapConfigurationException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FluentMapConfigurationException"/> class
        /// with the specified error message.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public FluentMapConfigurationException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FluentMapConfigurationException"/> class
        /// with the specified error message and a reference to the inner exception.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public FluentMapConfigurationException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
