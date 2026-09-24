# dotnet-otel-dashboard

Self-hosted OpenTelemetry dashboard for any .NET app (traces, metrics, logs) with no Aspire dependency.
Target framework: net9.0. Single NuGet package `DotnetOtelDashboard`.

## Build

```bash
dotnet restore
dotnet build -c Release
dotnet pack src/dotnet-otel-dashboard -c Release -o ./artifacts
```

`TreatWarningsAsErrors` is on via `Directory.Build.props`. A warning fails the build; fix it, do not suppress it
globally.

## Test

```bash
dotnet test                                   # all tests
dotnet test --filter "FullyQualifiedName~Store" # subset
dotnet test --collect:"XPlat Code Coverage"
```

Tests: xUnit in `tests/dotnet-otel-dashboard.Tests`. One test class per production type, named `<Type>Tests`.

## Architecture

Pipeline: OTLP payload -> `ITelemetryReceiver` -> `ITelemetryStore` -> query API -> UI.

- **Receiver** (`ITelemetryReceiver`): accepts OTLP/HTTP protobuf and gRPC payloads for traces, metrics, logs.
  Decodes into internal records and writes them to the store. Never blocks the caller on storage; batches.
- **Store** (`ITelemetryStore`): the only persistence abstraction. Default `InMemoryTelemetryStore` is a bounded
  ring buffer per signal honoring `OtelDashboardOptions.Retention` / `Max*`. Other stores implement the same
  interface. Queries are async and paged.
- **Hosting**: `AddOtelDashboard` (DI) + `MapOtelDashboard` (endpoint routing) are the only two entry points a
  consumer needs. UI assets are embedded resources served from the mapped path.
- **Options** (`OtelDashboardOptions`): validated at startup; invalid config throws early.

Design rules:
- No dependency on Aspire, Aspire.Dashboard, or any Aspire package. Ever.
- Only depend on `OpenTelemetry.Proto`-compatible types for decoding; do not leak protobuf types into the public API.
- Public API surface is intentionally small. New public types need a reason in the PR description.
- Bounded memory is a correctness requirement, not an optimization.

## Key directories

```
src/dotnet-otel-dashboard/       library (namespace DotnetOtelDashboard)
  OtelDashboardOptions.cs        configuration
  ITelemetryStore.cs             storage abstraction + record types
  InMemoryTelemetryStore.cs      default store
  ITelemetryReceiver.cs          OTLP ingest
  OtelDashboardExtensions.cs     AddOtelDashboard / MapOtelDashboard
tests/dotnet-otel-dashboard.Tests/
.github/workflows/ci.yml         build + test on push/PR
Directory.Build.props            shared compiler settings
```

## Conventions

- C# latest, nullable enabled, file-scoped namespaces, `sealed` by default.
- Public members have XML docs (`GenerateDocumentationFile` is on; missing docs are warnings, therefore errors).
- Async methods end in `Async` and take a `CancellationToken` as the last parameter.
- Prefer `readonly record struct` for telemetry record types on hot paths.
- No `Console.WriteLine`; use `ILogger<T>`.
- Commits: conventional commits (`feat:`, `fix:`, `test:`, `docs:`, `chore:`). No `Co-Authored-By` trailers.
- Stubs currently throw `NotImplementedException`; replace the throw, keep the signature unless the change is
  discussed in an issue.
