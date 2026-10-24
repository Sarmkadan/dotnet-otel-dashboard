using System.Globalization;
using System.Text;

namespace DotnetOtelDashboard.Otlp;

/// <summary>
/// Decodes OTLP <c>AnyValue</c>, <c>KeyValue</c> and <c>Resource</c> messages from raw protobuf bytes into
/// flat string form, following <c>opentelemetry/proto/common/v1/common.proto</c> and
/// <c>opentelemetry/proto/resource/v1/resource.proto</c>.
/// </summary>
/// <remarks>
/// Unknown fields and fields with an unexpected wire type are skipped. When a field appears more than once,
/// the last occurrence wins, as the protobuf specification requires for singular fields.
/// </remarks>
internal static class OtlpAttributeDecoder
{
    private const string ServiceNameKey = "service.name";
    private const string UnknownServiceName = "unknown_service";

    // Bounds recursion through nested array and kvlist values so hostile input cannot exhaust the stack.
    private const int MaxNestingDepth = 32;

    /// <summary>
    /// Decodes an <c>AnyValue</c> message into its string form.
    /// </summary>
    /// <param name="bytes">Encoded <c>AnyValue</c> bytes.</param>
    /// <returns>
    /// The string value; booleans as <c>true</c>/<c>false</c>, integers in invariant culture, doubles in invariant culture,
    /// arrays as <c>[a, b]</c>, key-value lists as <c>{k=v, ...}</c>, and bytes as lowercase hex.
    /// An empty string when no value field is set.
    /// </returns>
    /// <exception cref="InvalidDataException">The payload is malformed or nested deeper than the supported limit.</exception>
    public static string DecodeAnyValue(ReadOnlySpan<byte> bytes)
    {
        return DecodeAnyValue(bytes, 0);
    }

    /// <summary>
    /// Decodes a <c>KeyValue</c> message into a key and its string-form value.
    /// </summary>
    /// <param name="bytes">Encoded <c>KeyValue</c> bytes.</param>
    /// <returns>The key and value. Missing fields decode as empty strings.</returns>
    /// <exception cref="InvalidDataException">The payload is malformed.</exception>
    public static KeyValuePair<string, string> DecodeKeyValue(ReadOnlySpan<byte> bytes)
    {
        return DecodeKeyValue(bytes, 0);
    }

    /// <summary>
    /// Decodes a <c>KeyValue</c> message and stores it in <paramref name="target"/>, replacing any existing entry with the same key.
    /// </summary>
    /// <param name="target">Dictionary receiving the decoded attribute.</param>
    /// <param name="keyValueBytes">Encoded <c>KeyValue</c> bytes.</param>
    /// <exception cref="InvalidDataException">The payload is malformed.</exception>
    public static void AddKeyValue(Dictionary<string, string> target, ReadOnlySpan<byte> keyValueBytes)
    {
        ArgumentNullException.ThrowIfNull(target);

        KeyValuePair<string, string> pair = DecodeKeyValue(keyValueBytes);
        target[pair.Key] = pair.Value;
    }

    /// <summary>
    /// Extracts the <c>service.name</c> attribute from a <c>Resource</c> message.
    /// </summary>
    /// <param name="resourceBytes">Encoded <c>Resource</c> bytes.</param>
    /// <returns>
    /// The value of the first <c>service.name</c> attribute, or <c>unknown_service</c> when the attribute is absent.
    /// </returns>
    /// <exception cref="InvalidDataException">The payload is malformed.</exception>
    public static string ExtractServiceName(ReadOnlySpan<byte> resourceBytes)
    {
        var reader = new ProtobufReader(resourceBytes);
        while (reader.TryReadTag(out int fieldNumber, out WireType wireType))
        {
            // Resource.attributes = 1 (repeated KeyValue)
            if (fieldNumber == 1 && wireType == WireType.LengthDelimited)
            {
                KeyValuePair<string, string> pair = DecodeKeyValue(reader.ReadLengthDelimited(), 0);
                if (pair.Key == ServiceNameKey)
                {
                    return pair.Value;
                }
            }
            else
            {
                reader.SkipField(wireType);
            }
        }

        return UnknownServiceName;
    }

