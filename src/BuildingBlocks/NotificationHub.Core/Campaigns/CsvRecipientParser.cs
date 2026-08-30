using System.Globalization;
using System.Text;

namespace NotificationHub.Core.Campaigns;

/// <summary>Streaming CSV parser for recipient import — does not load entire file into memory.</summary>
public static class CsvRecipientParser
{
    private static readonly HashSet<string> AddressHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "phone", "phonenumber", "mobile", "msisdn", "email", "recipient", "address",
        "telegramid", "telegram", "tg", "whatsapp", "whatsappnumber", "userid", "id"
    };

    public static async Task<List<string>> ParseAddressesAsync(Stream stream, CancellationToken ct = default)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var results = new List<string>();
        string? headerLine = await reader.ReadLineAsync(ct);
        if (headerLine is null)
            return results;

        var headers = SplitCsvLine(headerLine).Select(NormalizeHeader).ToArray();
        var addressCol = -1;
        for (var i = 0; i < headers.Length; i++)
        {
            if (AddressHeaders.Contains(headers[i]))
            {
                addressCol = i;
                break;
            }
        }

        // No header match → treat first column as address (headerless or single column)
        if (addressCol < 0)
        {
            // If only one column and looks like data not header
            if (headers.Length == 1 && !AddressHeaders.Contains(headers[0]))
            {
                var first = headers[0].Trim();
                if (!string.IsNullOrEmpty(first))
                    results.Add(first);
                addressCol = 0;
            }
            else
                addressCol = 0;
        }

        while (await reader.ReadLineAsync(ct) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;
            var cols = SplitCsvLine(line);
            if (addressCol >= cols.Count)
                continue;
            var addr = cols[addressCol].Trim();
            if (addr.Length > 0)
                results.Add(addr);
        }

        return results;
    }

    public static string NormalizeHeader(string header) =>
        header.Trim().Replace("_", "", StringComparison.Ordinal).Replace("-", "", StringComparison.Ordinal)
            .ToLowerInvariant();

    private static List<string> SplitCsvLine(string line)
    {
        var list = new List<string>();
        var sb = new StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }
            if (c == ',' && !inQuotes)
            {
                list.Add(sb.ToString());
                sb.Clear();
                continue;
            }
            sb.Append(c);
        }
        list.Add(sb.ToString());
        return list;
    }
}
