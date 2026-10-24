using DotnetOtelDashboard.Otlp;
using DotnetOtelDashboard.Tests.Otlp;

namespace DotnetOtelDashboard.Tests;

public class ProtobufReaderTests
{
    [Fact]
    public void ReadsHandWrittenVarintField()
    {
        // 0x08 = field 1, wire type 0 (varint); 0x96 0x01 = 150.
        byte[] data = [0x08, 0x96, 0x01];
        var reader = new ProtobufReader(data);

        Assert.True(reader.TryReadTag(out int field, out WireType wireType));
        Assert.Equal(1, field);
        Assert.Equal(WireType.Varint, wireType);
        Assert.Equal(150UL, reader.ReadVarint());
        Assert.True(reader.IsAtEnd);
    }

    [Fact]
    public void MultiByteVarint_RoundTrips()
    {
        var writer = new ProtobufWriter();
        writer.WriteVarint(1, 300);

        var reader = new ProtobufReader(writer.ToArray());

        Assert.True(reader.TryReadTag(out int field, out WireType wireType));
        Assert.Equal(1, field);
        Assert.Equal(WireType.Varint, wireType);
        Assert.Equal(300UL, reader.ReadVarint());
        Assert.True(reader.IsAtEnd);
    }

    [Fact]
    public void TenByteVarint_RoundTripsUlongMaxValue()
    {
        var writer = new ProtobufWriter();
        writer.WriteVarint(1, ulong.MaxValue);
        byte[] bytes = writer.ToArray();

        // 1 tag byte + 10 varint bytes.
        Assert.Equal(11, bytes.Length);

        var reader = new ProtobufReader(bytes);
        Assert.True(reader.TryReadTag(out _, out _));
        Assert.Equal(ulong.MaxValue, reader.ReadVarint());
    }

    [Fact]
    public void Fixed64_RoundTrips()
    {
        const ulong value = 0x0102030405060708UL;
        var writer = new ProtobufWriter();
        writer.WriteFixed64(2, value);

        var reader = new ProtobufReader(writer.ToArray());

        Assert.True(reader.TryReadTag(out int field, out WireType wireType));
        Assert.Equal(2, field);
        Assert.Equal(WireType.Fixed64, wireType);
        Assert.Equal(value, reader.ReadFixed64());
        Assert.True(reader.IsAtEnd);
    }

    [Fact]
    public void Double_RoundTripsViaBitConverter()
    {
        const double value = -1234.5678;
        var writer = new ProtobufWriter();
        writer.WriteDouble(3, value);

        var reader = new ProtobufReader(writer.ToArray());

        Assert.True(reader.TryReadTag(out int field, out WireType wireType));
        Assert.Equal(3, field);
        Assert.Equal(WireType.Fixed64, wireType);
        Assert.Equal(value, reader.ReadDouble());
        Assert.True(reader.IsAtEnd);
    }

    [Fact]
    public void Fixed32_RoundTrips()
    {
        const uint value = 0xDEADBEEF;
        var writer = new ProtobufWriter();
        writer.WriteFixed32(4, value);

        var reader = new ProtobufReader(writer.ToArray());

        Assert.True(reader.TryReadTag(out int field, out WireType wireType));
        Assert.Equal(4, field);
        Assert.Equal(WireType.Fixed32, wireType);
        Assert.Equal(value, reader.ReadFixed32());
        Assert.True(reader.IsAtEnd);
    }

    [Fact]
    public void LengthDelimitedString_RoundTrips()
    {
        var writer = new ProtobufWriter();
        writer.WriteString(5, "hello");

        var reader = new ProtobufReader(writer.ToArray());

        Assert.True(reader.TryReadTag(out int field, out WireType wireType));
        Assert.Equal(5, field);
        Assert.Equal(WireType.LengthDelimited, wireType);
        Assert.Equal("hello", reader.ReadString());
        Assert.True(reader.IsAtEnd);
    }

