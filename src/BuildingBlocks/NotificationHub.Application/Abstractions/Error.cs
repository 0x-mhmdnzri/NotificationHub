namespace NotificationHub.Application.Abstractions;

/// <summary>
/// Typed, machine-readable application error (expected business outcomes).
/// Not a substitute for exceptions (unexpected / infrastructure failures).
/// </summary>
public sealed record Error(
    string Code,
    string Message,
    ErrorType Type = ErrorType.Failure,
    string? PropertyName = null,
    IReadOnlyDictionary<string, object?>? Metadata = null)
{
    public static Error Validation(string code, string message, string? property = null)
        => new(code, message, ErrorType.Validation, property);

    public static Error NotFound(string code, string message)
        => new(code, message, ErrorType.NotFound);

    public static Error Conflict(string code, string message)
        => new(code, message, ErrorType.Conflict);

    public static Error Forbidden(string code, string message)
        => new(code, message, ErrorType.Forbidden);

    public static Error Unauthorized(string code, string message)
        => new(code, message, ErrorType.Unauthorized);

    public static Error Failure(string code, string message)
        => new(code, message, ErrorType.Failure);

    public static Error RateLimited(string code, string message)
        => new(code, message, ErrorType.RateLimited);

    public static Error BusinessRule(string code, string message)
        => new(code, message, ErrorType.BusinessRule);
}

public enum ErrorType
{
    Failure = 0,
    Validation = 1,
    NotFound = 2,
    Conflict = 3,
    Forbidden = 4,
    Unauthorized = 5,
    RateLimited = 6,
    /// <summary>Domain / business rule rejection (often maps to 422).</summary>
    BusinessRule = 7
}

/// <summary>Stable application error catalog — public codes are API contracts.</summary>
public static class Errors
{
    public static readonly Error NotificationNotFound =
        Error.NotFound("notification.not_found", "Notification was not found.");

    public static readonly Error TemplateNotFound =
        Error.NotFound("template.not_found", "Template was not found.");

    public static readonly Error CampaignNotFound =
        Error.NotFound("campaign.not_found", "Campaign was not found.");

    public static readonly Error WorkflowNotFound =
        Error.NotFound("workflow.not_found", "Workflow was not found.");

    public static readonly Error TenantForbidden =
        Error.Forbidden("tenant.forbidden", "Access to this tenant resource is denied.");

    public static readonly Error PreferenceForbidden =
        Error.Forbidden("preference.denied", "User preference or consent blocks delivery.");

    public static readonly Error InvalidState =
        Error.BusinessRule("domain.invalid_state", "The operation is not valid for the current state.");

    public static readonly Error IdempotentConflict =
        Error.Conflict("request.duplicate", "An identical request was already processed.");

    public static readonly Error ValidationFailed =
        Error.Validation("validation.failed", "One or more validation errors occurred.");
}

/// <summary>Notification-bounded error codes.</summary>
public static class NotificationErrors
{
    public static readonly Error NotFound = Errors.NotificationNotFound;
    public static readonly Error AlreadyCancelled =
        Error.Conflict("notification.already_cancelled", "Notification is already cancelled.");
    public static readonly Error CannotAccept =
        Error.BusinessRule("notification.cannot_accept", "Notification cannot be accepted in its current state.");
    public static readonly Error Suppressed =
        Error.Forbidden("notification.suppressed", "Notification was suppressed by preference or consent.");
}

/// <summary>Campaign-bounded error codes.</summary>
public static class CampaignErrors
{
    public static readonly Error NotFound = Errors.CampaignNotFound;
    public static readonly Error InvalidTransition =
        Error.BusinessRule("campaign.invalid_transition", "Campaign lifecycle transition is not allowed.");
    public static readonly Error EmptyRecipients =
        Error.Validation("campaign.recipients_empty", "Campaign has no recipients.");
}
