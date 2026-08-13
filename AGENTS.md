# PactEf

## What This Project Is

PactEf is a consumer-driven contract testing library for EF Core database schemas. It intercepts the SQL that consumer integration tests execute, saves it as a JSON snapshot, and later replays those queries against a fresh database after migrations run — via `EXPLAIN` for reads, plus boundary-value variants (max-length strings, nulls) executed inside a rolled-back transaction for writes. Breaking schema changes (renamed columns, dropped tables, type changes, shrunk `varchar(n)`, tightened nullability) are caught before they ship.

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
  BrokenSampleConsumer/       Consumer used for negative (should-fail) scenarios
  BrokenSampleConsumer.Tests/ Capture tests + snapshot for the broken scenarios
```

## Key Types

### PactEf.Core

| Type | File | Purpose |
|---|---|---|
| `QueryEntry` | `Models/QueryEntry.cs` | Single captured SQL query + `Parameters` (v2 metadata) + legacy `ParameterTypes` + execution count |
| `ParameterMetadata` | `Models/ParameterMetadata.cs` | Per-parameter DB facets: `Name`, `ClrType`, `DbType`, `StoreType`, `MaxLength`, `Precision`, `Scale`, `IsNullable`, `Size` (all nullable = unknown) |
| `SnapshotFile` | `Models/SnapshotFile.cs` | Full snapshot: consumer name, schema version, list of `QueryEntry` |
| `SnapshotSerializer` | `Serialization/SnapshotSerializer.cs` | Read/write snapshot JSON; writes `schemaVersion` `2.0`, projects legacy `parameterTypes` into `parameters` on read |

### PactEf.Capture

| Type | File | Purpose |
|---|---|---|
| `PactEfCaptureInterceptor` | `PactEfCaptureInterceptor.cs` | `DbCommandInterceptor` that buffers SQL; `FlushAsync` / `FlushMergedAsync` |
| `CaptureOptions` | `CaptureOptions.cs` | `ConsumerName` (required), `DisableEnvVariable` |
| `DbContextOptionsBuilderExtensions` | `DbContextOptionsBuilderExtensions.cs` | `.AddPactEfCapture(o => o.ConsumerName = "X")` |
| `PactEfAssemblyFixture` | `PactEfAssemblyFixture.cs` | xUnit assembly fixture; calls `FlushMergedAsync` at end of run |
| `EnvironmentGuard` | `EnvironmentGuard.cs` | Capture is a no-op unless `ASPNETCORE_ENVIRONMENT=Testing` |
| `ProjectRootLocator` | `ProjectRootLocator.cs` | Walks up from test output to find `.csproj`, determines snapshot path |
| `QueryBuffer` | `QueryBuffer.cs` | Thread-safe accumulator for intercepted SQL; merges `ParameterMetadata` positionally on duplicate SQL (widest `MaxLength` wins) |
| `ModelParameterMetadataResolver` | `ModelParameterMetadataResolver.cs` | Enriches provider parameter metadata with EF model facets (`MaxLength`, `Precision`, `Scale`) by text-matching SQL column references to mapped properties |
| `SchemaVersionReader` | `SchemaVersionReader.cs` | Reads latest `MigrationId` from `__EFMigrationsHistory` |

### PactEf.Verify

| Type | File | Purpose |
|---|---|---|
| `PactEfVerifier` | `PactEfVerifier.cs` | Public entry point: `VerifyAllAsync(Action<VerifyOptions>)` |
| `VerifyOptions` | `VerifyOptions.cs` | `ConnectionString`, `Provider`, `DefaultMode`, `SnapshotSources` |
| `SnapshotSource` | `SnapshotSource.cs` | `FromFolder(path)` or `FromEnvVariable(envVar)` |
| `SnapshotLoader` | `SnapshotLoader.cs` | Loads all `.json` files; env-var sources win over folder sources |
| `PostgreSqlQueryVerifier` | `Verification/PostgreSqlQueryVerifier.cs` | Runs the replay variant matrix; `EXPLAIN`/execute for reads, rolled-back real execution for writes; catches `42703`, `42P01`, `22001`, etc. |
| `ParameterSubstitutor` | `Verification/ParameterSubstitutor.cs` | Replaces `@p0`/`@__name_N` (Npgsql) and `$N` (positional) with typed literals; optional index-keyed `valueOverrides` for boundary literals |
| `BoundaryValueGenerator` | `Verification/BoundaryValueGenerator.cs` | Per parameter: exact-`MaxLength` `'AAA…'` literal and/or `null` literal; `BoundLengthSource` marks Consumer vs Database bound |
| `ReplayVariantMatrixBuilder` | `Verification/ReplayVariant.cs` | Baseline replay + one variant per generated boundary value (only that parameter overridden) |
| `SqlColumnReferenceResolver` | `Verification/SqlColumnReferenceResolver.cs` | Best-effort `@param` → (table, column) mapping from SQL text alone (no EF model) |
| `DatabaseColumnLengthResolver` | `Verification/DatabaseColumnLengthResolver.cs` | Reads `information_schema.columns.character_maximum_length` when the consumer declared no `MaxLength` |
| `VerificationMode` | `Verification/VerificationMode.cs` | `Explain` (default) or `Execute`; ignored for mutating statements |
| `FailureReport` | `FailureReport.cs` | Formats `PactEfVerificationException` message, including parameter, variant kind, tested length, and both constraint sides |

## Important Constraints & Decisions

- **Capture is a no-op outside `Testing` environment.** `EnvironmentGuard` checks `ASPNETCORE_ENVIRONMENT=Testing`. Tests that want capture must set this env var.
- **Multiple interceptors per run.** One `PactEfCaptureInterceptor` per `DbContext` instance. `PactEfAssemblyFixture.DisposeAsync` groups interceptors by `ConsumerName` and calls `FlushMergedAsync`, which merges all queries from the same consumer into one snapshot.
- **Snapshot paths must be absolute** when passed via `PACTEF_SNAPSHOT_PATHS`. The test runner's working directory is not the solution root, so relative paths do not resolve. `FromFolder` paths should also be absolute or anchored to the test runner's CWD.
- **`SnapshotLoader` skips missing directories** (no exception). This allows CI-specific `FromFolder` paths to coexist with local `FromEnvVariable` overrides.
- **`ParameterSubstitutor` handles Npgsql `@name` style AND PostgreSQL `$N` positional style.** The regex `@\w+` matches params in order of appearance, mapping them to typed literals (`0`, `''`, `'2000-01-01'`, etc.).
- **Parameter metadata resolution is best-effort and never fatal.** Both `ModelParameterMetadataResolver`
  (capture) and `SqlColumnReferenceResolver`/`DatabaseColumnLengthResolver` (verify) map parameters to columns
  by matching SQL text, because `DbCommandInterceptor` exposes only the rendered SQL and provider-level
  `DbParameter`s. Ambiguous aliases or raw SQL simply yield no facets; exceptions are swallowed.
- **`null` in `ParameterMetadata` means unknown, never "unconstrained".** See the snapshot format section.
- **Mutating replays always run in a rolled-back transaction.** Verification must not leave data behind, so
  `RunMutatingVariantAsync` rolls back in a `finally` even when the statement succeeded.
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

Current format is **v2** (`"schemaVersion": "2.0"`), written by `SnapshotSerializer`.

```json
{
  "schemaVersion": "2.0",
  "consumerName": "SampleConsumer",
  "capturedAt": "2026-05-18T16:42:30Z",
  "dbSchemaVersion": "20260514000000_InitialCreate",
  "queries": [
    {
      "sql": "INSERT INTO \"OrderItems\" (\"Description\", \"OrderId\")\nVALUES (@p0, @p1)\nRETURNING \"Id\";\n",
      "parameterTypes": ["String", "Int32"],
      "parameters": [
        {
          "name": "@p0",
          "clrType": "String",
          "dbType": "String",
          "storeType": "Varchar",
          "maxLength": 1000,
          "isNullable": true
        },
        {
          "name": "@p1",
          "clrType": "Int32",
          "dbType": "Int32",
          "storeType": "Integer",
          "isNullable": false
        }
      ],
      "executionCount": 1,
      "testName": null
    }
  ]
}
```

`parameters[]` fields map 1:1 to `ParameterMetadata`: `name`, `clrType`, `dbType`, `storeType`, `maxLength`,
`precision`, `scale`, `isNullable`, `size`. `queries[]` is sorted by `sql` (ordinal) for stable diffs.

### `null` means unknown, not unconstrained

Every `ParameterMetadata` field is nullable and omitted from JSON when null (`WhenWritingNull`). A missing
`maxLength` means *capture could not determine one* — parameter-to-column resolution is best-effort text
matching (see `ModelParameterMetadataResolver`) — **not** that the column is unbounded. Verification treats
the two differently: an absent `maxLength` is the trigger for querying the live schema instead of asserting
anything about the consumer. Likewise a missing `isNullable` suppresses the null variant rather than
implying `NOT NULL`.

### Migrating v1 → v2

- **No regeneration required.** `SnapshotSerializer.Deserialize` backfills `parameters` from the legacy
  `parameterTypes` array when `parameters` is empty, yielding one `ParameterMetadata` per entry with only
  `ClrType` set. v1 snapshots verify exactly as before: no `MaxLength`/`IsNullable` means no boundary
  variants from the consumer side (the database-discovered bound may still apply).
- **`parameterTypes` is still written** in v2 for backward compatibility with older readers; `Parameters` is
  the authoritative source. Consumers of the model must read `QueryEntry.Parameters`, never `ParameterTypes`.
- **To get full boundary coverage, re-capture.** Re-running the consumer capture tests rewrites the snapshot
  at v2 with facets populated; expect a large one-time diff (added `parameters` blocks, no SQL changes).

## Boundary-Value Replay

`EXPLAIN` only proves a query still *plans*: it never sees the values, so a migration that shrinks
`varchar(1000)` to `varchar(50)` passes plan-level verification while breaking the consumer at runtime.
Boundary replay closes that gap by replaying each query several times with deliberately extreme literals.

For every captured query `PostgreSqlQueryVerifier` builds a matrix (`ReplayVariantMatrixBuilder.Build`):

| Variant | When generated | Literal substituted |
|---|---|---|
| `baseline` | always | default type-based literals for every parameter (`0`, `''`, `'2000-01-01'`, …) |
| `boundary-max-length` | parameter has a `MaxLength` bound (consumer-declared, else database-discovered) | exactly-N `'AAA…'` string for that one parameter |
| `boundary-null` | parameter has `isNullable: true` | `null` for that one parameter |

Each variant overrides a single parameter; the rest keep baseline literals. Any variant failing with a
schema error code (`42P01`, `42703`, `42883`, `42804`, `42601`, `22001`) fails the whole query.

### How a variant decides Explain vs Execute

- **Mutating statements** (`INSERT`/`UPDATE`/`DELETE` at the start of the SQL) are **always executed for
  real**, inside a transaction that is rolled back in a `finally` — including on success — so nothing is left
  behind. Real execution is required because `22001 string_data_right_truncation` and `NOT NULL` violations
  only surface when values actually hit the column; `EXPLAIN` would report success.
- **Reads** follow `VerifyOptions.DefaultMode`: `Explain` (default) issues `EXPLAIN <sql>`, `Execute` runs the
  statement. `VerificationMode` is therefore ignored for mutating statements.

### Consumer contract vs database capability

Two different `MaxLength` sources feed the boundary bound, tracked by `BoundLengthSource`:

- **`Consumer`** — `maxLength` recorded in the snapshot. This is a *contract*: the consumer's EF model
  declares it may send strings this long, so a column narrower than that is a breaking change.
- **`Database`** — discovered from `information_schema.columns.character_maximum_length` for the target
  column, used **only** when the consumer declared no `maxLength` (see `ResolveDiscoveredMaxLengthsAsync`).
  This is a *capability* of the current schema, not a proven consumer requirement: it says "the column
  accepts N chars today", and a failure at that bound points at the schema rather than at a violated
  contract. Consumer-declared bounds always win when both exist.

`FailureReport` prints both sides verbatim (`Database constraint discovered: … / Consumer constraint: …`),
rendering a missing side as `unspecified` rather than omitting it, so the provenance of a failure is always
visible.

<!-- BEGIN BEADS INTEGRATION v:1 profile:minimal hash:970c3bf2 -->
## Beads Issue Tracker

This project uses **bd (beads)** for issue tracking. Run `bd prime` to see full workflow context and commands.

### Quick Reference

```bash
bd ready              # Find available work
bd show <id>          # View issue details
bd update <id> --claim  # Claim work
bd close <id>         # Complete work
```

### Rules

- Use `bd` for ALL task tracking — do NOT use TodoWrite, TaskCreate, or markdown TODO lists
- Run `bd prime` for detailed command reference and session close protocol
- Use `bd remember` for persistent knowledge — do NOT use MEMORY.md files

**Architecture in one line:** issues live in a local Dolt DB; sync uses `refs/dolt/data` on your git remote; `.beads/issues.jsonl` is a passive export. See https://github.com/gastownhall/beads/blob/main/docs/SYNC_CONCEPTS.md for details and anti-patterns.

## Agent Context Profiles

The managed Beads block is task-tracking guidance, not permission to override repository, user, or orchestrator instructions.

- **Conservative (default)**: Use `bd` for task tracking. Do not run git commits, git pushes, or Dolt remote sync unless explicitly asked. At handoff, report changed files, validation, and suggested next commands.
- **Minimal**: Keep tool instruction files as pointers to `bd prime`; use the same conservative git policy unless active instructions say otherwise.
- **Team-maintainer**: Only when the repository explicitly opts in, agents may close beads, run quality gates, commit, and push as part of session close. A current "do not commit" or "do not push" instruction still wins.

## Session Completion

This protocol applies when ending a Beads implementation workflow. It is subordinate to explicit user, repository, and orchestrator instructions.

1. **File issues for remaining work** - Create beads for anything that needs follow-up
2. **Run quality gates** (if code changed) - Tests, linters, builds
3. **Update issue status** - Close finished work, update in-progress items
4. **Handle git/sync by active profile**:
   ```bash
   # Conservative/minimal/default: report status and proposed commands; wait for approval.
   git status

   # Team-maintainer opt-in only, unless current instructions forbid it:
   git pull --rebase
   bd dolt push
   git push
   git status
   ```
5. **Hand off** - Summarize changes, validation, issue status, and any blocked sync/commit/push step

**Critical rules:**
- Explicit user or orchestrator instructions override this Beads block.
- Do not commit or push without clear authority from the active profile or the current user request.
- If a required sync or push is blocked, stop and report the exact command and error.
<!-- END BEADS INTEGRATION -->

<!-- BEGIN BEADS CODEX SETUP: generated by bd setup codex -->
## Beads Issue Tracker

Use Beads (`bd`) for durable task tracking in repositories that include it. Use the `beads` skill at `.agents/skills/beads/SKILL.md` (project install) or `~/.agents/skills/beads/SKILL.md` (global install) for Beads workflow guidance, then use the `bd` CLI for issue operations.

### Quick Reference

```bash
bd ready                # Find available work
bd show <id>            # View issue details
bd update <id> --claim  # Claim work
bd close <id>           # Complete work
bd prime                # Refresh Beads context
```

### Rules

- Use `bd` for all task tracking; do not create markdown TODO lists.
- Run `bd prime` when Beads context is missing or stale. Codex 0.129.0+ can load Beads context automatically through native hooks; use `/hooks` to inspect or toggle them.
- Keep persistent project memory in Beads via `bd remember`; do not create ad hoc memory files.

**Architecture in one line:** issues live in a local Dolt DB; sync uses `refs/dolt/data` on your git remote; `.beads/issues.jsonl` is a passive export. See https://github.com/gastownhall/beads/blob/main/docs/SYNC_CONCEPTS.md for details and anti-patterns.
<!-- END BEADS CODEX SETUP -->
