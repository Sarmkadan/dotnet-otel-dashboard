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
