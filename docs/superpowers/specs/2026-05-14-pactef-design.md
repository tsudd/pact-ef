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

**`pactef-snapshots/<ConsumerName>.json`** — the contract file. Committed to each consumer repo. Main branch is the source of truth.

---

## Snapshot Format

```json
{
  "schemaVersion": "1.0",
  "consumerName": "OrderService",
  "capturedAt": "2026-05-14T10:23:00Z",
  "dbSchemaVersion": "20260512183045",
  "queries": [
    {
      "sql": "SELECT o.\"Id\", o.\"Status\"\nFROM \"Orders\" AS o\nWHERE o.\"Id\" = $1\nLIMIT 1",
      "parameterTypes": ["integer"],
      "executionCount": 3,
      "testName": "OrderRepository_GetById_ReturnsOrder",
      "testClass": "OrderService.Tests.OrderRepositoryTests"
    },
    {
      "sql": "SELECT o.\"Id\"\nFROM \"Orders\" AS o",
      "parameterTypes": [],
      "executionCount": 1
    }
  ]
}
```

**Key decisions:**
- `dbSchemaVersion` — timestamp prefix of the latest row in `__EFMigrationsHistory`, captured lazily on the first intercepted command. Used in failure reports to identify which migration introduced a break.
- Parameter values are stripped; only DB types are kept. Sufficient for `EXPLAIN` without leaking test data.
- Queries are deduplicated across the entire snapshot. Same SQL shape appearing N times → stored once with `executionCount: N`.
- `testName` and `testClass` are optional — present only when `[UsePactEfCapture]` was active for the test that triggered the query, absent otherwise.
- File is deterministically ordered (queries sorted by SQL text) for clean git diffs.
- Queries with no resolvable test name simply omit the test fields — they are not dropped or bucketed separately.

---

## PactEf.Capture

### Activation Guard

Capture is a **no-op unless the environment is explicitly `Testing`**. This prevents accidental snapshot writes in staging or production if the package is referenced outside test projects.

Capture activates only when one of these env variables equals `Testing` (case-insensitive):
- `ASPNETCORE_ENVIRONMENT`
- `DOTNET_ENVIRONMENT`

If neither is set to `Testing`, all registration methods (`AddPactEfCapture`, `PactEfCaptureInterceptor.Create`) silently return without registering anything.

`PACTEF_CAPTURE_DISABLED=true` explicitly disables capture even when the environment is `Testing` — useful for CI pipelines that run tests but do not want snapshot updates.

```bash
ASPNETCORE_ENVIRONMENT=Testing dotnet test        # capture active
DOTNET_ENVIRONMENT=Testing dotnet test            # capture active
ASPNETCORE_ENVIRONMENT=Development dotnet test    # capture is no-op
PACTEF_CAPTURE_DISABLED=true dotnet test          # capture disabled regardless
```

### Registration — DI path

For projects using `IServiceCollection`, register inside the `AddDbContext` callback:

```csharp
services.AddDbContext<MyDbContext>(options =>
{
    options.UseNpgsql(connectionString);
    options.AddPactEfCapture(capture =>
    {
        capture.ConsumerName = "OrderService";
    });
});
```

### Registration — Non-DI path

For projects with a custom `DbContext` factory that builds `DbContextOptions` directly:

```csharp
// Once in test setup — create a shared interceptor instance
var interceptor = PactEfCaptureInterceptor.Create(options =>
{
    options.ConsumerName = "OrderService";
});

// In the custom DbContext factory:
var options = new DbContextOptionsBuilder<MyDbContext>()
    .UseNpgsql(connectionString)
    .AddInterceptors(interceptor)
    .Options;

return new MyDbContext(options);
```

The consumer holds the interceptor reference. The assembly fixture (or teardown hook) calls `interceptor.FlushAsync()` to write the snapshot at the end of the test run.

`PactEfCaptureInterceptor.Create` returns a null-object interceptor (no-op) when the environment guard is not satisfied — so non-DI factory code needs no conditional checks.

Output path is not configurable in either registration path — it is always `<test-project-root>/pactef-snapshots/<ConsumerName>.json`. The project root is located by walking up from `AppContext.BaseDirectory` until a `.csproj` file is found.

### Interception

Registers an `IDbCommandInterceptor` into the EF Core pipeline. Hooks `ReaderExecutingAsync` and `NonQueryExecutingAsync`. Only **DML commands** are captured — `SELECT`, `INSERT`, `UPDATE`, `DELETE`. DDL statements and EF Core infrastructure queries (e.g. `__EFMigrationsHistory` lookups, `__EFMigrationsLock`) are skipped.

For each captured command:
- Strips parameter values, records parameter DB types
- Reads `PactEfTestContext.Current` (see below) — if set, attaches `testName`/`testClass` to the entry; if not set, entry has no test attribution
- Buffers the entry into `ConcurrentDictionary<string, ConcurrentBag<QueryEntry>>` keyed by SQL text

`dbSchemaVersion` is captured lazily on the first intercepted DML command: queries `__EFMigrationsHistory` for the latest `MigrationId` and caches it in memory. Subsequent commands reuse the cached value.

### Test Name Resolution (Optional)

Test attribution is opt-in. By default, no test framework wiring is needed and queries are captured without test names — **zero changes required to existing tests**.

To opt in to test-level attribution, decorate test classes with `[UsePactEfCapture]`:

```csharp
[UsePactEfCapture]
public class OrderRepositoryTests
{
    // all queries fired from these tests will include testName/testClass in the snapshot
}
```

