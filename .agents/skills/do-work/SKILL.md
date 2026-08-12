---
name: do-work
description: Ralph loop — run one phase-gated implementation cycle (explore, red, green, verify, commit) against a PRD or a GitHub issue, in the sandbox.
disable-model-invocation: true
---

Runs one **ralph loop**: a single pass through explore → red → green → verify → commit, scoped to one task. The sandbox (`Dockerfile.sandbox`) re-invokes this skill per iteration — each call must leave the repo in a committed, test-green state before it ends, so the next call starts clean.

## 0. Resolve the task

Branch on what's given:

- **PRD** (file path or pasted text) — read it in full.
- **Nothing given** — run `gh issue list` in this repo, pick the oldest open issue not already referenced by an open PR or a prior commit, and treat it as the task.
- **Issue number given** — `gh issue view <n>`.

Completion: task restated in your own words — the change, and why it's needed.

## 1. Scope

A PRD or issue may bundle more than one loop can safely land. Cut it down: keep only the slice that is one coherent, independently-testable change. For anything you drop, run `gh issue create` describing it and reference the parent issue/PRD in the body.

Completion: every deferred piece has its own filed issue; what remains is a single slice, small enough to red-green in this loop.

## 2. Explore

Read the code this slice touches — no edits yet. Use `Grep`/`Glob`/`Read`, or the `Explore` agent for a wider sweep. Check `AGENTS.md` for the affected project's key types and constraints first — it documents `PactEf.Core`/`Capture`/`Verify` already, don't re-derive it.

Completion: you can name every file the slice will touch, and which existing type/test each one extends or follows.

## 3. Red

Write the test(s) for the slice before any implementation code. Run just that test project (see **Test Commands** in `AGENTS.md`) and confirm it goes **red** — a failing assertion, not a build error.

Completion: test run shows the new test failing on missing behavior, and the project still compiles.

## 4. Green

Write the minimal implementation to turn the step-3 test green. Re-run the same test project.

Completion: the new test passes.

## 5. Verify

Run every test project **Test Commands** in `AGENTS.md` says applies to what you touched (unit tests always; Docker-backed consumer/schema tests if the slice touches capture, verify, or the sample DB).

Completion: every invoked `dotnet test` exits 0. Any pre-existing failure unrelated to this slice — stop and surface it, don't paper over it.

## 6. Commit

Match this repo's existing style — short lowercase prefix, colon, terse summary (`add:`, `upd:`, `rm:`, `fix:` — see `git log`), not full Conventional Commits. Reference the issue number if one exists. Do not push — pushing is a separate, explicit action.

Completion: `git status` clean, new commit present, message follows the prefix style above.

## Loop control

If the PRD/issue still has slices left after step 6, go back to step 1 for the next slice — same task, next cut. Stop the loop only when no scoped work remains; then report what's left as filed issues (from step 1) rather than leaving it half-done in the working tree.
