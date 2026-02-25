using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PipeLine.Internal
{
    /// <summary>
    /// Internal engine that handles length-prefixed message framing and
    /// overlap-safe writes over a pair of <see cref="PipeStream"/> instances.
    /// </summary>
    internal sealed class PipeMessenger : IAsyncDisposable
    {
        private readonly PipeStream _readPipe;
        private readonly PipeStream _writePipe;
        private readonly SemaphoreSlim _writeLock = new(1, 1);
        private readonly CancellationTokenSource _cts = new();
        private readonly int _maxMessageSize;

        internal event Func<PipeMessage, Task>? MessageReceived;
        internal event Func<Task>? Disconnected;

        internal PipeMessenger(PipeStream readPipe, PipeStream writePipe, int maxMessageSize)
        {
            _readPipe = readPipe;
            _writePipe = writePipe;
            _maxMessageSize = maxMessageSize;
        }

        /// <summary>Starts the background read loop.</summary>
        internal void StartReading()
        {
            _ = Task.Run(() => ReadLoopAsync(_cts.Token));
        }

        /// <summary>
        /// Sends a message safely. A <see cref="SemaphoreSlim"/> ensures that
        /// concurrent calls never interleave bytes on the wire.
        /// </summary>
        internal async Task SendAsync(string message, CancellationToken ct = default)
        {
            var bytes = Encoding.UTF8.GetBytes(message);
            var lengthPrefix = BitConverter.GetBytes(bytes.Length);

            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _cts.Token);

            await _writeLock.WaitAsync(linked.Token).ConfigureAwait(false);
            try
            {
                await _writePipe.WriteAsync(lengthPrefix, linked.Token).ConfigureAwait(false);
                await _writePipe.WriteAsync(bytes, linked.Token).ConfigureAwait(false);
                await _writePipe.FlushAsync(linked.Token).ConfigureAwait(false);
            }
            finally
            {
                _writeLock.Release();
            }
        }

        private async Task ReadLoopAsync(CancellationToken ct)
        {
            var lengthBuf = new byte[4];
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    int read = await ReadExactAsync(_readPipe, lengthBuf, 4, ct).ConfigureAwait(false);
                    if (read == 0) break;

                    int msgLength = BitConverter.ToInt32(lengthBuf, 0);
                    if (msgLength <= 0 || msgLength > _maxMessageSize)
                        break;

                    var msgBuf = new byte[msgLength];
                    read = await ReadExactAsync(_readPipe, msgBuf, msgLength, ct).ConfigureAwait(false);
                    if (read == 0) break;

                    var msg = new PipeMessage(Encoding.UTF8.GetString(msgBuf));
                    if (MessageReceived is not null)
                        await MessageReceived(msg).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) { }
            catch (IOException) { }
            finally
            {
                if (Disconnected is not null)
                    await Disconnected().ConfigureAwait(false);
            }
        }

        private static async Task<int> ReadExactAsync(
            PipeStream pipe, byte[] buffer, int count, CancellationToken ct)
        {
            int total = 0;
            while (total < count)
            {
                int read = await pipe.ReadAsync(buffer.AsMemory(total, count - total), ct)
                                     .ConfigureAwait(false);
                if (read == 0) return 0;
                total += read;
            }
            return total;
        }

        public async ValueTask DisposeAsync()
        {
            await _cts.CancelAsync().ConfigureAwait(false);
            _writeLock.Dispose();
            _cts.Dispose();
        }
    }
}