`[UsePactEfCapture]` is a **class-level** `BeforeAfterTestAttribute` (xUnit v2). It automatically sets/clears `PactEfTestContext.Current` (backed by `AsyncLocal<string>`) before and after each test method — safe for parallel execution.

| Framework | Strategy |
|-----------|----------|
| xUnit v2  | `[UsePactEfCapture]` class-level attribute provided by the package |
| NUnit     | `TestContext.CurrentContext.Test.FullName` (automatic) |
| MSTest    | `TestContext.TestName` (automatic) |

### Snapshot Writing

**DI path** — triggered once at the end of the full test run via xUnit's assembly fixture mechanism:

```csharp
[assembly: AssemblyFixture(typeof(PactEfAssemblyFixture))]
```

`PactEfAssemblyFixture` implements `IAsyncLifetime`. On `DisposeAsync` it calls `FlushAsync()` on all registered interceptors.

**Non-DI path** — consumer calls `FlushAsync()` explicitly in their teardown:

```csharp
await interceptor.FlushAsync();
```

**Both paths — `FlushAsync` behavior:**
1. Uses the already-cached `dbSchemaVersion`
2. Deduplicates queries by SQL text, sorts deterministically by SQL text
3. Writes JSON to `<test-project-root>/pactef-snapshots/<ConsumerName>.json`

One snapshot file is written per `ConsumerName`. If multiple `DbContext` types are registered with the same `ConsumerName`, their queries are merged into a single file.

---

## PactEf.Verify

### Registration

```csharp
await PactEfVerifier.VerifyAllAsync(options =>
{
    options.SnapshotSources = [
        SnapshotSource.FromFolder("/ci/consumers/order-service"),          // CI: checked-out consumer repos
        SnapshotSource.FromEnvVariable("PACTEF_SNAPSHOT_PATHS"),           // runtime override via env var
    ];
    options.ConnectionString = "<already migrated db connection string>";
    options.Provider = DbProvider.PostgreSql; // default
});
```

The caller is fully responsible for provisioning and migrating the database before calling `VerifyAllAsync`. PactEf.Verify only receives a connection string.

### Snapshot Source Types

| Source | Behavior |
|--------|----------|
| `SnapshotSource.FromFolder(path)` | Reads from the given folder path. Error if path does not exist. |
| `SnapshotSource.FromEnvVariable(name)` | Reads the env variable at runtime, splits by `;`, treats each entry as a folder path. Silently skipped if the variable is not set. |

Sources are merged; when the same `consumerName` appears in both a `FromFolder` and a `FromEnvVariable` source, **`FromEnvVariable` always wins** regardless of declaration order.

**Local monorepo usage** — set the env variable instead of hardcoding paths:

```bash
PACTEF_SNAPSHOT_PATHS="../order-service/tests;../inventory-service/tests" dotnet test
```

This means the verifier configuration in code only lists CI paths (`FromFolder`). Local developers override with `PACTEF_SNAPSHOT_PATHS` without touching code.

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

Scans all configured `SnapshotSources` folders recursively for files matching `pactef-snapshots/*.json`. Multiple consumers' snapshots can coexist in the same folder — each file's `consumerName` field identifies its owner.

### Failure Reporting

```
FAILED  OrderService
  [OrderRepository_GetById_ReturnsOrder]   ← shown only if testName present
    SELECT o."Id", o."Status" ...
    ERROR: column o."Status" does not exist (42703)
    Schema captured against: 20260512183045 → current: 20260514091200

  SELECT o."Id" FROM "Orders" AS o         ← no test attribution
    ERROR: relation "Orders" does not exist (42P01)
    Schema captured against: 20260512183045 → current: 20260514091200
```

`VerifyAllAsync` throws an aggregated exception listing all failures after checking every query. The verifier deduplicates queries across the entire snapshot before running — a query appearing multiple times (with or without test attribution) is validated once.

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
  run: git diff --exit-code **/pactef-snapshots/*.json
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

Set `PACTEF_SNAPSHOT_PATHS` to a semicolon-separated list of local snapshot folders:

```bash
PACTEF_SNAPSHOT_PATHS="../order-service/tests;../inventory-service/tests" dotnet test
```

The verifier configuration in code only lists CI paths. Local developers override at runtime without touching code. No CI machinery needed for local runs.

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
        │   ├── pactef-snapshots/
        │   │   └── SampleConsumer.json    # committed snapshot
        │   └── SampleConsumer.Tests.csproj
        └── SampleConsumer.csproj         # <IsPackable>false</IsPackable>
```

### Entities

**`Order`** — `Id (int)`, `Status (string)`, `CreatedAt (DateTimeOffset)`

**`OrderItem`** — `Id (int)`, `OrderId (int, FK)`, `ProductName (string)`, `Quantity (int)`

One-to-many relationship. Sufficient to generate: single fetch by id, filtered list, include/join query.

### SampleDb

- References `PactEf.Verify`
- Contains a verification test that reads the local snapshot via `PACTEF_SNAPSHOT_PATHS=../SampleConsumer/SampleConsumer.Tests/pactef-snapshots` or the CI folder path
- Caller (test fixture) spins up Testcontainers Postgres and applies migrations before calling `VerifyAllAsync`

### SampleConsumer.Tests

- References `PactEf.Capture` and `SampleDb`
- Contains 2-3 xUnit integration tests against `OrderRepository`
- Writes `pactef-snapshots/SampleConsumer.json` to the test project root

---

## Out of Scope (v1)

- Non-EF-Core queries (raw SQL, Dapper)
- Providers other than PostgreSQL
- Automatic snapshot push/publish (main branch commit is the mechanism)
- Query result shape validation (column name/type checking beyond parse success)
- Snapshot diffing UI
