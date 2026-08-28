using System.Reflection;
using Xunit;

namespace NotificationHub.Architecture.Tests;

/// <summary>
/// Enforces DDD dependency rule: Domain must not reference Infrastructure, EF, RabbitMQ, ASP.NET.
/// </summary>
public class DomainIndependenceTests
{
    private static readonly Assembly Domain = typeof(NotificationHub.Domain.Delivery.Notification).Assembly;

    private static readonly string[] Forbidden =
    [
        "Microsoft.EntityFrameworkCore",
        "Npgsql",
        "RabbitMQ.Client",
        "Microsoft.AspNetCore",
        "StackExchange.Redis",
        "NotificationHub.Infrastructure",
        "NotificationHub.Host"
    ];

    [Fact]
    public void Domain_assembly_does_not_reference_infrastructure_or_framework_packages()
    {
        var refs = Domain.GetReferencedAssemblies().Select(a => a.Name ?? "").ToArray();
        foreach (var bad in Forbidden)
        {
            Assert.False(
                refs.Any(r => r.Equals(bad, StringComparison.OrdinalIgnoreCase) ||
                              r.StartsWith(bad + ".", StringComparison.OrdinalIgnoreCase)),
                $"Domain must not reference '{bad}'. Found: {string.Join(", ", refs)}");
        }
    }

    [Fact]
    public void Domain_types_do_not_expose_DbContext_or_IChannel()
    {
        foreach (var type in Domain.GetExportedTypes())
        {
            foreach (var prop in type.GetProperties())
            {
                var n = prop.PropertyType.FullName ?? "";
                Assert.DoesNotContain("DbContext", n);
                Assert.DoesNotContain("RabbitMQ", n);
            }
        }
    }
}
