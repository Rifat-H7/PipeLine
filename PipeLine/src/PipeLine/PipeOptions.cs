using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PipeLine
{
    /// <summary>
    /// Configuration options for <see cref="PipeServer"/> and <see cref="PipeClient"/>.
    /// </summary>
    public sealed class PipeOptions
    {
        /// <summary>
        /// The name used as the base for the two internal pipe names.
        /// Default: <c>"pipechat"</c>
        /// <para>
        /// Two pipes are created: <c>{PipeName}_s2c</c> (server→client)
        /// and <c>{PipeName}_c2s</c> (client→server).
        /// </para>
        /// </summary>
        public string PipeName { get; set; } = "pipechat";

        /// <summary>
        /// Maximum allowed size (in bytes) for a single incoming message.
        /// Messages exceeding this size will cause the connection to be dropped.
        /// Default: 1 MB (1,048,576 bytes).
        /// </summary>
        public int MaxMessageSizeBytes { get; set; } = 1_048_576;

        /// <summary>
        /// Timeout in milliseconds for the client to connect to the server.
        /// Default: 10,000 ms (10 seconds).
        /// </summary>
        public int ConnectTimeoutMs { get; set; } = 10_000;
    }
}
