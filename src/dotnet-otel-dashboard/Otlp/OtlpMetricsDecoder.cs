namespace DotnetOtelDashboard.Otlp;

/// <summary>
/// Decodes OTLP <c>ExportMetricsServiceRequest</c> payloads into <see cref="MetricPoint"/> values, following
/// <c>opentelemetry/proto/collector/metrics/v1/metrics_service.proto</c> and <c>opentelemetry/proto/metrics/v1/metrics.proto</c>.
/// </summary>
/// <remarks>
/// Gauge, sum and histogram metrics are decoded. Summary and exponential histogram metrics are skipped.
/// Unknown fields are skipped. Malformed input throws <see cref="InvalidDataException"/>.
/// </remarks>
internal static class OtlpMetricsDecoder
{
    // Largest tick count (100 ns units) that fits between the Unix epoch and DateTimeOffset.MaxValue.
    private static readonly ulong MaxUnixTicks = (ulong)(DateTimeOffset.MaxValue.UtcTicks - DateTimeOffset.UnixEpoch.UtcTicks);

    /// <summary>
    /// Decodes an <c>ExportMetricsServiceRequest</c> message into the metric data points it contains.
    /// </summary>
    /// <param name="exportMetricsServiceRequest">Encoded <c>ExportMetricsServiceRequest</c> bytes.</param>
    /// <returns>Data points in payload order. Empty when the request carries no supported metrics.</returns>
    /// <exception cref="InvalidDataException">The payload is malformed.</exception>
    public static List<MetricPoint> Decode(ReadOnlySpan<byte> exportMetricsServiceRequest)
    {
        var points = new List<MetricPoint>();
        var reader = new ProtobufReader(exportMetricsServiceRequest);
        while (reader.TryReadTag(out int fieldNumber, out WireType wireType))
        {
            // resource_metrics = 1
            if (fieldNumber == 1 && wireType == WireType.LengthDelimited)
            {
                DecodeResourceMetrics(reader.ReadLengthDelimited(), points);
            }
            else
            {
                reader.SkipField(wireType);
            }
        }

        return points;
    }

    private static void DecodeResourceMetrics(ReadOnlySpan<byte> bytes, List<MetricPoint> points)
    {
        // The resource may appear after scope_metrics on the wire, so locate it before decoding any metric.
        ReadOnlySpan<byte> resource = default;
        var reader = new ProtobufReader(bytes);
        while (reader.TryReadTag(out int fieldNumber, out WireType wireType))
        {
            // resource = 1
            if (fieldNumber == 1 && wireType == WireType.LengthDelimited)
            {
                resource = reader.ReadLengthDelimited();
            }
            else
            {
                reader.SkipField(wireType);
            }
        }

        string serviceName = OtlpAttributeDecoder.ExtractServiceName(resource);

        reader = new ProtobufReader(bytes);
        while (reader.TryReadTag(out int fieldNumber, out WireType wireType))
        {
            // scope_metrics = 2
            if (fieldNumber == 2 && wireType == WireType.LengthDelimited)
            {
                DecodeScopeMetrics(reader.ReadLengthDelimited(), serviceName, points);
            }
            else
            {
                reader.SkipField(wireType);
            }
        }
    }

    private static void DecodeScopeMetrics(ReadOnlySpan<byte> bytes, string serviceName, List<MetricPoint> points)
    {
        var reader = new ProtobufReader(bytes);
        while (reader.TryReadTag(out int fieldNumber, out WireType wireType))
        {
            // metrics = 2
            if (fieldNumber == 2 && wireType == WireType.LengthDelimited)
            {
                DecodeMetric(reader.ReadLengthDelimited(), serviceName, points);
            }
            else
            {
                reader.SkipField(wireType);
            }
        }
    }

