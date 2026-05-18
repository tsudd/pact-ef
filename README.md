# PactEf

Consumer-driven contract testing for EF Core database schemas.

PactEf intercepts the SQL that consumer integration tests actually execute, writes those queries as a JSON snapshot ("pact"), and replays them via `EXPLAIN` against a fresh database at migration time. If a migration breaks a query — renamed column, dropped table, type change — the verification test fails before the change ships.

## How It Works

```
Consumer tests run          Producer runs migrations
      |                              |
      v                              v
EF Core interceptor          Testcontainers spins up
captures SQL queries         a fresh Postgres instance
      |                              |
      v                              v
  SampleConsumer.json    -----> PactEfVerifier replays
  (snapshot / pact)             each query via EXPLAIN
                                     |
                              Pass / Fail report
```

## Packages

| Package | Purpose |
|---|---|
| `PactEf.Core` | Shared models: `QueryEntry`, `SnapshotFile`, `SnapshotSerializer` |
| `PactEf.Capture` | EF Core interceptor that records SQL during consumer tests |
| `PactEf.Verify` | Loads snapshots and verifies them against the current schema |

## Quick Start

### 1. Consumer side — capture queries

Add `PactEf.Capture` to your consumer test project and wire up the interceptor:

```csharp
// In your test fixture
var options = new DbContextOptionsBuilder<MyDbContext>()
    .UseNpgsql(connectionString)
    .AddPactEfCapture(o => o.ConsumerName = "MyConsumer")
    .Options;
```

Register the assembly fixture so snapshots are flushed after the test run:

```csharp
// AssemblyInfo.cs
[assembly: TestFramework("Xunit.Extensions.AssemblyFixture.XunitTestFramework",
                          "Xunit.Extensions.AssemblyFixture")]

// Test class
public class MyTests : IAssemblyFixture<PactEfAssemblyFixture>
```

Set the environment variable so capture activates:

```bash
export DOTNET_ENVIRONMENT=Testing
# or ASPNETCORE_ENVIRONMENT=Testing
```

Snapshots are written to `pactef-snapshots/<ConsumerName>.json` next to the test project.

### 2. Producer side — verify against current schema

Add `PactEf.Verify` to your database project and add a verification test:

```csharp
[Fact]
[Trait("Category", "PactEfVerification")]
public async Task AllConsumerSnapshots_AreCompatibleWithCurrentSchema()
{
    await PactEfVerifier.VerifyAllAsync(options =>
    {
        options.SnapshotSources =
        [
            SnapshotSource.FromFolder("/path/to/consumers/my-consumer/pactef-snapshots"),
            SnapshotSource.FromEnvVariable("PACTEF_SNAPSHOT_PATHS"), // local override
        ];
        options.ConnectionString = _container.GetConnectionString();
        options.Provider = DbProvider.PostgreSql;
        options.DefaultMode = VerificationMode.Explain;
    });
}
```

The test spins up a real database (Testcontainers), applies all migrations, then runs `EXPLAIN` for each captured query. Any column/table/type mismatch fails the test with a detailed report.

## Environment Variables

| Variable | Default | Purpose |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | — | Must equal `Testing` for capture to activate |
| `PACTEF_CAPTURE_DISABLED` | — | Set to any value to disable capture regardless of environment |
| `PACTEF_SNAPSHOT_PATHS` | — | Semicolon-separated absolute paths; overrides `FromFolder` sources for local dev |

## Snapshot Format

```json
{
  "schemaVersion": "1.0",
  "consumerName": "SampleConsumer",
  "capturedAt": "2026-05-18T16:42:30Z",
  "dbSchemaVersion": "20260514000000_InitialCreate",
  "queries": [
    {
      "sql": "SELECT o.\"Id\", o.\"CreatedAt\", o.\"Status\"\nFROM \"Orders\" AS o\nWHERE o.\"Id\" = @__id_0\nLIMIT 1",
      "parameterTypes": ["Int32"],
      "executionCount": 1
    }
  ]
}
```

## Verification Modes

| Mode | Behaviour |
|---|---|
| `Explain` | Runs `EXPLAIN <sql>` — catches schema errors without touching data. Default. |
| `Execute` | Runs the query with substituted literal values — catches runtime errors too. |

## Repository Layout

```
src/
  PactEf.Core/              Models and serialization
  PactEf.Core.Tests/
  PactEf.Capture/           EF Core interceptor, xUnit fixtures
  PactEf.Capture.Tests/
  PactEf.Verify/            Snapshot loader, verifier, failure report
  PactEf.Verify.Tests/
samples/
  SampleDb/                 Entity model, migrations, SchemaVerificationTests
  SampleConsumer/           OrderRepository using SampleDb
  SampleConsumer.Tests/     Integration tests that capture SQL
```

## Running Tests

```bash
# Unit tests (fast, no Docker required)
dotnet test src/PactEf.Core.Tests/PactEf.Core.Tests.csproj
dotnet test src/PactEf.Capture.Tests/PactEf.Capture.Tests.csproj
dotnet test src/PactEf.Verify.Tests/PactEf.Verify.Tests.csproj

# Consumer integration tests (requires Docker — captures snapshot)
dotnet test samples/SampleConsumer.Tests/SampleConsumer.Tests.csproj

# Schema verification (requires Docker — verifies snapshot against schema)
export PACTEF_SNAPSHOT_PATHS=/absolute/path/to/samples/SampleConsumer.Tests/pactef-snapshots
dotnet test samples/SampleDb/SampleDb.csproj --filter "Category=PactEfVerification"
```

## Stack

- .NET 10, C# 13
- EF Core 9 (interceptors)
- Npgsql / PostgreSQL
- xUnit v2 + `Xunit.Extensions.AssemblyFixture`
- Testcontainers.PostgreSql (samples only)
- System.Text.Json
