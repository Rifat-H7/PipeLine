using PipeLine;

// ══════════════════════════════════════════════════
//  PipeLine Library — Example Client
//  Shows how to use PipeClient in your own app.
// ══════════════════════════════════════════════════

Console.Title = "PipeLine Example – Client";
Console.ForegroundColor = ConsoleColor.Magenta;
Console.WriteLine("╔══════════════════════════════════════╗");
Console.WriteLine("║     PipeLine Example Client          ║");
Console.WriteLine("╚══════════════════════════════════════╝");
Console.ResetColor();

// ── 1. Configure (must match server options) ─────────────────────────────────
var options = new PipeOptions
{
    PipeName = "mypipes",         // must match the server
    ConnectTimeoutMs = 10_000,    // wait up to 10s for the server
};

// ── 2. Create client ─────────────────────────────────────────────────────────
//   For a remote server on Windows, pass the machine name as first argument:
//   new PipeClient("REMOTE_PC", options)
await using var client = new PipeClient(options);

// ── 3. Subscribe to events ───────────────────────────────────────────────────
client.OnMessageReceived += msg =>
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine($"\n[SERVER] ({msg.ReceivedAt:HH:mm:ss}): {msg.Content}");
    Console.ResetColor();
    Console.Write("[You]   : ");
    return Task.CompletedTask;
};

client.OnDisconnected += () =>
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("\n[Client] Server disconnected.");
    Console.ResetColor();
    return Task.CompletedTask;
};

// ── 4. Connect ───────────────────────────────────────────────────────────────
Console.WriteLine("[Client] Connecting to server...");
try
{
    await client.ConnectAsync();
}
catch (TimeoutException)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("[Client] Connection timed out. Is the server running?");
    Console.ResetColor();
    return;
}

Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine("[Client] Connected! Type messages below. Press Ctrl+C to exit.\n");
Console.ResetColor();
Console.Write("[You]   : ");

// ── 5. Console input loop ────────────────────────────────────────────────────
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

while (!cts.Token.IsCancellationRequested && client.IsConnected)
{
    if (!Console.KeyAvailable)
    {
        await Task.Delay(50);
        continue;
    }

    string? input = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(input)) { Console.Write("[You]   : "); continue; }

    try
    {
        await client.SendAsync(input);
        Console.Write("[You]   : ");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Client] Send failed: {ex.Message}");
        break;
    }
}

Console.WriteLine("[Client] Session ended.");