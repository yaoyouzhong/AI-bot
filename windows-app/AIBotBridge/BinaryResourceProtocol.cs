using System.Buffers.Binary;

namespace AIBotBridge;

internal enum BinaryResourceKind : byte
{
    TextBitmap = 1,
    MusicCover = 2,
    PetAsset = 3
}

internal sealed record BinaryResourceChunk(
    BinaryResourceKind Kind,
    uint TransferId,
    ushort Sequence,
    ushort TotalChunks,
    int TotalLength,
    uint WholeCrc32,
    byte[] Payload,
    byte[] WireBytes);

internal sealed record ResourcePayload(BinaryResourceKind Kind, int Revision, byte[] Data);

internal static class BinaryResourceProtocol
{
    internal const int MaxPayload = 768;
    private const int HeaderLength = 24;
    private static ReadOnlySpan<byte> Magic => "AIB1"u8;

    internal static IReadOnlyList<BinaryResourceChunk> CreateChunks(
        BinaryResourceKind kind, byte[] data, uint transferId)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length == 0 || data.Length > 1024 * 1024)
            throw new ArgumentOutOfRangeException(nameof(data), "Resource size must be 1..1048576 bytes.");
        var totalChunks = checked((ushort)((data.Length + MaxPayload - 1) / MaxPayload));
        var wholeCrc = Crc32(data);
        var result = new List<BinaryResourceChunk>(totalChunks);
        for (ushort sequence = 0; sequence < totalChunks; sequence++)
        {
            var offset = sequence * MaxPayload;
            var length = Math.Min(MaxPayload, data.Length - offset);
            var payload = data.AsSpan(offset, length).ToArray();
            var decoded = new byte[HeaderLength + length + 4];
            Magic.CopyTo(decoded);
            decoded[4] = 1;
            decoded[5] = (byte)kind;
            BinaryPrimitives.WriteUInt32LittleEndian(decoded.AsSpan(6), transferId);
            BinaryPrimitives.WriteUInt16LittleEndian(decoded.AsSpan(10), sequence);
            BinaryPrimitives.WriteUInt16LittleEndian(decoded.AsSpan(12), totalChunks);
            BinaryPrimitives.WriteInt32LittleEndian(decoded.AsSpan(14), data.Length);
            BinaryPrimitives.WriteUInt16LittleEndian(decoded.AsSpan(18), (ushort)length);
            BinaryPrimitives.WriteUInt32LittleEndian(decoded.AsSpan(20), wholeCrc);
            payload.CopyTo(decoded, HeaderLength);
            BinaryPrimitives.WriteUInt32LittleEndian(decoded.AsSpan(HeaderLength + length),
                Crc32(decoded.AsSpan(0, HeaderLength + length)));
            var encoded = CobsEncode(decoded);
            var wire = new byte[encoded.Length + 2];
            encoded.CopyTo(wire, 1);
            result.Add(new BinaryResourceChunk(kind, transferId, sequence, totalChunks,
                data.Length, wholeCrc, payload, wire));
        }
        return result;
    }

    internal static BinaryResourceChunk DecodeWire(byte[] wire)
    {
        if (wire.Length < 3 || wire[0] != 0 || wire[^1] != 0)
            throw new InvalidDataException("COBS wire frame must be NUL delimited.");
        var decoded = CobsDecode(wire.AsSpan(1, wire.Length - 2));
        if (decoded.Length < HeaderLength + 4 || !decoded.AsSpan(0, 4).SequenceEqual(Magic) || decoded[4] != 1)
            throw new InvalidDataException("Binary resource header is invalid.");
        var payloadLength = BinaryPrimitives.ReadUInt16LittleEndian(decoded.AsSpan(18));
        if (decoded.Length != HeaderLength + payloadLength + 4)
            throw new InvalidDataException("Binary resource length is invalid.");
        var expected = BinaryPrimitives.ReadUInt32LittleEndian(decoded.AsSpan(HeaderLength + payloadLength));
        if (Crc32(decoded.AsSpan(0, HeaderLength + payloadLength)) != expected)
            throw new InvalidDataException("Binary resource chunk CRC is invalid.");
        return new BinaryResourceChunk(
            (BinaryResourceKind)decoded[5],
            BinaryPrimitives.ReadUInt32LittleEndian(decoded.AsSpan(6)),
            BinaryPrimitives.ReadUInt16LittleEndian(decoded.AsSpan(10)),
            BinaryPrimitives.ReadUInt16LittleEndian(decoded.AsSpan(12)),
            BinaryPrimitives.ReadInt32LittleEndian(decoded.AsSpan(14)),
            BinaryPrimitives.ReadUInt32LittleEndian(decoded.AsSpan(20)),
            decoded.AsSpan(HeaderLength, payloadLength).ToArray(), wire);
    }

    internal static byte[] CobsEncode(ReadOnlySpan<byte> input)
    {
        var output = new byte[input.Length + input.Length / 254 + 2];
        var read = 0;
        var write = 1;
        var codeIndex = 0;
        byte code = 1;
        while (read < input.Length)
        {
            if (input[read] == 0)
            {
                output[codeIndex] = code;
                code = 1;
                codeIndex = write++;
                read++;
            }
            else
            {
                output[write++] = input[read++];
                code++;
                if (code == 0xFF)
                {
                    output[codeIndex] = code;
                    code = 1;
                    codeIndex = write++;
                }
            }
        }
        output[codeIndex] = code;
        return output.AsSpan(0, write).ToArray();
    }

    internal static byte[] CobsDecode(ReadOnlySpan<byte> input)
    {
        var output = new byte[input.Length];
        var read = 0;
        var write = 0;
        while (read < input.Length)
        {
            var code = input[read++];
            if (code == 0 || read + code - 1 > input.Length)
                throw new InvalidDataException("COBS payload is malformed.");
            for (var index = 1; index < code; index++) output[write++] = input[read++];
            if (code != 0xFF && read < input.Length) output[write++] = 0;
        }
        return output.AsSpan(0, write).ToArray();
    }

    internal static uint Crc32(ReadOnlySpan<byte> data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (var value in data)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1;
        }
        return ~crc;
    }
}
