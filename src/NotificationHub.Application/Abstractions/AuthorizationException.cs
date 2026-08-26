namespace NotificationHub.Application.Abstractions;

public sealed class AuthorizationException : Exception
{
    public string Code { get; }
    public int StatusCode { get; }

    public AuthorizationException(string message, string code = "auth.forbidden", int statusCode = 403)
        : base(message)
    {
        Code = code;
        StatusCode = statusCode;
    }
}
