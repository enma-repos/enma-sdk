using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Enma.Sdk.Models;

/// <summary>
/// Represents a single analytics event to be sent to the Enma Ingest API.
/// </summary>
public sealed class EnmaEvent
{
    /// <summary>
    /// Unique event identifier. Auto-generated if not specified.
    /// </summary>
    public Guid EventId { get; set; }

    /// <summary>
    /// Identifier of the SDK client application that produced this event.
    /// </summary>
    public Guid SdkClientId { get; set; }

    /// <summary>
    /// Event name (required). Used for routing and filtering.
    /// </summary>
    public string EventName { get; set; } = string.Empty;

    /// <summary>
    /// Arbitrary payload object. Will be serialized to JSON.
    /// </summary>
    public object? Payload { get; set; }

    /// <summary>
    /// Key-value metadata tags attached to this event.
    /// </summary>
    public Dictionary<string, string>? Tags { get; set; }

    /// <summary>
    /// Process keys linking this event to process instances.
    /// </summary>
    public List<ProcessKey> ProcessKeys { get; set; } = new();

    /// <summary>
    /// Actor who triggered this event.
    /// </summary>
    public Actor Actor { get; set; } = new();

    /// <summary>
    /// Timestamp when the event occurred. Defaults to <see cref="DateTime.UtcNow"/>.
    /// </summary>
    public DateTime OccurredAt { get; set; }

    internal TaskCompletionSource<bool>? Completion { get; set; }
}
