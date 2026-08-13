## Your Task (one iteration = one issue)

1. Recover context: `bd prime`, then `bd memories <keyword>` for relevant prior insights.
2. Pull work: `bd ready --json`. Select one issue:
   1. Lowest priority number first (`0` before `1`).
   2. If tied, prefer bugs over features/tasks/chores.
   3. If still tied, oldest created issue first.
   State the chosen issue ID explicitly.
3. Claim and inspect:
   ```bash
   bd update ISSUE_ID --claim
   bd show ISSUE_ID --json
   bd dep tree ISSUE_ID
   ```
   Summarize scope in 2-4 bullets. If required blockers are unresolved, do not code — pick another ready issue.
4. Branch per epic (required).
5. Implement with TDD:
   1. Explore touched modules and existing tests.
   2. Write/adjust failing test(s) for the issue behavior.
   3. Implement minimal changes to pass tests.
   4. No unrelated refactors.
6. Verify fully — run the tests/build/lint the repo already uses for every touched project. Fix regressions before closing.
7. Record learnings (see below) and update nearby `CLAUDE.md` files if you found reusable patterns.
8. Commit ALL changes: `<verb>: <performed change>`.
9. Close the issue:
   ```bash
   bd close ISSUE_ID --reason "Implemented and verified"
   ```
   If partial or blocked: keep status accurate, add a progress note via `bd update ISSUE_ID --notes="..."`, and create follow-up issue(s).

## Non-Negotiable Scope Rule

Focus only on the picked issue scope. No unrelated bug fixes, no unrelated refactors, no "nice to have" extras.

Out-of-scope findings become follow-up tickets, then you continue the picked task:

```bash
bd create "Follow-up: <short title>" -t bug -p 2 --description "<what was found, impact, suggested fix>" --deps discovered-from:ISSUE_ID --json
```

## Record Learnings

Persist knowledge in beads memory:

```bash
bd remember "Codebase pattern: <general, reusable insight>"
```

Store only **general and reusable** knowledge — patterns, gotchas, non-obvious requirements, cross-file dependencies, test/environment setup. Not story-specific implementation details or temporary debugging notes. Retrieve with `bd memories <keyword>`.

Issue-specific progress belongs on the issue itself (`bd update ISSUE_ID --notes="..."`).

## Quality Requirements

- ALL commits must pass the project's quality checks (`dotnet test`).
- Do NOT commit broken code
- Keep changes focused and minimal
- Follow existing code patterns

## Stop Condition

After closing an issue, run `bd ready` (or `bd close ISSUE_ID --suggest-next`).

If nothing is ready and `bd list --status=open` is empty, reply with:
<promise>COMPLETE</promise>

Otherwise end your response normally — another iteration picks up the next issue.

## Common Mistakes

- Starting work without claiming the issue.
- Executing a task that is not in `bd ready`.
- Expanding scope to fix unrelated bugs or add optional improvements.
- Unrelated files get commited, or multiple components are changed in one branch.
- Keeping out-of-scope findings local instead of creating follow-up tickets.
- Mixing multiple components into the same git branch.
- Closing the issue without running full relevant verification.
- Editing dependencies while executing unless scope truly changed.
