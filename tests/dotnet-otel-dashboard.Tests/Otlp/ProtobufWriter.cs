using System.Buffers.Binary;
using System.Text;
using DotnetOtelDashboard.Otlp;

namespace DotnetOtelDashboard.Tests.Otlp;

/// <summary>
/// Test helper that encodes protobuf wire format so decoder tests can build payloads by hand.
/// Mirrors <see cref="ProtobufReader"/>; intentionally minimal and not a production encoder.
/// </summary>
internal sealed class ProtobufWriter
{
    private readonly MemoryStream _stream = new();

    /// <summary>Writes a varint-encoded field.</summary>
    public void WriteVarint(int field, ulong value)
    {
        WriteTag(field, WireType.Varint);
        WriteBase128(value);
    }

    /// <summary>Writes a little-endian 8-byte field.</summary>
    public void WriteFixed64(int field, ulong value)
    {
        WriteTag(field, WireType.Fixed64);
        Span<byte> buffer = stackalloc byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt64LittleEndian(buffer, value);
        _stream.Write(buffer);
    }

    /// <summary>Writes an IEEE 754 double as a fixed64 field.</summary>
    public void WriteDouble(int field, double value)
    {
        WriteFixed64(field, (ulong)BitConverter.DoubleToInt64Bits(value));
    }

    /// <summary>Writes a little-endian 4-byte field.</summary>
    public void WriteFixed32(int field, uint value)
    {
        WriteTag(field, WireType.Fixed32);
        Span<byte> buffer = stackalloc byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32LittleEndian(buffer, value);
        _stream.Write(buffer);
    }

    /// <summary>Writes a length-delimited field containing raw bytes.</summary>
    public void WriteBytes(int field, ReadOnlySpan<byte> value)
    {
        WriteTag(field, WireType.LengthDelimited);
        WriteBase128((ulong)value.Length);
        _stream.Write(value);
    }

    /// <summary>Writes a length-delimited field containing the UTF-8 encoding of <paramref name="value"/>.</summary>
    public void WriteString(int field, string value)
    {
        WriteBytes(field, Encoding.UTF8.GetBytes(value));
    }

    /// <summary>Writes a length-delimited field containing an embedded message.</summary>
    public void WriteMessage(int field, ProtobufWriter nested)
    {
        WriteBytes(field, nested.ToArray());
    }

    /// <summary>Returns the encoded bytes written so far.</summary>
    public byte[] ToArray() => _stream.ToArray();

    private void WriteTag(int field, WireType wireType)
    {
        WriteBase128(((ulong)field << 3) | (ulong)wireType);
    }

    private void WriteBase128(ulong value)
    {
        while (value >= 0x80)
        {
            _stream.WriteByte((byte)(value | 0x80));
            value >>= 7;
        }

        _stream.WriteByte((byte)value);
    }
}
