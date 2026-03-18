# Enma.Sdk

Official .NET SDK for the [Enma](https://enma.io) Ingest API. Provides buffered, batched event tracking with automatic retries, a middleware pipeline, and first-class ASP.NET Core integration.

## Installation

```bash
dotnet add package Enma.Sdk
```

**Targets:** .NET Standard 2.1 (.NET Core 3.0+, .NET 5+)

## Quick Start

```csharp
using Enma.Sdk.Core;

await using var enma = new EnmaClient(o =>
{
    o.ApiToken = "sdk_abc123";
    o.OrganizationId = Guid.Parse("...");
    o.ProjectId = Guid.Parse("...");
    o.SdkClientId = Guid.Parse("...");
});

enma.Track("order.created", e =>
{
    e.Actor.UserId = "user-42";
    e.Payload = new { Amount = 99.99, Currency = "USD" };
    e.Tag("region", "eu-west");
});

await enma.FlushAsync();
```

## How It Works

```
Track() ──► Channel<T> queue ──► Background loop ──► HTTP POST (batch)
              (lock-free)         (flush by timer       (retry on 5xx)
                                   or batch size)
```

1. `Track()` / `TrackAsync()` writes an event into a bounded in-memory queue (`System.Threading.Channels`).
2. A background loop reads from the queue and groups events into batches (up to `BatchSize`, default 50).
3. Batches are flushed automatically every `FlushInterval` (default 5s) or when the batch is full.
4. Each HTTP request is sent to the Enma Ingest API with retry on 5xx/network errors (exponential backoff: 1s, 2s, 4s).
5. On dispose or explicit `FlushAsync()`, all remaining events are drained and sent.

## Configuration

```csharp
var enma = new EnmaClient(o =>
{
    // Required
    o.ApiToken = "sdk_...";
    o.OrganizationId = Guid.Parse("...");
    o.ProjectId = Guid.Parse("...");
    o.SdkClientId = Guid.Parse("...");

    // Optional
    o.BaseUrl = new Uri("https://enma.tech");     // default
    o.BatchSize = 50;                             // events per HTTP request (max 200)
    o.FlushInterval = TimeSpan.FromSeconds(5);    // auto-flush interval
    o.MaxQueueSize = 10_000;                      // bounded queue capacity
    o.MaxRetries = 3;                             // retry attempts for 5xx

    // Global tags added to every event
    o.DefaultTags["service"] = "order-api";
    o.DefaultTags["env"] = "production";

    // Error callback
    o.OnError = (events, ex) =>
    {
        Console.Error.WriteLine($"Failed to send {events.Count} events: {ex.Message}");
    };
});
```

## Tracking Events

### Fire-and-forget

`Track()` enqueues the event and returns immediately. The event is sent in the background.

```csharp
enma.Track("page.viewed");

enma.Track("order.created", e =>
{
    e.Actor.UserId = "user-42";
    e.Payload = new { Amount = 99.99, Currency = "USD" };
});
```

### Awaitable

`TrackAsync()` waits until the event is actually delivered to the server.

```csharp
await enma.TrackAsync("payment.processed", e =>
{
    e.Actor.UserId = "user-42";
    e.Payload = new { TransactionId = "tx-001", Amount = 150.00 };
    e.Tag("provider", "stripe");
});
```

### Process Keys

Link events to process instances for tracking workflows:

```csharp
enma.Track("step.completed", e =>
{
    e.Actor.UserId = "user-42";
    e.ProcessKey(Guid.Parse("...process-def-id..."), "order-123");
    e.ProcessKey("another-def-id-guid", "session-456");  // string overload
});
```

### Tags

```csharp
enma.Track("item.added", e =>
{
    e.Tag("category", "electronics");
    e.Tags(new Dictionary<string, string>
    {
        ["source"] = "mobile",
        ["ab_test"] = "variant-b"
    });
});
```

> Event-level tags take precedence over `DefaultTags` when keys collide.

## ASP.NET Core Integration

### With inline configuration

```csharp
builder.Services.AddEnma(o =>
{
    o.ApiToken = builder.Configuration["Enma:ApiToken"]!;
    o.OrganizationId = Guid.Parse(builder.Configuration["Enma:OrgId"]!);
    o.ProjectId = Guid.Parse(builder.Configuration["Enma:ProjectId"]!);
    o.SdkClientId = Guid.Parse(builder.Configuration["Enma:SdkClientId"]!);
});
```

### With `IConfiguration` binding

```json
{
  "Enma": {
    "ApiToken": "sdk_...",
    "OrganizationId": "...",
    "ProjectId": "...",
    "SdkClientId": "...",
    "BatchSize": 100,
    "MaxRetries": 5
  }
}
```

```csharp
builder.Services.AddEnma(builder.Configuration.GetSection("Enma"));
```

### Inject and use

```csharp
public class OrdersController : ControllerBase
{
    private readonly IEnmaClient _enma;

    public OrdersController(IEnmaClient enma) => _enma = enma;

    [HttpPost]
    public IActionResult CreateOrder(CreateOrderRequest request)
    {
        _enma.Track("order.created", e =>
        {
            e.Actor.UserId = User.FindFirst("sub")?.Value;
            e.Payload = new { request.ProductId, request.Quantity };
        });

        return Ok();
    }
}
```

`AddEnma()` registers:
- `IEnmaClient` as a **singleton**
- `IHostedService` for **graceful shutdown** (flushes remaining events when the host stops)

## Middleware

Enrich or filter events before they enter the queue:

```csharp
var enma = new EnmaClient(o =>
{
    // Add machine name to every event
    o.Use(next => async (evt, ct) =>
    {
        evt.Tags ??= new Dictionary<string, string>();
        evt.Tags["host"] = Environment.MachineName;
        await next(evt, ct);
    });

    // Filter out internal events
    o.Use(next => async (evt, ct) =>
    {
        if (!evt.EventName.StartsWith("internal."))
            await next(evt, ct);
    });
});
```

Middleware executes in registration order. Call `next` to pass the event down the pipeline, or skip it to drop the event.

## Examples

Runnable example projects are in the [`examples/`](examples/) directory:

| Project | Description |
|---------|-------------|
| [`Enma.Sdk.Example.Standalone`](examples/Enma.Sdk.Example.Standalone/) | Console app — standalone usage without DI. Demonstrates Track, TrackAsync, process keys, tags, middleware, and flush. |
| [`Enma.Sdk.Example.AspNet`](examples/Enma.Sdk.Example.AspNet/) | ASP.NET Core Minimal API — DI integration with `AddEnma()`, inject `IEnmaClient` into endpoints, appsettings.json config. |

```bash
# Run standalone example
dotnet run --project examples/Enma.Sdk.Example.Standalone

# Run ASP.NET example
dotnet run --project examples/Enma.Sdk.Example.AspNet
```

## API Reference

### `IEnmaClient`

| Method | Description |
|--------|-------------|
| `void Track(string eventName, Action<EventBuilder>? configure)` | Fire-and-forget. Enqueues event, returns immediately. |
| `Task TrackAsync(string eventName, Action<EventBuilder>? configure, CancellationToken ct)` | Awaitable. Completes when the event is delivered. |
| `Task FlushAsync(CancellationToken ct)` | Forces immediate delivery of all buffered events. |
| `ValueTask DisposeAsync()` | Flushes remaining events and releases resources. |

### `EventBuilder`

| Member | Description |
|--------|-------------|
| `ActorBuilder Actor` | Set `.UserId` or `.AnonymousId` |
| `object? Payload` | Arbitrary object, serialized to JSON |
| `EventBuilder ProcessKey(Guid defId, string processId)` | Link to a process instance |
| `EventBuilder Tag(string key, string value)` | Add a single tag |
| `EventBuilder Tags(Dictionary<string, string> tags)` | Add multiple tags |

### `EnmaClientOptions`

| Property | Default | Description |
|----------|---------|-------------|
| `ApiToken` | *required* | API token (`sdk_...`) |
| `OrganizationId` | *required* | Organization GUID |
| `ProjectId` | *required* | Project GUID |
| `SdkClientId` | *required* | SDK client application GUID |
| `BaseUrl` | `https://api.enma.io` | Ingest API base URL |
| `BatchSize` | `50` | Max events per HTTP request |
| `FlushInterval` | `5s` | Auto-flush interval |
| `MaxQueueSize` | `10,000` | Bounded queue capacity |
| `MaxRetries` | `3` | Retry attempts (5xx / network) |
| `DefaultTags` | `{}` | Tags added to all events |
| `OnError` | `null` | Callback on send failure |

## Project Structure

```
enma-sdk/
├── README.md
├── src/
│   ├── Enma.Sdk.sln
│   └── Enma.Sdk/
│       ├── Core/               # IEnmaClient, EnmaClient, EnmaClientOptions, EventBuilder
│       ├── Models/             # EnmaEvent, Actor, ProcessKey
│       ├── Internal/           # BatchProcessor, HttpEventTransport, EventMiddlewarePipeline
│       ├── Serialization/      # JSON wire DTOs and serialization options
│       └── DependencyInjection/# AddEnma() extension methods
└── examples/
    ├── Enma.Sdk.Example.Standalone/
    └── Enma.Sdk.Example.AspNet/
```
