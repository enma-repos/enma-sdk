using System;
using System.Collections.Generic;
using Enma.Sdk.Models;

namespace Enma.Sdk.Core;

/// <summary>
/// Fluent builder for constructing <see cref="EnmaEvent"/> instances.
/// </summary>
public sealed class EventBuilder
{
    private readonly List<ProcessKey> _processKeys = new();
    private Dictionary<string, string>? _tags;

    /// <summary>
    /// Actor builder for setting user or anonymous identity.
    /// </summary>
    public ActorBuilder Actor { get; } = new();

    /// <summary>
    /// Arbitrary payload object. Will be serialized to JSON.
    /// </summary>
    public object? Payload { get; set; }

    /// <summary>
    /// Links the event to a process instance.
    /// </summary>
    /// <param name="processDefinitionId">Process definition identifier.</param>
    /// <param name="processId">Process instance identifier.</param>
    /// <returns>This builder for chaining.</returns>
    public EventBuilder ProcessKey(Guid processDefinitionId, string processId)
    {
        _processKeys.Add(new Models.ProcessKey(processDefinitionId, processId));
        return this;
    }

    /// <summary>
    /// Links the event to a process instance, parsing the definition id from a string.
    /// </summary>
    /// <param name="processDefinitionId">Process definition identifier as a GUID string.</param>
    /// <param name="processId">Process instance identifier.</param>
    /// <returns>This builder for chaining.</returns>
    /// <exception cref="FormatException"><paramref name="processDefinitionId"/> is not a valid GUID.</exception>
    public EventBuilder ProcessKey(string processDefinitionId, string processId)
    {
        return ProcessKey(Guid.Parse(processDefinitionId), processId);
    }

    /// <summary>
    /// Adds a tag to the event.
    /// </summary>
    /// <param name="key">Tag key.</param>
    /// <param name="value">Tag value.</param>
    /// <returns>This builder for chaining.</returns>
    public EventBuilder Tag(string key, string value)
    {
        _tags ??= new Dictionary<string, string>();
        _tags[key] = value;
        return this;
    }

    /// <summary>
    /// Adds multiple tags to the event.
    /// </summary>
    /// <param name="tags">Dictionary of tags to add.</param>
    /// <returns>This builder for chaining.</returns>
    public EventBuilder Tags(Dictionary<string, string> tags)
    {
        if (tags == null) return this;
        _tags ??= new Dictionary<string, string>();
        foreach (var kvp in tags)
            _tags[kvp.Key] = kvp.Value;
        return this;
    }

    internal EnmaEvent Build(string eventName, Guid sdkClientId)
    {
        return new EnmaEvent
        {
            EventId = Guid.NewGuid(),
            SdkClientId = sdkClientId,
            EventName = eventName,
            Payload = Payload,
            Tags = _tags,
            ProcessKeys = _processKeys,
            Actor = new Models.Actor
            {
                UserId = Actor.UserId,
                AnonymousId = Actor.AnonymousId
            },
            OccurredAt = DateTime.UtcNow
        };
    }
}

/// <summary>
/// Builder for setting actor identity on an event.
/// </summary>
public sealed class ActorBuilder
{
    /// <summary>
    /// Authenticated user identifier.
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// Anonymous (session/device) identifier.
    /// </summary>
    public string? AnonymousId { get; set; }
}
