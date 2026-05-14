# PactEf Design Spec
_Date: 2026-05-14_

## Problem Statement

A shared EF Core DB nuget (containing a `DbContext` and Postgres migrations) is consumed by multiple services. When a migration is introduced, there is no automated way to verify that existing EF Core LINQ queries used by consumers still work against the updated schema. A breaking migration can only be discovered after consumers pull the new nuget version and their own tests fail.

PactEf solves this by capturing SQL snapshots from consumer integration tests and replaying them against an updated schema in the DB nuget's CI pipeline — blocking migration merges that would break any consumer.

---

## Architecture Overview

The system has two NuGet packages and a shared JSON snapshot format that acts as the contract between them.

```
Consumer Service Repo                  DB Nuget Repo
─────────────────────                  ─────────────
Integration Tests                      CI Validation Tests
  + PactEf.Capture          ────────►    + PactEf.Verify
  intercepts EF Core SQL                 reads snapshot files
  writes pactef-snapshot.json            spins up Postgres (caller)
  committed to main branch               applies migrations (caller)
                                         runs EXPLAIN per query
                                         fails CI if broken
```

**`PactEf.Capture`** — lightweight, referenced only by consumer test projects. Zero verification weight.

**`PactEf.Verify`** — referenced by the DB nuget's CI test project. Orchestrates validation against a caller-provided database.

**`pactef-snapshot.json`** — the contract file. Committed to each consumer repo. Main branch is the source of truth.

---

## Snapshot Format

```json
{
  "schemaVersion": "1.0",
  "consumerName": "OrderService",
  "capturedAt": "2026-05-14T10:23:00Z",
  "dbSchemaVersion": "20260512183045",
  "tests": [
    {
      "testName": "OrderRepository_GetById_ReturnsOrder",
      "testClass": "OrderService.Tests.OrderRepositoryTests",
      "queries": [
        {
          "sql": "SELECT o.\"Id\", o.\"CustomerId\", o.\"Status\"\nFROM \"Orders\" AS o\nWHERE o.\"Id\" = $1\nLIMIT 1",
          "parameterTypes": ["integer"],
          "executionCount": 1
        }
      ]
    }
  ]
}
```

**Key decisions:**
- `dbSchemaVersion` — timestamp prefix of the latest row in `__EFMigrationsHistory`, read at snapshot write time. Used in failure reports to identify which migration introduced a break.
- Parameter values are stripped; only DB types are kept. Sufficient for `EXPLAIN` without leaking test data.
- Queries are deduplicated per test. Same SQL appearing N times → stored once with `executionCount: N`.
- File is deterministically ordered (tests by class+name, queries by SQL text) for clean git diffs.
- Queries with no resolvable test name go into an `"__unattributed__"` bucket — not silently dropped.

---

## PactEf.Capture

### Registration

```csharp
services.AddPactEfCapture(options =>
{
    options.ConsumerName = "OrderService";
    options.OutputPath = "pactef-snapshot.json"; // relative to test output dir
    options.DisableEnvVariable = "PACTEF_CAPTURE_DISABLED"; // optional, this is the default
});
```

### Disable via Environment Variable

If the env variable named by `DisableEnvVariable` is set to `true`, `1`, or `yes`, `AddPactEfCapture` is a complete no-op — no interceptor registered, no file written, no `__EFMigrationsHistory` query. Default variable name: `PACTEF_CAPTURE_DISABLED`.

```bash
PACTEF_CAPTURE_DISABLED=true dotnet test  # capture skipped entirely
```

### Interception

Registers an `IDbCommandInterceptor` into the EF Core pipeline. Hooks `ReaderExecutingAsync` and `NonQueryExecutingAsync`. For each command:
- Strips parameter values, records parameter DB types
- Resolves the current test name (see below)
- Buffers the entry in-memory, thread-safe

### Test Name Resolution

| Framework | Strategy |
|-----------|----------|
| xUnit     | Package provides `[UsePactEfCapture]` attribute (extends `BeforeAfterTestAttribute`) that automatically sets/clears `PactEfTestContext.Current`. Consumer decorates their test class — no manual wiring required. |
| NUnit     | `TestContext.CurrentContext.Test.FullName` |
| MSTest    | `TestContext.TestName` |
| Fallback  | `"__unattributed__"` bucket |

### Snapshot Writing

Triggered once at the end of the full test run (xUnit: assembly fixture `IAsyncLifetime`; NUnit: `[SetUpFixture]`):
1. Reads `dbSchemaVersion` from `__EFMigrationsHistory` (`SELECT "MigrationId" ORDER BY "MigrationId" DESC LIMIT 1`)
2. Deduplicates queries per test, sorts deterministically
3. Writes JSON to `OutputPath`

---

## PactEf.Verify

### Registration

```csharp
await PactEfVerifier.VerifyAllAsync(options =>
{
    options.SnapshotSources = [
        SnapshotSource.FromFolder("/ci/consumers/order-service"),
        SnapshotSource.FromFolder("../order-service/tests"), // local monorepo
    ];
    options.ConnectionString = "<already migrated db connection string>";
    options.Provider = DbProvider.PostgreSql; // default
});
```

The caller is fully responsible for provisioning and migrating the database before calling `VerifyAllAsync`. PactEf.Verify only receives a connection string.

### Provider Strategy

