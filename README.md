# dotnet-otel-dashboard

[![CI](https://github.com/Sarmkadan/dotnet-otel-dashboard/actions/workflows/ci.yml/badge.svg)](https://github.com/Sarmkadan/dotnet-otel-dashboard/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/DotnetOtelDashboard.svg)](https://www.nuget.org/packages/DotnetOtelDashboard)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

Self-hosted OpenTelemetry dashboard for any .NET app - traces, metrics, logs without Aspire dependency.

Drop it into an existing ASP.NET Core host (or run it standalone), point your OTLP exporters at it, and get a
searchable trace waterfall, metric charts and correlated logs on a single page. No Aspire AppHost, no Docker
Compose stack, no external database required for the default in-memory store.

## Features

- OTLP receiver (gRPC + HTTP/protobuf) for traces, metrics and logs
- Pluggable storage: in-memory ring buffer by default, `ITelemetryStore` for SQLite/PostgreSQL/anything
- Trace explorer: search by service, span name, attribute, duration; waterfall view with span details
- Metrics: counters, gauges, histograms with time-range aggregation
- Logs correlated to traces via `TraceId`/`SpanId`
- Embeddable: `app.MapOtelDashboard("/otel")` in any ASP.NET Core app
- Single self-contained NuGet package, no Aspire, no Node build step for consumers
- Retention and size limits per signal so the dashboard cannot exhaust host memory

## Quick start

```csharp
using DotnetOtelDashboard;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOtelDashboard(options =>
{
    options.Retention = TimeSpan.FromHours(2);
    options.MaxSpans = 200_000;
});

var app = builder.Build();

// OTLP receiver at /v1/traces, /v1/metrics, /v1/logs + UI at /otel
app.MapOtelDashboard("/otel");

app.Run();
```

Configure any OpenTelemetry SDK (including the app hosting the dashboard) to export to the app's own URL:

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddAspNetCoreInstrumentation()
                       .AddOtlpExporter(o => o.Endpoint = new Uri("http://localhost:5000")));
```

## Installation

```bash
dotnet add package DotnetOtelDashboard
```

Requires .NET 9.0 or later.

## Storage and queries

Telemetry is held by an `ITelemetryStore`. `AddOtelDashboard` registers an `InMemoryTelemetryStore` by default; you
only need the examples below when you construct a store yourself (tests, custom hosts) or query one directly.

### In-memory store

```csharp
using DotnetOtelDashboard;

var options = new OtelDashboardOptions
{
    Retention = TimeSpan.FromMinutes(30),
    MaxSpans = 50_000,
    MaxMetricPoints = 200_000,
    MaxLogRecords = 50_000,
};

// The optional TimeProvider drives retention eviction; it defaults to TimeProvider.System.
var store = new InMemoryTelemetryStore(options, timeProvider);
```

The constructor calls `OtelDashboardOptions.Validate()` and throws on out-of-range values. To use a custom store in
the dashboard, pass it to `OtelDashboardBuilder.UseStore`.

### Query methods

Span queries filter on `StartTime`; metric and log queries filter on `Timestamp`.

| Method | Semantics |
|--------|-----------|
| `WriteSpansAsync(spans)` | Appends a batch of spans; the oldest are overwritten once `MaxSpans` is reached. |
| `WriteMetricsAsync(points)` | Appends metric points, bounded by `MaxMetricPoints`. |
| `WriteLogsAsync(records)` | Appends log records, bounded by `MaxLogRecords`. |
| `QuerySpansAsync(query)` | Matching spans, newest first (ties broken by `SpanId`), at most `Limit` (default 100). `From` inclusive, `To` exclusive. |
| `GetTraceAsync(traceId)` | All spans of one trace, oldest first; an empty list when the trace is unknown. |
| `QueryMetricsAsync(query)` | Points for one `MetricName`, optionally one `ServiceName`, oldest first. No `Limit`. `From` inclusive, `To` exclusive. |
| `QueryLogsAsync(query)` | Matching log records, newest first, at most `Limit` (default 100). `From` inclusive, `To` exclusive. `Search` is a case-insensitive substring match on `Body`. |
| `GetServiceNamesAsync()` | Distinct service names seen in spans, metrics and logs, sorted ordinally. |
| `ClearAsync()` | Removes all spans, metric points and log records. |

Examples:

```csharp
// Up to 50 error spans from the checkout service, newest first
var errors = await store.QuerySpansAsync(
    new SpanQuery(ServiceName: "checkout", ErrorsOnly: true, Limit: 50));

// Every span in one trace (traceId is a 32-character lowercase hex string), oldest first
var spans = await store.GetTraceAsync(traceId);

// Request duration points for the last 15 minutes, across all services
var from = DateTimeOffset.UtcNow.AddMinutes(-15);
var to = DateTimeOffset.UtcNow;
var points = await store.QueryMetricsAsync(
    new MetricQuery("http.server.request.duration", null, from, to));

// Log records correlated to one trace at severity 13 (WARN) or higher
var logs = await store.QueryLogsAsync(new LogQuery(TraceId: traceId, MinSeverity: 13));
```

### Eviction

Count limits (`MaxSpans`, `MaxMetricPoints`, `MaxLogRecords`) are enforced by one ring buffer per signal: when a
buffer is full, each new item overwrites the oldest one. `Retention` is time-based: records whose timestamp is older
than now minus `Retention` are dropped. There is no background timer. Both kinds of eviction run at the start of
every write and every read (`WriteSpansAsync`, `QuerySpansAsync`, `GetTraceAsync`, `QueryMetricsAsync`,
`QueryLogsAsync`, `GetServiceNamesAsync`), so a read never returns data older than `Retention`.

### Implementing your own store

`ITelemetryStore` is the only persistence abstraction, so a SQLite or PostgreSQL store implements the same methods.
The interface contract:

- Thread-safe: the receiver writes and the query endpoints read concurrently, so every method must be safe to call
  from multiple threads.
- Bounded: memory and storage must honor `OtelDashboardOptions` (`Retention`, `MaxSpans`, `MaxMetricPoints`,
  `MaxLogRecords`).
- Query semantics match the table above: newest first for spans and logs, `Limit` respected, `From` inclusive and
  `To` exclusive.
- Return empty lists rather than `null`. `GetTraceAsync` returns an empty list for an unknown trace.

## Ingest and composition

### Composing the dashboard

`OtelDashboardBuilder` is the framework-agnostic composition root. It collects options, an optional store factory and
an optional receiver factory, then validates and wires them together:

```csharp
using DotnetOtelDashboard;

OtelDashboard dashboard = new OtelDashboardBuilder()
    .Configure(o =>
    {
        o.Retention = TimeSpan.FromMinutes(30);
        o.MaxSpans = 50_000;
    })
    .UseStore(o => new MyStore(o)) // MyStore implements ITelemetryStore; omit UseStore for InMemoryTelemetryStore
    .Build();

ITelemetryStore store = dashboard.Store;
ITelemetryReceiver receiver = dashboard.Receiver;
OtelDashboardOptions options = dashboard.Options;
```

- `Configure` can be called several times; later calls win.
- `UseStore` replaces the default `InMemoryTelemetryStore`. The factory receives the options, which are already
  validated when the factory runs in `Build`.
- `UseReceiver` replaces the default `OtlpTelemetryReceiver`. Its factory receives the options and the configured store.
- `Build` validates the options (invalid values throw `ArgumentOutOfRangeException`) and throws
  `InvalidOperationException` if a factory returns `null`.
- `Build` returns an `OtelDashboard` record with three members: `Options`, `Store` and `Receiver`. The receiver
  writes into the same store that the dashboard exposes.

### ITelemetryReceiver

`ITelemetryReceiver` accepts raw OTLP/protobuf request bodies, one method per signal:

| Method | OTLP request message | Returns |
|--------|----------------------|---------|
| `ReceiveTracesAsync(ReadOnlyMemory<byte> payload, CancellationToken)` | `ExportTraceServiceRequest` | Number of spans accepted |
| `ReceiveMetricsAsync(ReadOnlyMemory<byte> payload, CancellationToken)` | `ExportMetricsServiceRequest` | Number of data points accepted |
| `ReceiveLogsAsync(ReadOnlyMemory<byte> payload, CancellationToken)` | `ExportLogsServiceRequest` | Number of log records accepted |

Each method decodes the payload, writes the records into the store and returns the count. A request with no records
returns `0` and writes nothing. A malformed payload throws `InvalidDataException`; a payload larger than
`OtlpTelemetryReceiver.MaxPayloadBytes` (16 MiB) throws `ArgumentOutOfRangeException` before any decoding happens.

The receiver does not know about HTTP. The minimal API below shows how a host passes request bodies to it. This is
what the upcoming `MapOtelDashboard` will do for you, mapping these three routes automatically:

```csharp
using DotnetOtelDashboard;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var dashboard = new OtelDashboardBuilder().Build();
var receiver = dashboard.Receiver;

app.MapPost("/v1/traces", async (HttpContext http, CancellationToken ct) =>
{
    var body = await ReadBodyAsync(http, ct);
    return Results.Ok(new { accepted = await receiver.ReceiveTracesAsync(body, ct) });
});

app.MapPost("/v1/metrics", async (HttpContext http, CancellationToken ct) =>
{
    var body = await ReadBodyAsync(http, ct);
    return Results.Ok(new { accepted = await receiver.ReceiveMetricsAsync(body, ct) });
});

app.MapPost("/v1/logs", async (HttpContext http, CancellationToken ct) =>
{
    var body = await ReadBodyAsync(http, ct);
    return Results.Ok(new { accepted = await receiver.ReceiveLogsAsync(body, ct) });
});

app.Run();

static async Task<byte[]> ReadBodyAsync(HttpContext http, CancellationToken ct)
{
    using var buffer = new MemoryStream();
    await http.Request.Body.CopyToAsync(buffer, ct);
    return buffer.ToArray();
}
```

### Zero dependencies

The OTLP protobuf payload is decoded with a built-in wire-format reader (`ProtobufReader`), so the package does not
reference Google.Protobuf, OpenTelemetry.Proto or Aspire. The reader is internal, and no protobuf types appear in the
public API.

### Waterfall

`TraceWaterfall.Build` lays out the spans of one trace as depth-first rows for the trace view:

```csharp
var rows = TraceWaterfall.Build(await store.GetTraceAsync(traceId));

foreach (var row in rows)
{
    Console.WriteLine($"{new string(' ', row.Depth * 2)}{row.Span.SpanId} @ {row.OffsetPercent:F1}% width {row.WidthPercent:F1}%");
}
```

Each `WaterfallRow` has these fields:

- `Span`: the `SpanRecord` this row represents.
- `Depth`: nesting depth in the span tree; root spans are at depth 0.
- `Offset`: start of the span relative to the start of the trace, as a `TimeSpan`.
- `OffsetPercent`: `Offset` as a percentage of the total trace duration.
- `WidthPercent`: span duration as a percentage of the total trace duration, at least 0.1 so zero-length spans stay visible.

Spans whose parent is missing from the trace become roots. Rows come back in pre-order, so every span appears once.

## API overview

| Type | Purpose |
|------|---------|
| `OtelDashboardOptions` | Retention, size limits, UI path, auth hooks |
| `ITelemetryStore` | Storage abstraction: write/query spans, metrics, log records |
| `InMemoryTelemetryStore` | Default bounded in-memory implementation |
| `ITelemetryReceiver` | Ingest OTLP payloads and forward to the store |
| `OtelDashboardServiceCollectionExtensions.AddOtelDashboard` | DI registration |
| `OtelDashboardEndpointRouteBuilderExtensions.MapOtelDashboard` | Maps receiver endpoints and UI |

See XML docs on each type for details. Everything public lives in the `DotnetOtelDashboard` namespace.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md).

## License

[MIT](LICENSE) - Copyright 2026 Vladyslav Zaiets
