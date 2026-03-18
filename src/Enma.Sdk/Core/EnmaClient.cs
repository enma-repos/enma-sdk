using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Enma.Sdk.Internal;
using Enma.Sdk.Models;

namespace Enma.Sdk.Core;

public sealed class EnmaClient : IEnmaClient
{
    private readonly EnmaClientOptions _options;
    private readonly Channel<EnmaEvent> _channel;
    private readonly EventMiddlewarePipeline _middleware;
    private readonly BatchProcessor _processor;
    private readonly HttpClient? _ownedHttpClient;
    private bool _disposed;

    public EnmaClient(Action<EnmaClientOptions> configure)
        : this(BuildOptions(configure), httpClient: null)
    {
    }

    public EnmaClient(EnmaClientOptions options, HttpClient? httpClient = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _options.Validate();

        if (httpClient == null)
        {
            _ownedHttpClient = new HttpClient();
            httpClient = _ownedHttpClient;
        }

        _channel = Channel.CreateBounded<EnmaEvent>(new BoundedChannelOptions(_options.MaxQueueSize)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

        _middleware = new EventMiddlewarePipeline(_options.Middlewares);
        var transport = new HttpEventTransport(httpClient, _options);
        _processor = new BatchProcessor(_channel.Reader, transport, _options);
    }

    public void Track(string eventName, Action<EventBuilder>? configure = null)
    {
        if (string.IsNullOrWhiteSpace(eventName))
            throw new ArgumentException("Event name is required.", nameof(eventName));
        if (_disposed) return;

        var @event = BuildEvent(eventName, configure);
        ApplyMiddlewareSync(@event);
        _channel.Writer.TryWrite(@event);
    }

    public async Task TrackAsync(string eventName, Action<EventBuilder>? configure = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(eventName))
            throw new ArgumentException("Event name is required.", nameof(eventName));
        if (_disposed) throw new ObjectDisposedException(nameof(EnmaClient));

        var @event = BuildEvent(eventName, configure);
        @event.Completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        await _middleware.ExecuteAsync(@event, ct).ConfigureAwait(false);

        if (!_channel.Writer.TryWrite(@event))
        {
            @event.Completion.TrySetException(new InvalidOperationException("Event queue is closed."));
        }

        using var registration = ct.CanBeCanceled
            ? ct.Register(() => @event.Completion.TrySetCanceled(ct))
            : default;

        await @event.Completion.Task.ConfigureAwait(false);
    }

    public Task FlushAsync(CancellationToken ct = default)
    {
        return _processor.FlushAsync(ct);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        _channel.Writer.TryComplete();
        await _processor.DisposeAsync().ConfigureAwait(false);
        _ownedHttpClient?.Dispose();
    }

    private EnmaEvent BuildEvent(string eventName, Action<EventBuilder>? configure)
    {
        var builder = new EventBuilder();
        configure?.Invoke(builder);

        var @event = builder.Build(eventName, _options.SdkClientId);
        MergeDefaultTags(@event);
        return @event;
    }

    private void MergeDefaultTags(EnmaEvent @event)
    {
        if (_options.DefaultTags.Count == 0) return;

        @event.Tags ??= new System.Collections.Generic.Dictionary<string, string>();

        foreach (var kvp in _options.DefaultTags)
        {
            if (!@event.Tags.ContainsKey(kvp.Key))
                @event.Tags[kvp.Key] = kvp.Value;
        }
    }

    private void ApplyMiddlewareSync(EnmaEvent @event)
    {
        if (_options.Middlewares.Count == 0) return;
        _middleware.ExecuteAsync(@event, CancellationToken.None).GetAwaiter().GetResult();
    }

    private static EnmaClientOptions BuildOptions(Action<EnmaClientOptions> configure)
    {
        var options = new EnmaClientOptions();
        configure(options);
        return options;
    }
}
