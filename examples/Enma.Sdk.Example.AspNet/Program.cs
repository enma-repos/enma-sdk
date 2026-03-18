using Enma.Sdk.Core;
using Enma.Sdk.DependencyInjection;

// -------------------------------------------------------
// Enma SDK — ASP.NET Core Integration Example
// -------------------------------------------------------

var builder = WebApplication.CreateBuilder(args);

// Register Enma SDK via DI.
// In production, use appsettings.json or environment variables.
builder.Services.AddEnma(o =>
{
    o.ApiToken = "sdk_example_token";
    o.OrganizationId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    o.ProjectId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    o.SdkClientId = Guid.Parse("00000000-0000-0000-0000-000000000003");

    o.DefaultTags["service"] = "example-aspnet";
    o.DefaultTags["env"] = "development";

    o.OnError = (events, ex) =>
    {
        Console.Error.WriteLine($"[Enma] Failed to send {events.Count} events: {ex.Message}");
    };
});

// Alternative: bind from configuration
// builder.Services.AddEnma(builder.Configuration.GetSection("Enma"));

var app = builder.Build();

// --- Minimal API endpoints ---

app.MapGet("/", () => "Enma SDK ASP.NET Example. Try POST /orders or GET /products.");

app.MapPost("/orders", (OrderRequest request, IEnmaClient enma) =>
{
    // Track order creation — fire-and-forget
    enma.Track("order.created", e =>
    {
        e.Actor.UserId = request.UserId;
        e.Payload = new
        {
            request.ProductId,
            request.Quantity,
            request.Amount
        };
        e.Tag("region", "eu-west");
        e.ProcessKey(
            Guid.Parse("00000000-0000-0000-0000-000000000004"),
            $"order-{Guid.NewGuid():N}");
    });

    return Results.Ok(new { Status = "created" });
});

app.MapGet("/products/{id}", (string id, IEnmaClient enma) =>
{
    // Track product view
    enma.Track("product.viewed", e =>
    {
        e.Actor.AnonymousId = "anon-" + Guid.NewGuid().ToString("N")[..8];
        e.Payload = new { ProductId = id };
        e.Tag("source", "web");
    });

    return Results.Ok(new { Id = id, Name = $"Product {id}", Price = 29.99 });
});

app.MapPost("/checkout", async (CheckoutRequest request, IEnmaClient enma) =>
{
    // Awaitable tracking — waits until delivered to server
    await enma.TrackAsync("checkout.completed", e =>
    {
        e.Actor.UserId = request.UserId;
        e.Payload = new { request.OrderId, request.Total };
        e.Tag("payment_method", request.PaymentMethod);
    });

    return Results.Ok(new { Status = "completed" });
});

app.Run();

// --- Request models ---

record OrderRequest(string UserId, string ProductId, int Quantity, decimal Amount);
record CheckoutRequest(string UserId, string OrderId, decimal Total, string PaymentMethod);
