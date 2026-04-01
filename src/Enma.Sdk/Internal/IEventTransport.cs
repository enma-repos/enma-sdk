using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Enma.Sdk.Models;

namespace Enma.Sdk.Internal;

internal interface IEventTransport
{
    Task SendBatchAsync(IReadOnlyList<EnmaEvent> events, CancellationToken ct);
}
