using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Enma.Sdk.Core;
using Enma.Sdk.Models;

namespace Enma.Sdk.Internal;

internal sealed class EventMiddlewarePipeline
{
    private readonly EventMiddleware _pipeline;

    public EventMiddlewarePipeline(IReadOnlyList<Func<EventMiddleware, EventMiddleware>> middlewares)
    {
        EventMiddleware terminal = (_, _) => Task.CompletedTask;

        var handler = terminal;
        for (var i = middlewares.Count - 1; i >= 0; i--)
        {
            handler = middlewares[i](handler);
        }

        _pipeline = handler;
    }

    public Task ExecuteAsync(EnmaEvent @event, CancellationToken ct)
    {
        return _pipeline(@event, ct);
    }
}
