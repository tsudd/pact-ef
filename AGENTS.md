# PactEf

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
