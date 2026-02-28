# 🔗 PipeLine

A lightweight, bidirectional, overlap-safe **Named Pipes messaging library** for .NET.  
Drop it into any app to get full-duplex IPC communication between processes — no sockets, no HTTP, no dependencies.

---

## ✨ Features

- **Full-duplex** — both sides send and receive simultaneously
- **No message overlap** — `SemaphoreSlim` prevents concurrent write interleaving
- **No message merging** — 4-byte length-prefix framing keeps messages intact
- **Event-driven API** — subscribe to `OnMessageReceived`, `OnClientConnected`, `OnDisconnected`
- **Configurable** — custom pipe names, max message size, connect timeout
- **Cross-platform** — Windows, Linux, macOS (.NET 8+)
- **Zero dependencies** — only `System.IO.Pipes` from the BCL

---

## 📦 Installation

```bash
dotnet add package RHFactory.PipeLine
```

---

## 🚀 Quick Start

### Server

```csharp
using PipeLine;

await using var server = new PipeServer();

server.OnClientConnected    += () => { Console.WriteLine("Client connected!"); return Task.CompletedTask; };
server.OnClientDisconnected += () => { Console.WriteLine("Client disconnected."); return Task.CompletedTask; };

server.OnMessageReceived += async msg =>
{
    Console.WriteLine($"Client says: {msg.Content}");
    await server.SendAsync("Hello from server!");
};

await server.StartAsync(); // loops, accepting clients one at a time
```

### Client

```csharp
using PipeLine;

await using var client = new PipeClient();

client.OnMessageReceived += msg =>
{
    Console.WriteLine($"Server says: {msg.Content}");
    return Task.CompletedTask;
};

await client.ConnectAsync();
await client.SendAsync("Hello from client!");

// keep alive
await Task.Delay(Timeout.Infinite);
```

---

## ⚙️ Configuration

Both `PipeServer` and `PipeClient` accept a `PipeOptions` object:

```csharp
var options = new PipeOptions
{
    PipeName           = "myapp",      // base name for the two internal pipes
    MaxMessageSizeBytes = 1_048_576,   // 1 MB — drop connection if exceeded
    ConnectTimeoutMs   = 10_000,       // client connect timeout (ms)
};

await using var server = new PipeServer(options);
await using var client = new PipeClient(options); // same options on both sides
```

> ⚠️ `PipeName` must match on both the server and client.

---

## 🌐 Remote connections (Windows only)

Named pipes support cross-machine communication on Windows. Pass the remote machine name to the client:

```csharp
await using var client = new PipeClient("REMOTE_PC_NAME", options);
await client.ConnectAsync();
```

On Linux/macOS, pipes are local-only (Unix domain sockets). For cross-machine comms on Linux, use TCP sockets — `PipeMessenger` internally works with any `Stream`.

---

## 🏗️ Architecture

Two named pipes are used for full-duplex — one in each direction:

```
Server ──► {pipeName}_s2c ──► Client   (server writes, client reads)
Server ◄── {pipeName}_c2s ◄── Client   (client writes, server reads)
```

### How overlap is prevented

**Layer 1 — Length-prefix framing**

Every message is sent as a 4-byte integer (message byte length) followed by the UTF-8 body. The reader reads *exactly* that many bytes, so messages never get merged or split:

```
┌──────────────┬─────────────────────────┐
│  4 bytes     │  N bytes (UTF-8 body)   │
│  (length)    │                         │
└──────────────┴─────────────────────────┘
```

**Layer 2 — SemaphoreSlim write lock**

`SendAsync` acquires a `SemaphoreSlim(1,1)` for the duration of each write (header + body + flush). Concurrent callers queue up — bytes from two messages can never interleave.

---

## 📁 Repository Structure

```
PipeLine/
├── PipeLine/                  
│   ├── PipeLine.csproj
│   ├── PipeServer.cs
│   ├── PipeClient.cs
│   ├── PipeOptions.cs
│   ├── PipeMessage.cs
│   ├── Program.cs
│   └── Internal/
│
├── PipeLine.ClientExample/
│   └── Program.cs
│
├── PipeLine.ServerExample/
│   └── Program.cs
│
├── PipeLine.sln
└── README.md
```

---

## 🛠️ Building from Source

```bash
git clone https://github.com/Rifat-H7/PipeLine.git
cd PipeLine

# Build everything
dotnet build

# Run examples (two terminals)
dotnet run --project examples/PipeLine.ServerExample
dotnet run --project examples/PipeLine.ClientExample

# Pack the NuGet package
dotnet pack src/PipeLine -c Release -o ./nupkg
```

---

## 📋 API Reference

### `PipeChatServer`

| Member | Description |
|---|---|
| `PipeServer()` | Create with default options |
| `PipeServer(PipeOptions)` | Create with custom options |
| `StartAsync(CancellationToken)` | Start server; accepts clients in a loop |
| `SendAsync(string, CancellationToken)` | Send message to connected client |
| `IsClientConnected` | Whether a client is currently connected |
| `OnMessageReceived` | Fired when a message arrives from the client |
| `OnClientConnected` | Fired when a client connects |
| `OnClientDisconnected` | Fired when the client disconnects |

### `PipeClient`

| Member | Description |
|---|---|
| `PipeClient()` | Create with default options, targeting localhost |
| `PipeClient(PipeOptions)` | Create with custom options |
| `PipeClient(string, PipeOptions)` | Create targeting a remote server (Windows) |
| `ConnectAsync(CancellationToken)` | Connect to the server |
| `SendAsync(string, CancellationToken)` | Send message to server |
| `IsConnected` | Whether currently connected |
| `OnMessageReceived` | Fired when a message arrives from the server |
| `OnDisconnected` | Fired when the server disconnects |

### `PipeOptions`

| Property | Default | Description |
|---|---|---|
| `PipeName` | `"pipechat"` | Base name for internal pipes — must match on both sides |
| `MaxMessageSizeBytes` | `1,048,576` | Max message size in bytes (1 MB) |
| `ConnectTimeoutMs` | `10,000` | Client connect timeout in milliseconds |

---

## 📄 License

MIT — free to use, modify, and distribute.
