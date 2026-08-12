---
name: spec-to-beads
description: Use when turning a product spec or user request into a beads epic with child issues, priorities, and explicit blocking dependencies that drive the ready queue.
disable-model-invocation: true
argument-hint: "Provide a product spec, PRD, or user request to convert into a beads backlog."
---

# Spec to Beads

Translate requirements into a beads backlog that is executable in dependency order. Model structure (`epic` + subtasks) separately from scheduling (`blocks`).

## When to Use

- You have a feature spec, PRD, or freeform user request and need actionable beads issues.
- You need an epic with decomposed implementation tasks.
- You need `bd ready` to reflect true execution order.
- Do not use when you only need one standalone issue with no dependency planning.

## Output Contract

Produce, in this order:
1. Epic definition (title, description, priority, labels).
2. Child issue list (each with type, priority, and clear completion outcome).
3. Blocking edges (`A blocks B`) for execution constraints only.
4. Exact `bd` commands to create issues and wire dependencies.
5. Final validation commands (`bd dep tree`, `bd blocked`, `bd ready`).

## Dependency Rules

| Relationship | Use for | Affects `bd ready` |
|---|---|---|
| `blocks` | Hard sequencing constraint | Yes |
| `parent-child` (`--parent`) | Epic hierarchy and grouping | No |
| `discovered-from` | Provenance of newly found work | No |
| `related` | Soft association | No |

Rule: use `blocks` only when starting target work truly requires source completion.

## Implementation Steps

1. Parse the input spec into workstreams (API, data, UI, tests, rollout, docs).
2. Create one `epic` for the overall capability.
3. Create child issues under the epic (`feature`, `task`, `bug`, `chore` as appropriate).
4. Add `blocks` edges for true prerequisites.
5. Keep non-scheduling context as parent-child/related/discovered-from, not blocks.
6. Validate queue behavior with `bd ready` and dependency shape with `bd dep tree`.

## Command Pattern

```bash
# 1) Create epic
bd create "Authentication System" -t epic -p 1 --description "JWT auth with login, refresh, logout" --json

# 2) Create children under epic (replace EPIC_ID)
bd create "Design auth data model" -t task -p 1 --parent EPIC_ID --json
bd create "Implement token issuance API" -t feature -p 1 --parent EPIC_ID --json
bd create "Add auth middleware" -t feature -p 1 --parent EPIC_ID --json
bd create "Integration tests for auth flows" -t task -p 1 --parent EPIC_ID --json

# 3) Add blocking dependencies (replace IDs)
# API implementation depends on data model
bd dep add API_ID MODEL_ID
# Middleware depends on API
bd dep add MIDDLEWARE_ID API_ID
# Tests depend on API + middleware
bd dep add TESTS_ID API_ID
bd dep add TESTS_ID MIDDLEWARE_ID

# 4) Validate
bd dep tree EPIC_ID
bd blocked
bd ready
```

## Common Mistakes

- Using `blocks` for organizational relationships (over-constrains queue).
- Missing critical prerequisites (work appears ready too early).
- Creating tasks too broad to complete in one focused change.
- Omitting verification commands after dependency wiring.