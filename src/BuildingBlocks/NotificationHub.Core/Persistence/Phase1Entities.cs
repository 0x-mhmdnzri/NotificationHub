namespace NotificationHub.Core.Persistence;

public sealed class DigestPolicyEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Key { get; set; } = "";
    public string? TenantId { get; set; }
    public int WindowMinutes { get; set; } = 60;
    public string Channel { get; set; } = "email";
    public string TemplateKey { get; set; } = "digest-default";
    public bool IsActive { get; set; } = true;
}

public sealed class DigestBufferEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string PolicyKey { get; set; } = "";
    public string Recipient { get; set; } = "";
    public string? TenantId { get; set; }
    public string PayloadJson { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? FlushedAt { get; set; }
}

public sealed class ThrottlePolicyEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Key { get; set; } = "";
    public string? TenantId { get; set; }
    public string? Channel { get; set; }
    public int MaxCount { get; set; } = 10;
    public int WindowMinutes { get; set; } = 60;
    public bool IsActive { get; set; } = true;
}

public sealed class ThrottleCounterEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string PolicyKey { get; set; } = "";
    public string Recipient { get; set; } = "";
    public string? TenantId { get; set; }
    public string? Channel { get; set; }
    public DateTimeOffset WindowStart { get; set; }
    public int Count { get; set; }
}

public sealed class TopicEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Key { get; set; } = "";
    public string? Name { get; set; }
    public string? TenantId { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class TopicSubscriberEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TopicKey { get; set; } = "";
    public string SubscriberId { get; set; } = "";
    public string? TenantId { get; set; }
    public string? Channel { get; set; }
    public string? Address { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class DeviceTokenEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = "";
    public string? TenantId { get; set; }
    public string Platform { get; set; } = "";
    public string Token { get; set; } = "";
    public string? Locale { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
