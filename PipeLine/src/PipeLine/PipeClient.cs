using PipeLine.Internal;
using System.IO.Pipes;

namespace PipeLine
{
    /// <summary>
    /// A Named Pipes client that connects to a <see cref="PipeServer"/> and supports
    /// full-duplex, overlap-safe message exchange.
    /// </summary>
    /// <example>
    /// <code>
    /// var client = new PipeClient();
    ///
    /// client.OnMessageReceived += async msg =>
    /// {
    ///     Console.WriteLine($"Server says: {msg.Content}");
    /// };
    ///
    /// await client.ConnectAsync();
    /// await client.SendAsync("Hello from client!");
    /// </code>
    /// </example>
    public sealed class PipeClient : IAsyncDisposable
    {
        private readonly PipeOptions _options;
        private readonly string _serverName;
        private readonly string _s2cPipe;  // server → client  (client reads this)
        private readonly string _c2sPipe;  // client → server  (client writes this)

        private PipeMessenger? _messenger;
        private readonly CancellationTokenSource _cts = new();

        /// <summary>Raised when a message is received from the server.</summary>
        public event Func<PipeMessage, Task>? OnMessageReceived;

        /// <summary>Raised when the server disconnects.</summary>
        public event Func<Task>? OnDisconnected;

        /// <summary>Gets whether this client is currently connected to the server.</summary>
        public bool IsConnected { get; private set; }

        /// <summary>
        /// Initialises the client targeting <c>localhost</c> with default options.
        /// </summary>
        public PipeClient() : this(".", new PipeOptions()) { }

        /// <summary>
        /// Initialises the client with custom <paramref name="options"/> targeting <c>localhost</c>.
        /// </summary>
        public PipeClient(PipeOptions options) : this(".", options) { }

        /// <summary>
        /// Initialises the client targeting a specific <paramref name="serverName"/>
        /// (use <c>"."</c> for localhost, or a machine name / IP for remote — Windows only for remote).
        /// </summary>
        public PipeClient(string serverName, PipeOptions options)
        {
            _serverName = serverName;
            _options = options;
            _s2cPipe = $"{options.PipeName}_s2c";
            _c2sPipe = $"{options.PipeName}_c2s";
        }

        /// <summary>
        /// Connects to the server. Throws <see cref="TimeoutException"/> if the server
        /// does not respond within <see cref="PipeOptions.ConnectTimeoutMs"/>.
        /// </summary>
        public async Task ConnectAsync(CancellationToken ct = default)
        {
            var inbound = new NamedPipeClientStream(
                _serverName, _s2cPipe, PipeDirection.In, System.IO.Pipes.PipeOptions.Asynchronous);

            var outbound = new NamedPipeClientStream(
                _serverName, _c2sPipe, PipeDirection.Out, System.IO.Pipes.PipeOptions.Asynchronous);

            try
            {
                await Task.WhenAll(
                    inbound.ConnectAsync(_options.ConnectTimeoutMs, ct),
                    outbound.ConnectAsync(_options.ConnectTimeoutMs, ct)).ConfigureAwait(false);
            }
            catch
            {
                await inbound.DisposeAsync().ConfigureAwait(false);
                await outbound.DisposeAsync().ConfigureAwait(false);
                throw;
            }

            IsConnected = true;

            var messenger = new PipeMessenger(inbound, outbound, _options.MaxMessageSizeBytes);
            _messenger = messenger;

            messenger.MessageReceived += async msg =>
            {
                if (OnMessageReceived is not null)
                    await OnMessageReceived(msg).ConfigureAwait(false);
            };

            messenger.Disconnected += async () =>
            {
                IsConnected = false;
                _messenger = null;
                if (OnDisconnected is not null)
                    await OnDisconnected().ConfigureAwait(false);
            };

            messenger.StartReading();
        }

        /// <summary>
        /// Sends a message to the server.
        /// Throws <see cref="InvalidOperationException"/> if not connected.
        /// </summary>
        public async Task SendAsync(string message, CancellationToken ct = default)
        {
            if (_messenger is null || !IsConnected)
                throw new InvalidOperationException("Not connected to a server. Call ConnectAsync() first.");

            await _messenger.SendAsync(message, ct).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            await _cts.CancelAsync().ConfigureAwait(false);
            if (_messenger is not null)
                await _messenger.DisposeAsync().ConfigureAwait(false);
            _cts.Dispose();
        }
    }
}
