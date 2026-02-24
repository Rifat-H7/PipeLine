using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PipeLine
{
    /// <summary>
    /// Represents a message received from the other end of a pipe connection.
    /// </summary>
    public sealed class PipeMessage
    {
        /// <summary>The UTF-8 text content of the message.</summary>
        public string Content { get; }

        /// <summary>The UTC timestamp when the message was received.</summary>
        public DateTime ReceivedAt { get; }

        internal PipeMessage(string content)
        {
            Content = content;
            ReceivedAt = DateTime.UtcNow;
        }

        /// <inheritdoc/>
        public override string ToString() => Content;
    }
}
