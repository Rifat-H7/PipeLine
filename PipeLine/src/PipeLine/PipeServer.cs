using PipeLine.Internal;
using System.IO.Pipes;

namespace PipeLine
{
    /// <summary>
    /// A Named Pipes server that accepts one client at a time and supports
    /// full-duplex, overlap-safe message exchange.
    /// </summary>
    /// <example>
    /// <code>
    /// var server = new PipeServer();
    ///
    /// server.OnMessageReceived += async msg =>
    /// {
    ///     Console.WriteLine($"Client says: {msg.Content}");
    ///     await server.SendAsync("Hello from server!");
    /// };
    ///
    /// server.OnClientConnected    += () => { Console.WriteLine("Client connected!"); return Task.CompletedTask; };
    /// server.OnClientDisconnected += () => { Console.WriteLine("Client disconnected."); return Task.CompletedTask; };
    ///
    /// await server.StartAsync(); // blocks, accepts clients in a loop
    /// </code>
    /// </example>
    public sealed class PipeServer : IAsyncDisposable
    {
        private readonly PipeOptions _options;
        private readonly string _s2cPipe;   // server → client
        private readonly string _c2sPipe;   // client → server

        private PipeMessenger? _messenger;
        private readonly CancellationTokenSource _cts = new();

        /// <summary>Raised when a message is received from the connected client.</summary>
        public event Func<PipeMessage, Task>? OnMessageReceived;

        /// <summary>Raised when a client successfully connects.</summary>
        public event Func<Task>? OnClientConnected;

        /// <summary>Raised when the connected client disconnects.</summary>
        public event Func<Task>? OnClientDisconnected;

        /// <summary>Gets whether a client is currently connected.</summary>
        public bool IsClientConnected { get; private set; }

        /// <summary>
        /// Initialises the server with default options (<c>PipeName = "pipechat"</c>).
        /// </summary>
        public PipeServer() : this(new PipeOptions()) { }

        /// <summary>Initialises the server with custom <paramref name="options"/>.</summary>
        public PipeServer(PipeOptions options)
        {
            _options = options;
            _s2cPipe = $"{options.PipeName}_s2c";
            _c2sPipe = $"{options.PipeName}_c2s";
        }

        /// <summary>
        /// Starts the server and waits for clients in a loop.
        /// Each accepted client is handled until it disconnects, then the server
        /// waits for the next one.
        /// <para>Cancelling <paramref name="ct"/> stops the server gracefully.</para>
        /// </summary>
        public async Task StartAsync(CancellationToken ct = default)
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _cts.Token);

            while (!linked.Token.IsCancellationRequested)
            {
                await AcceptClientAsync(linked.Token).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Sends a message to the currently connected client.
        /// Throws <see cref="InvalidOperationException"/> if no client is connected.
        /// </summary>
        public async Task SendAsync(string message, CancellationToken ct = default)
        {
            if (_messenger is null || !IsClientConnected)
                throw new InvalidOperationException("No client is currently connected.");

            await _messenger.SendAsync(message, ct).ConfigureAwait(false);
        }

        private async Task AcceptClientAsync(CancellationToken ct)
        {
            using var outbound = new NamedPipeServerStream(
                _s2cPipe, PipeDirection.Out, 1,
                PipeTransmissionMode.Byte, System.IO.Pipes.PipeOptions.Asynchronous);

            using var inbound = new NamedPipeServerStream(
                _c2sPipe, PipeDirection.In, 1,
                PipeTransmissionMode.Byte, System.IO.Pipes.PipeOptions.Asynchronous);

            await Task.WhenAll(
                outbound.WaitForConnectionAsync(ct),
                inbound.WaitForConnectionAsync(ct)).ConfigureAwait(false);

            IsClientConnected = true;
            if (OnClientConnected is not null)
                await OnClientConnected().ConfigureAwait(false);

            var disconnectTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            await using var messenger = new PipeMessenger(inbound, outbound, _options.MaxMessageSizeBytes);
            _messenger = messenger;

            messenger.MessageReceived += async msg =>
            {
                if (OnMessageReceived is not null)
                    await OnMessageReceived(msg).ConfigureAwait(false);
            };

            messenger.Disconnected += async () =>
            {
                IsClientConnected = false;
                _messenger = null;
                if (OnClientDisconnected is not null)
                    await OnClientDisconnected().ConfigureAwait(false);
                disconnectTcs.TrySetResult();
            };

            messenger.StartReading();

            // Wait until the client disconnects or cancellation is requested
            await disconnectTcs.Task.WaitAsync(ct).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            await _cts.CancelAsync().ConfigureAwait(false);
            _cts.Dispose();
        }
    }
}
