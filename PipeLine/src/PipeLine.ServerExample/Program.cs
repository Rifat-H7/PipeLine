

// ══════════════════════════════════════════════════
//  PipeChat Library — Example Server
//  Shows how to use PipeServer in your own app.
// ══════════════════════════════════════════════════

using PipeLine;

Console.Title = "PipeLine Example – Server";
Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("╔══════════════════════════════════════╗");
Console.WriteLine("║     PipeLine Example Server          ║");
Console.WriteLine("╚══════════════════════════════════════╝");
Console.ResetColor();

// ── 1. Configure (optional — defaults work out of the box) ──────────────────
var options = new PipeOptions
{
    PipeName = "mypipes",       // must match the client
    MaxMessageSizeBytes = 1_048_576,  // 1 MB
};

// ── 2. Create server ─────────────────────────────────────────────────────────
await using var server = new PipeServer(options);

// ── 3. Subscribe to events ───────────────────────────────────────────────────
server.OnClientConnected += () =>
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("\n[Server] Client connected!\n");
    Console.ResetColor();
    Console.Write("[You]   : ");
    return Task.CompletedTask;
};

server.OnClientDisconnected += () =>
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("\n[Server] Client disconnected. Waiting for next client...\n");
    Console.ResetColor();
    return Task.CompletedTask;
};

server.OnMessageReceived += async msg =>
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine($"\n[CLIENT] ({msg.ReceivedAt:HH:mm:ss}): {msg.Content}");
    Console.ResetColor();
    Console.Write("[You]   : ");

    // Example: echo the message back with a prefix
    // await server.SendAsync($"Echo: {msg.Content}");
};

// ── 4. Start server in background (loops accepting clients) ──────────────────
using var cts = new CancellationTokenSource();
var serverTask = server.StartAsync(cts.Token);

Console.WriteLine("[Server] Waiting for client to connect...");
Console.WriteLine("[Server] Press Ctrl+C to shut down.\n");

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

// ── 5. Console input loop ────────────────────────────────────────────────────
while (!cts.Token.IsCancellationRequested)
{
    if (!Console.KeyAvailable)
    {
        await Task.Delay(50);
        continue;
    }

    string? input = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(input)) { Console.Write("[You]   : "); continue; }

    if (!server.IsClientConnected)
    {
        Console.WriteLine("[Server] No client connected yet.");
        continue;
    }

    try
    {
        await server.SendAsync(input);
        Console.Write("[You]   : ");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Server] Send failed: {ex.Message}");
    }
}

await serverTask;
Console.WriteLine("[Server] Shut down.");