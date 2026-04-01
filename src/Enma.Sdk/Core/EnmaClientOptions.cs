using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Enma.Sdk.Models;

namespace Enma.Sdk.Core;

/// <summary>
/// Middleware delegate that processes an event and passes it to the next handler.
/// </summary>
/// <param name="event">The event being processed.</param>
/// <param name="ct">Cancellation token.</param>
public delegate Task EventMiddleware(EnmaEvent @event, CancellationToken ct);

/// <summary>
/// Configuration options for <see cref="IEnmaClient"/>.
/// </summary>
public sealed class EnmaClientOptions
{
    /// <summary>
    /// API token for authentication. Must start with <c>"sdk_"</c>.
    /// </summary>
    public string ApiToken { get; set; } = string.Empty;

    /// <summary>
    /// Organization identifier.
    /// </summary>
    public Guid OrganizationId { get; set; }

    /// <summary>
    /// Project identifier.
    /// </summary>
    public Guid ProjectId { get; set; }

    /// <summary>
    /// SDK client application identifier, created in the Enma admin panel.
    /// </summary>
    public Guid SdkClientId { get; set; }

    /// <summary>
    /// Base URL of the Enma Ingest API.
    /// </summary>
    public Uri BaseUrl { get; set; } = new("https://sdk.enma.tech");

    /// <summary>
    /// Maximum number of events per HTTP request. Server limit is 200.
    /// </summary>
    public int BatchSize { get; set; } = 50;

    /// <summary>
    /// Interval for automatic flush of buffered events.
    /// </summary>
    public TimeSpan FlushInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Maximum number of events in the internal queue. Oldest events are dropped when full.
    /// </summary>
    public int MaxQueueSize { get; set; } = 10_000;

    /// <summary>
    /// Maximum number of retry attempts for failed HTTP requests (5xx and network errors).
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Tags that are automatically added to every tracked event. Event-level tags take precedence.
    /// </summary>
    public Dictionary<string, string> DefaultTags { get; } = new();

    /// <summary>
    /// Callback invoked when a batch of events fails to send after all retries.
    /// </summary>
    public Action<IReadOnlyList<EnmaEvent>, Exception>? OnError { get; set; }

    private readonly List<Func<EventMiddleware, EventMiddleware>> _middlewares = new();

    /// <summary>
    /// Adds a middleware to the event processing pipeline.
    /// Middleware wraps the next handler and can modify events before they are queued.
    /// </summary>
    /// <param name="middleware">A function that takes the next middleware and returns a wrapping middleware.</param>
    public void Use(Func<EventMiddleware, EventMiddleware> middleware)
    {
        _middlewares.Add(middleware ?? throw new ArgumentNullException(nameof(middleware)));
    }

    internal IReadOnlyList<Func<EventMiddleware, EventMiddleware>> Middlewares => _middlewares;

    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(ApiToken))
            throw new ArgumentException("ApiToken is required.", nameof(ApiToken));
        if (OrganizationId == Guid.Empty)
            throw new ArgumentException("OrganizationId is required.", nameof(OrganizationId));
        if (ProjectId == Guid.Empty)
            throw new ArgumentException("ProjectId is required.", nameof(ProjectId));
        if (SdkClientId == Guid.Empty)
            throw new ArgumentException("SdkClientId is required.", nameof(SdkClientId));
    }
}
