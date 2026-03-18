using System;

namespace Enma.Sdk.Models;

/// <summary>
/// Links an event to a specific process instance within a process definition.
/// </summary>
public sealed class ProcessKey
{
    /// <summary>
    /// Identifier of the process definition (template).
    /// </summary>
    public Guid ProcessDefinitionId { get; }

    /// <summary>
    /// Identifier of the concrete process instance (e.g. order id, session id).
    /// </summary>
    public string ProcessId { get; }

    /// <summary>
    /// Creates a new <see cref="ProcessKey"/>.
    /// </summary>
    /// <param name="processDefinitionId">Process definition identifier.</param>
    /// <param name="processId">Process instance identifier.</param>
    /// <exception cref="ArgumentNullException"><paramref name="processId"/> is <c>null</c>.</exception>
    public ProcessKey(Guid processDefinitionId, string processId)
    {
        ProcessDefinitionId = processDefinitionId;
        ProcessId = processId ?? throw new ArgumentNullException(nameof(processId));
    }
}
