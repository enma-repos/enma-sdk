using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Enma.Sdk.Core;
using Enma.Sdk.Models;

namespace Enma.Sdk.Internal;

internal sealed class BatchProcessor : IAsyncDisposable
{
    private readonly ChannelReader<EnmaEvent> _reader;
    private readonly IEventTransport _transport;
    private readonly EnmaClientOptions _options;
    private readonly Task _backgroundTask;
    private readonly CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim _flushSignal = new(0);
    private TaskCompletionSource<bool>? _flushCompletion;
    private readonly object _flushLock = new();
    private bool _disposed;

    public BatchProcessor(
        ChannelReader<EnmaEvent> reader,
        IEventTransport transport,
        EnmaClientOptions options)
    {
        _reader = reader;
        _transport = transport;
        _options = options;
        _backgroundTask = Task.Run(() => ProcessLoopAsync(_cts.Token));
    }

    public Task FlushAsync(CancellationToken ct = default)
    {
        lock (_flushLock)
        {
            _flushCompletion ??= new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var completion = _flushCompletion;

            _flushSignal.Release();

            if (ct.CanBeCanceled)
            {
                var registration = ct.Register(() => completion.TrySetCanceled(ct));
                completion.Task.ContinueWith(_ => registration.Dispose(), TaskScheduler.Default);
            }

            return completion.Task;
        }
    }

    private async Task ProcessLoopAsync(CancellationToken ct)
    {
        var buffer = new List<EnmaEvent>();

        try
        {
            while (!ct.IsCancellationRequested)
            {
                var timedOut = false;

                try
                {
                    using var delayCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    var readTask = _reader.WaitToReadAsync(ct).AsTask();
                    var flushTask = _flushSignal.WaitAsync(ct);
                    var delayTask = Task.Delay(_options.FlushInterval, delayCts.Token);

                    var completed = await Task.WhenAny(readTask, flushTask, delayTask).ConfigureAwait(false);
                    delayCts.Cancel();

                    if (completed == readTask)
                    {
                        if (!await readTask.ConfigureAwait(false)) break;
                    }
                    else if (completed == delayTask)
                    {
                        timedOut = true;
                    }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    break;
                }

                while (_reader.TryRead(out var item))
                {
                    buffer.Add(item);
                    if (buffer.Count >= _options.BatchSize)
                    {
                        await SendBufferAsync(buffer).ConfigureAwait(false);
                    }
                }

                if (buffer.Count > 0 && (timedOut || buffer.Count >= _options.BatchSize || _flushCompletion != null))
                {
                    await SendBufferAsync(buffer).ConfigureAwait(false);
                }

                CompleteFlushIfRequested();
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // expected during shutdown
        }

        // Drain remaining events on shutdown
        while (_reader.TryRead(out var remaining))
        {
            buffer.Add(remaining);
        }

        if (buffer.Count > 0)
        {
            await SendBufferAsync(buffer).ConfigureAwait(false);
        }
        
        CompleteFlushIfRequested();
    }

    private async Task SendBufferAsync(List<EnmaEvent> buffer)
    {
        var batch = buffer.ToArray();
        buffer.Clear();

        try
        {
            await _transport.SendBatchAsync(batch, CancellationToken.None).ConfigureAwait(false);

            foreach (var e in batch)
            {
                e.Completion?.TrySetResult(true);
            }
        }
        catch (Exception ex)
        {
            _options.OnError?.Invoke(batch, ex);

            foreach (var e in batch)
            {
                e.Completion?.TrySetException(ex);
            }
        }
    }

    private void CompleteFlushIfRequested()
    {
        lock (_flushLock)
        {
            _flushCompletion?.TrySetResult(true);
            _flushCompletion = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        _cts.Cancel();

        var completed = await Task.WhenAny(_backgroundTask, Task.Delay(TimeSpan.FromSeconds(30)))
            .ConfigureAwait(false);

        if (completed == _backgroundTask)
        {
            try { await _backgroundTask.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }

        _cts.Dispose();
        _flushSignal.Dispose();
    }
}
