namespace NotificationHub.Application.Abstractions;

/// <summary>
/// Marker for commands that require an explicit DB transaction around the handler.
/// Only use when multiple persistence operations must commit atomically.
/// </summary>
public interface ITransactional;