    private static void DecodeMetric(ReadOnlySpan<byte> bytes, string serviceName, List<MetricPoint> points)
    {
        string name = string.Empty;
        string unit = string.Empty;
        MetricType? type = null;
        ReadOnlySpan<byte> dataPoints = default;

        // Fields may arrive in any order, so the name and unit are collected before the data points are decoded.
        var reader = new ProtobufReader(bytes);
        while (reader.TryReadTag(out int fieldNumber, out WireType wireType))
        {
            switch (fieldNumber, wireType)
            {
                // name = 1
                case (1, WireType.LengthDelimited):
                    name = reader.ReadString();
                    break;

                // unit = 3
                case (3, WireType.LengthDelimited):
                    unit = reader.ReadString();
                    break;

                // gauge = 5
                case (5, WireType.LengthDelimited):
                    type = MetricType.Gauge;
                    dataPoints = reader.ReadLengthDelimited();
                    break;

                // sum = 7
                case (7, WireType.LengthDelimited):
                    type = MetricType.Sum;
                    dataPoints = reader.ReadLengthDelimited();
                    break;

                // histogram = 9
                case (9, WireType.LengthDelimited):
                    type = MetricType.Histogram;
                    dataPoints = reader.ReadLengthDelimited();
                    break;

                // exponential_histogram = 10 and summary = 11 are unsupported. They belong to the same oneof,
                // so they clear any earlier selection rather than leaving a supported type behind.
                case (10, WireType.LengthDelimited):
                case (11, WireType.LengthDelimited):
                    type = null;
                    dataPoints = default;
                    reader.SkipField(wireType);
                    break;

                default:
                    reader.SkipField(wireType);
                    break;
            }
        }

        if (type is not MetricType metricType)
        {
            return;
        }

        if (metricType == MetricType.Histogram)
        {
            DecodeHistogramDataPoints(dataPoints, serviceName, name, unit, points);
        }
        else
        {
            DecodeNumberDataPoints(dataPoints, serviceName, name, unit, metricType, points);
        }
    }

    private static void DecodeNumberDataPoints(
        ReadOnlySpan<byte> bytes,
        string serviceName,
        string metricName,
        string unit,
        MetricType type,
        List<MetricPoint> points)
    {
        var reader = new ProtobufReader(bytes);
        while (reader.TryReadTag(out int fieldNumber, out WireType wireType))
        {
            // data_points = 1 (repeated NumberDataPoint). Gauge and Sum share this field number.
            if (fieldNumber == 1 && wireType == WireType.LengthDelimited)
            {
                points.Add(DecodeNumberDataPoint(reader.ReadLengthDelimited(), serviceName, metricName, unit, type));
            }
            else
            {
                reader.SkipField(wireType);
            }
        }
    }

    private static MetricPoint DecodeNumberDataPoint(
        ReadOnlySpan<byte> bytes,
        string serviceName,
        string metricName,
        string unit,
        MetricType type)
    {
        var attributes = new Dictionary<string, string>();
        ulong timeUnixNanos = 0;
        double value = 0;

        var reader = new ProtobufReader(bytes);
        while (reader.TryReadTag(out int fieldNumber, out WireType wireType))
        {
            switch (fieldNumber, wireType)
            {
                // attributes = 7
                case (7, WireType.LengthDelimited):
                    OtlpAttributeDecoder.AddKeyValue(attributes, reader.ReadLengthDelimited());
                    break;

                // time_unix_nano = 3
                case (3, WireType.Fixed64):
                    timeUnixNanos = reader.ReadFixed64();
                    break;

                // as_double = 4. as_double and as_int share the value oneof, so the last one seen wins.
                case (4, WireType.Fixed64):
                    value = reader.ReadDouble();
                    break;

                // as_int = 6 (sfixed64)
                case (6, WireType.Fixed64):
                    value = (double)(long)reader.ReadFixed64();
                    break;

                default:
                    reader.SkipField(wireType);
                    break;
            }
        }

        return new MetricPoint(
            serviceName,
            metricName,
            unit,
            type,
            ToDateTimeOffset(timeUnixNanos),
            value,
            attributes,
            HistogramBuckets: null);
    }

