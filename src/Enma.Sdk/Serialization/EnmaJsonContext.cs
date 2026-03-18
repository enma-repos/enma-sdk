using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Enma.Sdk.Core;
using Enma.Sdk.Models;

namespace Enma.Sdk.Serialization;

internal static class EnmaJsonContext
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static byte[] SerializeBatch(IReadOnlyList<EnmaEvent> events, EnmaClientOptions options)
    {
        var dtos = new List<IngestEventDto>(events.Count);
        foreach (var e in events)
        {
            dtos.Add(MapToDto(e));
        }

        return JsonSerializer.SerializeToUtf8Bytes(dtos, Options);
    }

    private static IngestEventDto MapToDto(EnmaEvent e)
    {
        JsonElement? payload = null;
        if (e.Payload != null)
        {
            payload = JsonSerializer.SerializeToElement(e.Payload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
        }

        return new IngestEventDto
        {
            EventId = e.EventId,
            SdkClientId = e.SdkClientId,
            EventName = e.EventName,
            Payload = payload,
            Tags = e.Tags,
            ProcessKeys = e.ProcessKeys.Select(pk => new ProcessKeyDto
            {
                ProcessDefinitionId = pk.ProcessDefinitionId,
                ProcessId = pk.ProcessId
            }).ToList(),
            Actor = new ActorDto
            {
                UserId = e.Actor.UserId,
                AnonymousId = e.Actor.AnonymousId
            },
            OccurredAt = e.OccurredAt
        };
    }

    internal sealed class IngestEventDto
    {
        public Guid EventId { get; set; }
        public Guid SdkClientId { get; set; }
        public string EventName { get; set; } = string.Empty;
        public JsonElement? Payload { get; set; }
        public Dictionary<string, string>? Tags { get; set; }
        public List<ProcessKeyDto> ProcessKeys { get; set; } = new();
        public ActorDto Actor { get; set; } = new();
        public DateTime OccurredAt { get; set; }
    }

    internal sealed class ProcessKeyDto
    {
        public Guid ProcessDefinitionId { get; set; }
        public string ProcessId { get; set; } = string.Empty;
    }

    internal sealed class ActorDto
    {
        public string? UserId { get; set; }
        public string? AnonymousId { get; set; }
    }
}
