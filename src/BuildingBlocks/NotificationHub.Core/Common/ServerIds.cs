namespace NotificationHub.Core.Common;

/// <summary>
/// Resource identifiers for create/command operations are owned by the application layer.
/// Clients must not supply them on POST; path IDs remain valid for GET/PUT/PATCH/DELETE.
/// </summary>
public static class ServerIds
{
    public static Guid New() => Guid.NewGuid();

    /// <summary>Always replace with a server-generated id (ignores any client-supplied value).</summary>
    public static Guid ForceNew(Guid _) => Guid.NewGuid();
}
