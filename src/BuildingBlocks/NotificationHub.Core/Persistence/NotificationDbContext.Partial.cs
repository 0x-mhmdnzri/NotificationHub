using Microsoft.EntityFrameworkCore;

namespace NotificationHub.Core.Persistence;

/// <summary>
/// Partial declaration so Identity DbSets / ConfigureIdentity can extend the context.
/// Main class body remains in NotificationDbContext.cs.
/// </summary>
public partial class NotificationDbContext
{
}
