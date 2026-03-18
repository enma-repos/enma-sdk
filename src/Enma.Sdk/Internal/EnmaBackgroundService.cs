using System;
using System.Threading;
using System.Threading.Tasks;
using Enma.Sdk.Core;
using Microsoft.Extensions.Hosting;

namespace Enma.Sdk.Internal;

internal sealed class EnmaBackgroundService : IHostedService
{
    private readonly IEnmaClient _client;

    public EnmaBackgroundService(IEnmaClient client)
    {
        _client = client;
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _client.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {

        }

        if (_client is IAsyncDisposable disposable)
        {
            await disposable.DisposeAsync().ConfigureAwait(false);
        }
    }
}
