using System.Text;

namespace NotificationHub.Host.Security;

/// <summary>Strip CR/LF and control chars from untrusted values before writing to logs (CodeQL cs/log-forging).</summary>
public static class LogSanitizer
{
    public static string Sanitize(string? value, int maxLength = 256)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var sb = new StringBuilder(Math.Min(value.Length, maxLength));
        foreach (var ch in value)
        {
            if (ch is '\r' or '\n' or '\0')
                sb.Append('_');
            else if (char.IsControl(ch))
                continue;
            else
                sb.Append(ch);

            if (sb.Length >= maxLength)
                break;
        }

        return sb.ToString();
    }
}