    [Fact]
    public void TryReadTag_ReturnsFalseAtEnd()
    {
        var reader = new ProtobufReader([]);

        Assert.True(reader.IsAtEnd);
        Assert.False(reader.TryReadTag(out int field, out WireType wireType));
        Assert.Equal(0, field);
        Assert.Equal(default, wireType);
    }

    [Fact]
    public void TryReadTag_ReturnsFalseAfterLastField()
    {
        var writer = new ProtobufWriter();
        writer.WriteVarint(1, 7);

        var reader = new ProtobufReader(writer.ToArray());

        Assert.True(reader.TryReadTag(out _, out _));
        _ = reader.ReadVarint();
        Assert.False(reader.TryReadTag(out _, out _));
    }

    // Wire type passed as its numeric value because WireType is internal and this test class is public.
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    public void SkipField_SkipsValueAndPositionsOnNextTag(int wireTypeValue)
    {
        var skipped = (WireType)wireTypeValue;
        var writer = new ProtobufWriter();
        WriteFieldOfType(writer, 1, skipped);
        writer.WriteVarint(2, 42);

        var reader = new ProtobufReader(writer.ToArray());

        Assert.True(reader.TryReadTag(out int field, out WireType wireType));
        Assert.Equal(1, field);
        Assert.Equal(skipped, wireType);
        reader.SkipField(wireType);

        Assert.True(reader.TryReadTag(out field, out wireType));
        Assert.Equal(2, field);
        Assert.Equal(WireType.Varint, wireType);
        Assert.Equal(42UL, reader.ReadVarint());
        Assert.True(reader.IsAtEnd);
    }

    [Fact]
    public void SkipField_SkipsMultipleFieldsInSequence()
    {
        var writer = new ProtobufWriter();
        writer.WriteVarint(1, 1);
        writer.WriteFixed64(2, 2);
        writer.WriteString(3, "skip me");
        writer.WriteFixed32(4, 4);
        writer.WriteVarint(5, 55);

        var reader = new ProtobufReader(writer.ToArray());
        var fieldsSeen = new List<int>();
        while (reader.TryReadTag(out int field, out WireType wireType))
        {
            fieldsSeen.Add(field);
            if (field == 5)
            {
                Assert.Equal(55UL, reader.ReadVarint());
            }
            else
            {
                reader.SkipField(wireType);
            }
        }

        Assert.Equal([1, 2, 3, 4, 5], fieldsSeen);
        Assert.True(reader.IsAtEnd);
    }

    [Fact]
    public void ReadVarint_Truncated_Throws()
    {
        byte[] data = [0x80];

        Assert.Throws<InvalidDataException>(() => new ProtobufReader(data).ReadVarint());
    }

    [Fact]
    public void ReadString_TruncatedLengthDelimited_Throws()
    {
        // Field 1, wire type 2, declared length 5, but only 2 payload bytes present.
        byte[] data = [0x0A, 0x05, (byte)'h', (byte)'i'];

        Assert.Throws<InvalidDataException>(() =>
        {
            var reader = new ProtobufReader(data);
            Assert.True(reader.TryReadTag(out _, out _));
            _ = reader.ReadString();
        });
    }

    [Fact]
    public void SkipField_StartGroup_Throws()
    {
        // Field 1, wire type 3 (start group). Groups are deprecated and unsupported.
        byte[] data = [0x0B, 0x08, 0x01, 0x0C];

        Assert.Throws<InvalidDataException>(() =>
        {
            var reader = new ProtobufReader(data);
            Assert.True(reader.TryReadTag(out _, out WireType wireType));
            reader.SkipField(wireType);
        });
    }

    private static void WriteFieldOfType(ProtobufWriter writer, int field, WireType wireType)
    {
        switch (wireType)
        {
            case WireType.Varint:
                writer.WriteVarint(field, 0x1234);
                break;
            case WireType.Fixed64:
                writer.WriteFixed64(field, 0x1122334455667788UL);
                break;
            case WireType.LengthDelimited:
                writer.WriteBytes(field, [1, 2, 3, 4, 5]);
                break;
            case WireType.Fixed32:
                writer.WriteFixed32(field, 0xCAFEBABE);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(wireType), wireType, null);
        }
    }
}
