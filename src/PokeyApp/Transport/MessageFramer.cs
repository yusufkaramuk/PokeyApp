using System.Buffers.Binary;
using System.IO;

namespace PokeyApp.Transport;

/// <summary>
/// Mesajları 4-byte length prefix + payload formatında encode/decode eder.
/// Tüm socket işlemlerinden bağımsız, unit test edilebilir.
/// </summary>
public static class MessageFramer
{
    private const int HeaderSize = 4; // uint32 = 4 byte

    /// <summary>Payload'u length-prefixed frame'e dönüştürür.</summary>
    public static byte[] Encode(byte[] payload)
    {
        var frame = new byte[HeaderSize + payload.Length];
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(0, HeaderSize), (uint)payload.Length);
        payload.CopyTo(frame, HeaderSize);
        return frame;
    }

    /// <summary>
    /// Stream'den tek bir frame okur. Bağlantı kapandıysa null döner.
    /// Kısmi okumalar otomatik olarak handle edilir.
    /// </summary>
    public static async Task<byte[]?> ReadFrameAsync(Stream stream, CancellationToken ct)
    {
        // Önce header oku
        var header = new byte[HeaderSize];
        if (!await ReadExactAsync(stream, header, ct))
            return null;

        var length = BinaryPrimitives.ReadUInt32BigEndian(header);

        // Payload boyutu makul bir limit içinde mi?
        if (length > 1_048_576) // 1 MB üzeri kabul etme
            throw new InvalidDataException($"Frame boyutu çok büyük: {length} byte");

        var payload = new byte[length];
        if (!await ReadExactAsync(stream, payload, ct))
            return null;

        return payload;
    }

    private static async Task<bool> ReadExactAsync(Stream stream, byte[] buffer, CancellationToken ct)
    {
        int totalRead = 0;
        while (totalRead < buffer.Length)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(totalRead), ct);
            if (read == 0)
                return false; // Bağlantı kapandı
            totalRead += read;
        }
        return true;
    }
}
