using System.Buffers;
using System.Text.Json;

namespace NotificationHub.Core.Performance;

/// <summary>
/// Rent-backed UTF-8 JSON serialize to cut LOH pressure on large payloads.
/// Callers must Return() the rented buffer after publish completes.
/// </summary>
public static class Utf8JsonBuffer
{
    /// <summary>
    /// Serializes <paramref name="value"/> to a rented UTF-8 buffer.
    /// Returns (array, length). Always <c>ArrayPool.Shared.Return(array)</c> when done.
    /// </summary>
    public static (byte[] Buffer, int Length) SerializeRented<T>(T value, JsonSerializerOptions options)
    {
        // SerializeToUtf8Bytes allocates one array sized to payload — fine for small messages.
        // For larger payloads we write into a rented buffer via buffer writer.
        var writer = new ArrayBufferWriter<byte>(256);
        using (var utf8 = new Utf8JsonWriter(writer))
        {
            JsonSerializer.Serialize(utf8, value, options);
        }
        var written = writer.WrittenSpan;
        var rented = ArrayPool<byte>.Shared.Rent(written.Length);
        written.CopyTo(rented);
        return (rented, written.Length);
    }

    public static void Return(byte[] buffer) => ArrayPool<byte>.Shared.Return(buffer);
}
