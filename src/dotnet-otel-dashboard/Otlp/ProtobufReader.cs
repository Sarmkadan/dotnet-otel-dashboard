using System.Buffers.Binary;
using System.Text;

namespace DotnetOtelDashboard.Otlp;

/// <summary>
/// Protobuf wire-format types. The values match the encoding defined by the protobuf specification.
/// </summary>
internal enum WireType
{
    /// <summary>Variable-length integer (int32, int64, uint32, uint64, sint32, sint64, bool, enum).</summary>
    Varint = 0,

    /// <summary>Little-endian 8-byte value (fixed64, sfixed64, double).</summary>
    Fixed64 = 1,

    /// <summary>Varint length followed by that many bytes (string, bytes, embedded messages, packed repeated fields).</summary>
    LengthDelimited = 2,

    /// <summary>Deprecated group start. Not supported by this reader.</summary>
    StartGroup = 3,

    /// <summary>Deprecated group end. Not supported by this reader.</summary>
    EndGroup = 4,

    /// <summary>Little-endian 4-byte value (fixed32, sfixed32, float).</summary>
    Fixed32 = 5,
}

/// <summary>
/// Dependency-free, forward-only reader for the protobuf wire format. Used to decode OTLP payloads
/// without referencing Google.Protobuf, so protobuf types never leak into the public API.
/// </summary>
/// <remarks>
/// The reader does not allocate; string and byte payloads are returned as slices of the input span
/// or decoded on demand. Malformed or truncated input throws <see cref="InvalidDataException"/>.
/// </remarks>
internal ref struct ProtobufReader
{
    private const int MaxVarintBytes = 10;

    // Largest field number permitted by the protobuf specification (2^29 - 1).
    private const ulong MaxFieldNumber = (1UL << 29) - 1;

    private readonly ReadOnlySpan<byte> _data;
    private int _position;

    /// <summary>
    /// Creates a reader positioned at the start of <paramref name="data"/>.
    /// </summary>
    /// <param name="data">Encoded protobuf message bytes.</param>
    public ProtobufReader(ReadOnlySpan<byte> data)
    {
        _data = data;
    }

    /// <summary>
    /// <c>true</c> when every byte of the input has been consumed.
    /// </summary>
    public readonly bool IsAtEnd => _position >= _data.Length;

    /// <summary>
    /// Reads the next field tag. Returns <c>false</c> at the end of the input.
    /// </summary>
    /// <param name="fieldNumber">Field number taken from the tag (tag &gt;&gt; 3).</param>
    /// <param name="wireType">Wire type taken from the tag (tag &amp; 7).</param>
    /// <returns><c>true</c> if a tag was read; <c>false</c> if the input is exhausted.</returns>
    /// <exception cref="InvalidDataException">The tag is truncated, has field number 0 or out of range, or uses an undefined wire type.</exception>
    public bool TryReadTag(out int fieldNumber, out WireType wireType)
    {
        if (IsAtEnd)
        {
            fieldNumber = 0;
            wireType = default;
            return false;
        }

        ulong tag = ReadVarint();
        ulong field = tag >> 3;
        if (field == 0 || field > MaxFieldNumber)
        {
            throw new InvalidDataException($"Invalid protobuf field number {field}.");
        }

        ulong wire = tag & 0x7;
        if (wire is 6 or 7)
        {
            throw new InvalidDataException($"Invalid protobuf wire type {wire}.");
        }

        fieldNumber = (int)field;
        wireType = (WireType)wire;
        return true;
    }

    /// <summary>
    /// Reads a base-128 varint (up to 10 bytes) as an unsigned 64-bit value.
    /// </summary>
    /// <returns>The decoded value.</returns>
    /// <exception cref="InvalidDataException">The input ends mid-varint, or the varint is longer than 10 bytes.</exception>
    public ulong ReadVarint()
    {
        ulong result = 0;
        for (int i = 0; i < MaxVarintBytes; i++)
        {
            byte b = NextByte();
            result |= (ulong)(b & 0x7F) << (7 * i);
            if ((b & 0x80) == 0)
            {
                return result;
            }
        }

        throw new InvalidDataException($"Varint is longer than {MaxVarintBytes} bytes.");
    }

    /// <summary>
    /// Reads a little-endian 4-byte value.
    /// </summary>
    /// <returns>The decoded value.</returns>
    /// <exception cref="InvalidDataException">Fewer than 4 bytes remain.</exception>
    public uint ReadFixed32()
    {
        return BinaryPrimitives.ReadUInt32LittleEndian(Take(sizeof(uint)));
    }

    /// <summary>
    /// Reads a little-endian 8-byte value.
    /// </summary>
    /// <returns>The decoded value.</returns>
    /// <exception cref="InvalidDataException">Fewer than 8 bytes remain.</exception>
    public ulong ReadFixed64()
    {
        return BinaryPrimitives.ReadUInt64LittleEndian(Take(sizeof(ulong)));
    }

    /// <summary>
    /// Reads a little-endian 8-byte IEEE 754 double.
    /// </summary>
    /// <returns>The decoded value.</returns>
    /// <exception cref="InvalidDataException">Fewer than 8 bytes remain.</exception>
    public double ReadDouble()
    {
        return BitConverter.Int64BitsToDouble((long)ReadFixed64());
    }

    /// <summary>
    /// Reads a varint length prefix followed by that many bytes. The returned span aliases the input.
    /// </summary>
    /// <returns>A slice of the input holding the payload.</returns>
    /// <exception cref="InvalidDataException">The declared length exceeds the remaining input.</exception>
    public ReadOnlySpan<byte> ReadLengthDelimited()
    {
        ulong length = ReadVarint();
        if (length > (ulong)(_data.Length - _position))
        {
            throw Truncated();
        }

        return Take((int)length);
    }

    /// <summary>
    /// Reads a length-delimited payload and decodes it as UTF-8.
    /// </summary>
    /// <returns>The decoded string. Invalid UTF-8 sequences are replaced with U+FFFD.</returns>
    /// <exception cref="InvalidDataException">The declared length exceeds the remaining input.</exception>
    public string ReadString()
    {
        return Encoding.UTF8.GetString(ReadLengthDelimited());
    }

    /// <summary>
    /// Advances past the value of a field whose tag has already been read.
    /// </summary>
    /// <param name="wireType">Wire type of the field being skipped.</param>
    /// <exception cref="InvalidDataException">The value is truncated, or the wire type is a group or otherwise unsupported.</exception>
    public void SkipField(WireType wireType)
    {
        switch (wireType)
        {
            case WireType.Varint:
                _ = ReadVarint();
                break;
            case WireType.Fixed64:
                _ = Take(sizeof(ulong));
                break;
            case WireType.LengthDelimited:
                _ = ReadLengthDelimited();
                break;
            case WireType.Fixed32:
                _ = Take(sizeof(uint));
                break;
            default:
                throw new InvalidDataException($"Cannot skip protobuf wire type {wireType}.");
        }
    }

    private byte NextByte()
    {
        if (IsAtEnd)
        {
            throw Truncated();
        }

        return _data[_position++];
    }

    private ReadOnlySpan<byte> Take(int count)
    {
        if (count > _data.Length - _position)
        {
            throw Truncated();
        }

        ReadOnlySpan<byte> slice = _data.Slice(_position, count);
        _position += count;
        return slice;
    }

    private static InvalidDataException Truncated() => new("Unexpected end of protobuf data.");
}