    private static string DecodeAnyValue(ReadOnlySpan<byte> bytes, int depth)
    {
        if (depth > MaxNestingDepth)
        {
            throw new InvalidDataException($"AnyValue is nested deeper than {MaxNestingDepth} levels.");
        }

        string result = string.Empty;
        var reader = new ProtobufReader(bytes);
        while (reader.TryReadTag(out int fieldNumber, out WireType wireType))
        {
            switch (fieldNumber, wireType)
            {
                // string_value = 1
                case (1, WireType.LengthDelimited):
                    result = reader.ReadString();
                    break;

                // bool_value = 2
                case (2, WireType.Varint):
                    result = reader.ReadVarint() != 0 ? "true" : "false";
                    break;

                // int_value = 3
                case (3, WireType.Varint):
                    result = ((long)reader.ReadVarint()).ToString(CultureInfo.InvariantCulture);
                    break;

                // double_value = 4
                case (4, WireType.Fixed64):
                    result = reader.ReadDouble().ToString(CultureInfo.InvariantCulture);
                    break;

                // array_value = 5
                case (5, WireType.LengthDelimited):
                    result = DecodeArrayValue(reader.ReadLengthDelimited(), depth + 1);
                    break;

                // kvlist_value = 6
                case (6, WireType.LengthDelimited):
                    result = DecodeKeyValueList(reader.ReadLengthDelimited(), depth + 1);
                    break;

                // bytes_value = 7
                case (7, WireType.LengthDelimited):
                    result = Convert.ToHexString(reader.ReadLengthDelimited()).ToLowerInvariant();
                    break;

                default:
                    reader.SkipField(wireType);
                    break;
            }
        }

        return result;
    }

    private static string DecodeArrayValue(ReadOnlySpan<byte> bytes, int depth)
    {
        var builder = new StringBuilder("[");
        var reader = new ProtobufReader(bytes);
        bool first = true;
        while (reader.TryReadTag(out int fieldNumber, out WireType wireType))
        {
            // ArrayValue.values = 1 (repeated AnyValue)
            if (fieldNumber == 1 && wireType == WireType.LengthDelimited)
            {
                if (!first)
                {
                    builder.Append(", ");
                }

                builder.Append(DecodeAnyValue(reader.ReadLengthDelimited(), depth));
                first = false;
            }
            else
            {
                reader.SkipField(wireType);
            }
        }

        return builder.Append(']').ToString();
    }

    private static string DecodeKeyValueList(ReadOnlySpan<byte> bytes, int depth)
    {
        var builder = new StringBuilder("{");
        var reader = new ProtobufReader(bytes);
        bool first = true;
        while (reader.TryReadTag(out int fieldNumber, out WireType wireType))
        {
            // KeyValueList.values = 1 (repeated KeyValue)
            if (fieldNumber == 1 && wireType == WireType.LengthDelimited)
            {
                KeyValuePair<string, string> pair = DecodeKeyValue(reader.ReadLengthDelimited(), depth);
                if (!first)
                {
                    builder.Append(", ");
                }

                builder.Append(pair.Key).Append('=').Append(pair.Value);
                first = false;
            }
            else
            {
                reader.SkipField(wireType);
            }
        }

        return builder.Append('}').ToString();
    }

    private static KeyValuePair<string, string> DecodeKeyValue(ReadOnlySpan<byte> bytes, int depth)
    {
        string key = string.Empty;
        string value = string.Empty;
        var reader = new ProtobufReader(bytes);
        while (reader.TryReadTag(out int fieldNumber, out WireType wireType))
        {
            switch (fieldNumber, wireType)
            {
                // KeyValue.key = 1
                case (1, WireType.LengthDelimited):
                    key = reader.ReadString();
                    break;

                // KeyValue.value = 2 (AnyValue)
                case (2, WireType.LengthDelimited):
                    value = DecodeAnyValue(reader.ReadLengthDelimited(), depth + 1);
                    break;

                default:
                    reader.SkipField(wireType);
                    break;
            }
        }

        return new KeyValuePair<string, string>(key, value);
    }
}