    private static void DecodeHistogramDataPoints(
        ReadOnlySpan<byte> bytes,
        string serviceName,
        string metricName,
        string unit,
        List<MetricPoint> points)
    {
        var reader = new ProtobufReader(bytes);
        while (reader.TryReadTag(out int fieldNumber, out WireType wireType))
        {
            // data_points = 1 (repeated HistogramDataPoint)
            if (fieldNumber == 1 && wireType == WireType.LengthDelimited)
            {
                points.Add(DecodeHistogramDataPoint(reader.ReadLengthDelimited(), serviceName, metricName, unit));
            }
            else
            {
                reader.SkipField(wireType);
            }
        }
    }

    private static MetricPoint DecodeHistogramDataPoint(
        ReadOnlySpan<byte> bytes,
        string serviceName,
        string metricName,
        string unit)
    {
        var attributes = new Dictionary<string, string>();
        var bucketCounts = new List<ulong>();
        var explicitBounds = new List<double>();
        ulong timeUnixNanos = 0;
        double sum = 0;

        // bucket_counts and explicit_bounds may arrive in either order, so both are collected before buckets are built.
        var reader = new ProtobufReader(bytes);
        while (reader.TryReadTag(out int fieldNumber, out WireType wireType))
        {
            switch (fieldNumber, wireType)
            {
                // attributes = 9
                case (9, WireType.LengthDelimited):
                    OtlpAttributeDecoder.AddKeyValue(attributes, reader.ReadLengthDelimited());
                    break;

                // time_unix_nano = 3
                case (3, WireType.Fixed64):
                    timeUnixNanos = reader.ReadFixed64();
                    break;

                // sum = 5
                case (5, WireType.Fixed64):
                    sum = reader.ReadDouble();
                    break;

                // bucket_counts = 6 (packed, or one fixed64 per tag)
                case (6, WireType.LengthDelimited):
                    ReadPackedFixed64(reader.ReadLengthDelimited(), bucketCounts);
                    break;

                case (6, WireType.Fixed64):
                    bucketCounts.Add(reader.ReadFixed64());
                    break;

                // explicit_bounds = 7 (packed, or one double per tag)
                case (7, WireType.LengthDelimited):
                    ReadPackedDoubles(reader.ReadLengthDelimited(), explicitBounds);
                    break;

                case (7, WireType.Fixed64):
                    explicitBounds.Add(reader.ReadDouble());
                    break;

                default:
                    reader.SkipField(wireType);
                    break;
            }
        }

        // Bucket i holds values up to explicit_bounds[i]. Buckets past the last bound, normally only the final one,
        // hold values above it and are keyed by positive infinity.
        var buckets = new Dictionary<double, long>();
        for (int i = 0; i < bucketCounts.Count; i++)
        {
            double bound = i < explicitBounds.Count ? explicitBounds[i] : double.PositiveInfinity;
            buckets[bound] = (long)bucketCounts[i];
        }

        return new MetricPoint(
            serviceName,
            metricName,
            unit,
            MetricType.Histogram,
            ToDateTimeOffset(timeUnixNanos),
            sum,
            attributes,
            buckets);
    }

    private static void ReadPackedFixed64(ReadOnlySpan<byte> bytes, List<ulong> values)
    {
        var reader = new ProtobufReader(bytes);
        while (!reader.IsAtEnd)
        {
            values.Add(reader.ReadFixed64());
        }
    }

    private static void ReadPackedDoubles(ReadOnlySpan<byte> bytes, List<double> values)
    {
        var reader = new ProtobufReader(bytes);
        while (!reader.IsAtEnd)
        {
            values.Add(reader.ReadDouble());
        }
    }

    private static DateTimeOffset ToDateTimeOffset(ulong unixNanos)
    {
        ulong ticks = unixNanos / 100;
        if (ticks > MaxUnixTicks)
        {
            throw new InvalidDataException($"Timestamp {unixNanos} ns is outside the supported range.");
        }

        return DateTimeOffset.UnixEpoch.AddTicks((long)ticks);
    }
}
