# PactEf — Agent Context

This file provides context for AI agents and automated tooling working on this repository.

## What This Project Is

PactEf is a consumer-driven contract testing library for EF Core database schemas. It intercepts the SQL that consumer integration tests execute, saves it as a JSON snapshot, and later replays those queries via `EXPLAIN` against a fresh database after migrations run. Breaking schema changes (renamed columns, dropped tables, type changes) are caught before they ship.

## Repository Layout

```markdown
PactEf.sln
src/
  PactEf.Core/             QueryEntry, SnapshotFile, SnapshotSerializer
  PactEf.Core.Tests/
  PactEf.Capture/          Interceptor, fixtures, DI extensions
  PactEf.Capture.Tests/
  PactEf.Verify/           Loader, verifier, failure report
  PactEf.Verify.Tests/
samples/
  SampleDb/                EF Core model + migrations + SchemaVerificationTests
  SampleConsumer/          OrderRepository
  SampleConsumer.Tests/    Capture integration tests + snapshot JSON
```

## Key Types

### PactEf.Core

| Type | File | Purpose |
|---|---|---|
| `QueryEntry` | `Models/QueryEntry.cs` | Single captured SQL query + parameter types + execution count |
| `SnapshotFile` | `Models/SnapshotFile.cs` | Full snapshot: consumer name, schema version, list of `QueryEntry` |
| `SnapshotSerializer` | `Serialization/SnapshotSerializer.cs` | Read/write snapshot JSON |

### PactEf.Capture

| Type | File | Purpose |
|---|---|---|
| `PactEfCaptureInterceptor` | `PactEfCaptureInterceptor.cs` | `DbCommandInterceptor` that buffers SQL; `FlushAsync` / `FlushMergedAsync` |
| `CaptureOptions` | `CaptureOptions.cs` | `ConsumerName` (required), `DisableEnvVariable` |
| `DbContextOptionsBuilderExtensions` | `DbContextOptionsBuilderExtensions.cs` | `.AddPactEfCapture(o => o.ConsumerName = "X")` |
| `PactEfAssemblyFixture` | `PactEfAssemblyFixture.cs` | xUnit assembly fixture; calls `FlushMergedAsync` at end of run |
| `EnvironmentGuard` | `EnvironmentGuard.cs` | Capture is a no-op unless `ASPNETCORE_ENVIRONMENT=Testing` |
| `ProjectRootLocator` | `ProjectRootLocator.cs` | Walks up from test output to find `.csproj`, determines snapshot path |
| `QueryBuffer` | `QueryBuffer.cs` | Thread-safe accumulator for intercepted SQL |
| `SchemaVersionReader` | `SchemaVersionReader.cs` | Reads latest `MigrationId` from `__EFMigrationsHistory` |

### PactEf.Verify

| Type | File | Purpose |
|---|---|---|
| `PactEfVerifier` | `PactEfVerifier.cs` | Public entry point: `VerifyAllAsync(Action<VerifyOptions>)` |
| `VerifyOptions` | `VerifyOptions.cs` | `ConnectionString`, `Provider`, `DefaultMode`, `SnapshotSources` |
| `SnapshotSource` | `SnapshotSource.cs` | `FromFolder(path)` or `FromEnvVariable(envVar)` |
| `SnapshotLoader` | `SnapshotLoader.cs` | Loads all `.json` files; env-var sources win over folder sources |
| `PostgreSqlQueryVerifier` | `Verification/PostgreSqlQueryVerifier.cs` | Issues `EXPLAIN <sql>` or executes directly; catches `42703`, `42P01`, etc. |
| `ParameterSubstitutor` | `Verification/ParameterSubstitutor.cs` | Replaces `@p0`/`@__name_N` (Npgsql) and `$N` (positional) with typed literals |
| `VerificationMode` | `Verification/VerificationMode.cs` | `Explain` (default) or `Execute` |
| `FailureReport` | `FailureReport.cs` | Formats `PactEfVerificationException` message |

## Important Constraints & Decisions

- **Capture is a no-op outside `Testing` environment.** `EnvironmentGuard` checks `ASPNETCORE_ENVIRONMENT=Testing`. Tests that want capture must set this env var.
- **Multiple interceptors per run.** One `PactEfCaptureInterceptor` per `DbContext` instance. `PactEfAssemblyFixture.DisposeAsync` groups interceptors by `ConsumerName` and calls `FlushMergedAsync`, which merges all queries from the same consumer into one snapshot.
- **Snapshot paths must be absolute** when passed via `PACTEF_SNAPSHOT_PATHS`. The test runner's working directory is not the solution root, so relative paths do not resolve. `FromFolder` paths should also be absolute or anchored to the test runner's CWD.
- **`SnapshotLoader` skips missing directories** (no exception). This allows CI-specific `FromFolder` paths to coexist with local `FromEnvVariable` overrides.
- **`ParameterSubstitutor` handles Npgsql `@name` style AND PostgreSQL `$N` positional style.** The regex `@\w+` matches params in order of appearance, mapping them to typed literals (`0`, `''`, `'2000-01-01'`, etc.).
- **InternalsVisibleTo** is used so test projects can access `internal` types. Check `*.csproj` files for `[assembly: InternalsVisibleTo(...)]` attributes.
- **xUnit assembly fixture registration** requires two things: (1) `[assembly: TestFramework("Xunit.Extensions.AssemblyFixture.XunitTestFramework", "Xunit.Extensions.AssemblyFixture")]` in `AssemblyInfo.cs`, and (2) `IAssemblyFixture<PactEfAssemblyFixture>` on the test class.

## Test Commands

```bash
# Unit tests (no Docker)
dotnet test src/PactEf.Core.Tests/PactEf.Core.Tests.csproj
dotnet test src/PactEf.Capture.Tests/PactEf.Capture.Tests.csproj
dotnet test src/PactEf.Verify.Tests/PactEf.Verify.Tests.csproj

# Consumer integration tests (Docker required; writes snapshot)
dotnet test samples/SampleConsumer.Tests/SampleConsumer.Tests.csproj

# Schema verification (Docker required; reads snapshot)
export PACTEF_SNAPSHOT_PATHS=/absolute/path/to/samples/SampleConsumer.Tests/pactef-snapshots
dotnet test samples/SampleDb/SampleDb.csproj --filter "Category=PactEfVerification"
```

## Snapshot File Format

Written to `<test-project-dir>/pactef-snapshots/<ConsumerName>.json`.

```json
{
  "schemaVersion": "1.0",
  "consumerName": "SampleConsumer",
  "capturedAt": "2026-05-18T16:42:30Z",
  "dbSchemaVersion": "20260514000000_InitialCreate",
  "queries": [
    {
      "sql": "SELECT o.\"Id\" FROM \"Orders\" AS o WHERE o.\"Id\" = @__id_0 LIMIT 1",
      "parameterTypes": ["Int32"],
      "executionCount": 1,
      "testName": null
    }
  ]
}
```
