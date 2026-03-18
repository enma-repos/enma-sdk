using Enma.Sdk.Core;

// -------------------------------------------------------
// Enma SDK — Standalone Usage Example
// -------------------------------------------------------
// Replace the GUIDs below with your actual values from
// the Enma admin panel.
// -------------------------------------------------------

var organizationId = Guid.Parse("00000000-0000-0000-0000-000000000001");
var projectId = Guid.Parse("00000000-0000-0000-0000-000000000002");
var sdkClientId = Guid.Parse("00000000-0000-0000-0000-000000000003");
var processDefinitionId = Guid.Parse("00000000-0000-0000-0000-000000000004");

await using var enma = new EnmaClient(o =>
{
    o.ApiToken = "sdk_example_token";
    o.OrganizationId = organizationId;
    o.ProjectId = projectId;
    o.SdkClientId = sdkClientId;

    // Global tags — added to every event automatically
    o.DefaultTags["service"] = "example-standalone";
    o.DefaultTags["env"] = "development";

    // Error callback
    o.OnError = (events, ex) =>
    {
        Console.Error.WriteLine($"[Enma] Failed to send {events.Count} events: {ex.Message}");
    };

    // Middleware — enrich every event with machine name
    o.Use(next => async (evt, ct) =>
    {
        evt.Tags ??= new Dictionary<string, string>();
        evt.Tags["host"] = Environment.MachineName;
        await next(evt, ct);
    });
});

// 1. Simple fire-and-forget event
enma.Track("app.started");
Console.WriteLine("Tracked: app.started");

// 2. Event with actor and payload
enma.Track("order.created", e =>
{
    e.Actor.UserId = "user-42";
    e.Payload = new { Amount = 99.99, Currency = "USD", Items = 3 };
    e.Tag("region", "eu-west");
});
Console.WriteLine("Tracked: order.created");

// 3. Event with process keys
enma.Track("step.completed", e =>
{
    e.Actor.UserId = "user-42";
    e.ProcessKey(processDefinitionId, "order-123");
    e.Payload = new { Step = "payment", Status = "success" };
});
Console.WriteLine("Tracked: step.completed");

// 4. Awaitable tracking — waits until delivered to server
try
{
    await enma.TrackAsync("payment.processed", e =>
    {
        e.Actor.UserId = "user-42";
        e.Payload = new { TransactionId = "tx-001", Amount = 150.00 };
        e.Tag("provider", "stripe");
    });
    Console.WriteLine("Tracked and delivered: payment.processed");
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Failed to deliver: {ex.Message}");
}

// 5. Multiple events in a loop
for (var i = 0; i < 10; i++)
{
    enma.Track("item.viewed", e =>
    {
        e.Actor.AnonymousId = "anon-session-abc";
        e.Payload = new { ItemId = $"item-{i}", Category = "electronics" };
    });
}
Console.WriteLine("Tracked: 10x item.viewed");

// 6. Flush — send all buffered events now
await enma.FlushAsync();
Console.WriteLine("All events flushed.");

// DisposeAsync is called automatically by 'await using'
Console.WriteLine("Done.");