```csharp
public interface IQueryVerifier
{
    Task<VerificationResult> VerifyAsync(
        string sql,
        IReadOnlyList<QueryParameter> parameters,
        VerificationMode mode);
}
```

`PostgreSqlQueryVerifier` is the only implementation in v1. Registered internally based on `options.Provider`. Future providers implement the same interface with no changes to the orchestration layer.

### Verification Modes

| Mode | Behavior |
|------|----------|
| `Explain` (default) | Runs `EXPLAIN <sql>` with safe typed literal substitutions. No data needed. |
| `FullExecution` | Runs the query fully. Caller must ensure test data exists if needed. |

### Parameter Substitution for EXPLAIN

Safe literals per type used when running `EXPLAIN`:

| DB Type | Substituted Literal |
|---------|-------------------|
| integer, bigint, smallint | `0` |
| text, varchar, char | `''` |
| boolean | `false` |
| uuid | `'00000000-0000-0000-0000-000000000000'` |
| timestamp, date | `'2000-01-01'` |
| numeric, decimal | `0.0` |

### Snapshot Discovery

Scans all configured `SnapshotSources` folders recursively for `pactef-snapshot.json` files. Multiple consumers' snapshots can coexist in the same folder. Each file's `consumerName` identifies its owner.

### Failure Reporting

```
FAILED  OrderService
  OrderRepository_GetById_ReturnsOrder
    SELECT o."Id", o."CustomerId" ...
    ERROR: column o."CustomerId" does not exist (42703)
    Schema captured against: 20260512183045 → current: 20260514091200

FAILED  InventoryService
  ...
```

`VerifyAllAsync` throws an aggregated exception listing all failures after checking every query — so a single xUnit/NUnit test method covers all consumers and reports all failures at once.

### Caught Postgres Error Codes

| Code | Meaning |
|------|---------|
| `42P01` | Undefined table |
| `42703` | Undefined column |
| `42883` | Undefined function |
| `42804` | Type mismatch |

---

## CI/CD Wiring

### Consumer Service Pipeline

Snapshot is a committed artifact on main. PRs that change queries must include the regenerated snapshot:

```yaml
- name: Run integration tests
  run: dotnet test
  # PACTEF_CAPTURE_DISABLED not set → capture runs

- name: Fail if snapshot is outdated
  run: git diff --exit-code **/pactef-snapshot.json
  # forces updated snapshot to be included in the PR
```

### DB Nuget Migration PR Pipeline

Checks out main of each consumer repo and reads snapshots directly:

```yaml
- name: Checkout OrderService main
  uses: actions/checkout@v4
  with:
    repository: org/order-service
    ref: main
    path: consumers/order-service

- name: Checkout InventoryService main
  uses: actions/checkout@v4
  with:
    repository: org/inventory-service
    ref: main
    path: consumers/inventory-service

- name: Run schema validation
  run: dotnet test --filter Category=PactEfVerification
```

**The invariant:** main branch of every consumer always has an up-to-date snapshot. The DB nuget's validation job is a required status check on migration PRs — a failing validation blocks the merge.

### Local Monorepo Usage

```csharp
options.SnapshotSources = [
    SnapshotSource.FromFolder("../order-service/tests"),
    SnapshotSource.FromFolder("../inventory-service/tests"),
];
```

No CI machinery needed for local runs.

---

## Sample Projects

Two non-packable projects for development and manual testing.

### Solution Structure

```
PactEf.sln
├── src/
│   ├── PactEf.Capture/
│   └── PactEf.Verify/
└── samples/
    ├── SampleDb/                          # acts as the "DB nuget"
    │   ├── Entities/
    │   │   ├── Order.cs
    │   │   └── OrderItem.cs
    │   ├── Migrations/
    │   ├── SampleDbContext.cs
    │   └── SampleDb.csproj               # <IsPackable>false</IsPackable>
    │
    └── SampleConsumer/
        ├── Repositories/
        │   └── OrderRepository.cs
        ├── SampleConsumer.Tests/
        │   ├── OrderRepositoryTests.cs
        │   ├── pactef-snapshot.json       # committed snapshot
        │   └── SampleConsumer.Tests.csproj
        └── SampleConsumer.csproj         # <IsPackable>false</IsPackable>
```

### Entities

**`Order`** — `Id (int)`, `Status (string)`, `CreatedAt (DateTimeOffset)`

**`OrderItem`** — `Id (int)`, `OrderId (int, FK)`, `ProductName (string)`, `Quantity (int)`

One-to-many relationship. Sufficient to generate: single fetch by id, filtered list, include/join query.

### SampleDb

- References `PactEf.Verify`
- Contains a verification test that reads the local snapshot from `../SampleConsumer/SampleConsumer.Tests/pactef-snapshot.json`
- Caller (test fixture) spins up Testcontainers Postgres and applies migrations before calling `VerifyAllAsync`

### SampleConsumer.Tests

- References `PactEf.Capture` and `SampleDb`
- Contains 2-3 xUnit integration tests against `OrderRepository`
- Writes `pactef-snapshot.json` to the test output directory

---

## Out of Scope (v1)

- Non-EF-Core queries (raw SQL, Dapper)
- Providers other than PostgreSQL
- Automatic snapshot push/publish (main branch commit is the mechanism)
- Query result shape validation (column name/type checking beyond parse success)
- Snapshot diffing UI
