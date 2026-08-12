@AGENTS.md

# Ralph Agent Instructions

You are an autonomous coding agent working on a software project.

Task tracking lives in **beads** (`bd`). Do NOT use `prd.json`, `progress.txt`, TodoWrite, or markdown task files.

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
4. Branch per component (required). Identify impacted component(s); each component gets its own branch, following the branch naming guidelines:
   ```bash
   git checkout -b JIRA-ID-short-description
   git push -u origin JIRA-ID-short-description
   ```
   Never combine changes for multiple components in one branch.
5. Implement with TDD:
   1. Explore touched modules and existing tests.
   2. Write/adjust failing test(s) for the issue behavior.
   3. Implement minimal changes to pass tests.
   4. No unrelated refactors.
6. Verify fully — run the tests/build/lint the repo already uses for every touched project. Fix regressions before closing.
7. Record learnings (see below) and update nearby `CLAUDE.md` files if you found reusable patterns.
8. Commit ALL changes: `[JIRA-ID] <verb>: <performed change>`.
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

Persist knowledge in beads memory instead of a progress log:

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


<!-- BEGIN BEADS INTEGRATION v:1 profile:minimal hash:6cd5cc61 -->
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
   git push
   git status
   ```
5. **Hand off** - Summarize changes, validation, issue status, and any blocked sync/commit/push step

**Critical rules:**
- Explicit user or orchestrator instructions override this Beads block.
- Do not commit or push without clear authority from the active profile or the current user request.
- If a required sync or push is blocked, stop and report the exact command and error.
<!-- END BEADS INTEGRATION -->
