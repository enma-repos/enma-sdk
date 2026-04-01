using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Enma.Sdk.Core;
using Enma.Sdk.Models;
using Enma.Sdk.Serialization;

namespace Enma.Sdk.Internal;

internal sealed class HttpEventTransport : IEventTransport
{
    private const int MaxServerBatchSize = 200;

    private readonly HttpClient _httpClient;
    private readonly EnmaClientOptions _options;
    private readonly Uri _endpoint;

    public HttpEventTransport(HttpClient httpClient, EnmaClientOptions options)
    {
        _httpClient = httpClient;
        _options = options;
        _endpoint = new Uri(
            options.BaseUrl,
            $"/api/ingest/v1/organizations/{options.OrganizationId}/projects/{options.ProjectId}/events/batch");
    }

    public async Task SendBatchAsync(IReadOnlyList<EnmaEvent> events, CancellationToken ct)
    {
        if (events.Count == 0) return;

        if (events.Count <= MaxServerBatchSize)
        {
            await SendChunkAsync(events, ct).ConfigureAwait(false);
            return;
        }

        for (var offset = 0; offset < events.Count; offset += MaxServerBatchSize)
        {
            var count = Math.Min(MaxServerBatchSize, events.Count - offset);
            var chunk = new List<EnmaEvent>(count);
            for (var i = offset; i < offset + count; i++)
                chunk.Add(events[i]);

            await SendChunkAsync(chunk, ct).ConfigureAwait(false);
        }
    }

    private async Task SendChunkAsync(IReadOnlyList<EnmaEvent> events, CancellationToken ct)
    {
        var body = EnmaJsonContext.SerializeBatch(events, _options);
        Exception? lastException = null;

        for (var attempt = 0; attempt <= _options.MaxRetries; attempt++)
        {
            if (attempt > 0)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt - 1));
                await Task.Delay(delay, ct).ConfigureAwait(false);
            }

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint);
                request.Content = new ByteArrayContent(body);
                request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                request.Headers.TryAddWithoutValidation("X-Api-Key", _options.ApiToken);

                using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                    return;

                if ((int)response.StatusCode < 500)
                {
                    var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    throw new HttpRequestException(
                        $"Enma API returned {(int)response.StatusCode}: {responseBody}");
                }

                lastException = new HttpRequestException(
                    $"Enma API returned {(int)response.StatusCode}");
            }
            catch (HttpRequestException ex) when (attempt < _options.MaxRetries)
            {
                lastException = ex;
            }
        }

        throw lastException ?? new HttpRequestException("Failed to send events after retries.");
    }
}
