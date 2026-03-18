using System;
using System.Threading;
using System.Threading.Tasks;

namespace Enma.Sdk.Core;

/// <summary>
/// Client for tracking events to the Enma Ingest API.
/// Events are buffered internally and sent in batches.
/// </summary>
public interface IEnmaClient : IAsyncDisposable
{
    /// <summary>
    /// Enqueues an event for sending. Returns immediately without waiting for delivery.
    /// </summary>
    /// <param name="eventName">Event name (required).</param>
    /// <param name="configure">Optional action to configure the event via <see cref="EventBuilder"/>.</param>
    void Track(string eventName, Action<EventBuilder>? configure = null);

    /// <summary>
    /// Enqueues an event and waits until it is delivered to the server.
    /// </summary>
    /// <param name="eventName">Event name (required).</param>
    /// <param name="configure">Optional action to configure the event via <see cref="EventBuilder"/>.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the event has been sent.</returns>
    Task TrackAsync(string eventName, Action<EventBuilder>? configure = null, CancellationToken ct = default);

    /// <summary>
    /// Forces immediate delivery of all buffered events.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    Task FlushAsync(CancellationToken ct = default);
}
