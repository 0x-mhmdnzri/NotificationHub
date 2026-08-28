namespace NotificationHub.Domain.Templates;

public readonly record struct TemplateId(Guid Value)
{
    public static TemplateId New() => new(Guid.NewGuid());
    public static TemplateId From(Guid value) => new(value == Guid.Empty ? throw new ArgumentException("empty") : value);
}
