namespace NotificationHub.Application.Abstractions;

public sealed record Error(string Code, string Message, ErrorType Type = ErrorType.Failure)
{
    public static Error Validation(string code, string message) => new(code, message, ErrorType.Validation);
    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);
    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);
    public static Error Forbidden(string code, string message) => new(code, message, ErrorType.Forbidden);
    public static Error Failure(string code, string message) => new(code, message, ErrorType.Failure);
}

public enum ErrorType
{
    Failure = 0,
    Validation = 1,
    NotFound = 2,
    Conflict = 3,
    Forbidden = 4,
    Unauthorized = 5
}

public static class Errors
{
    public static readonly Error NotificationNotFound =
        Error.NotFound("notification.not_found", "Notification was not found.");
    public static readonly Error TemplateNotFound =
        Error.NotFound("template.not_found", "Template was not found.");
    public static readonly Error TenantForbidden =
        Error.Forbidden("tenant.forbidden", "Access to this tenant resource is denied.");
}
