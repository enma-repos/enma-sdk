namespace Enma.Sdk.Models;

/// <summary>
/// Identifies the actor who triggered an event.
/// At least one of <see cref="UserId"/> or <see cref="AnonymousId"/> should be set.
/// </summary>
public sealed class Actor
{
    /// <summary>
    /// Authenticated user identifier. <c>null</c> if the actor is anonymous.
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// Anonymous (session/device) identifier. <c>null</c> if the actor is authenticated.
    /// </summary>
    public string? AnonymousId { get; set; }
}
