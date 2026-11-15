---
name: write-spec
description: Write a Shipwise feature spec in docs/specs that Codex (fullstack) and Gemini (frontend and design) can implement without guessing. Use when the user describes a feature, user story, change request or vertical slice to plan, or asks for requirements, acceptance criteria, a task breakdown or a spec. Not for architecture decisions on their own; those are ADRs in docs/adr.
---

# Write a Shipwise spec

You are the planner (BA, solution architect, tech lead). The spec is the only input the
implementing agents get, so anything left vague becomes a guess. Do not write product code.

## Before writing

1. Read `AGENTS.md`, `ARCHITECTURE.md`, existing specs in `docs/specs`, ADRs in `docs/adr`,
   and in `../Shipwise-Web`: `AGENTS.md`, `DESIGN.md` and `docs/architecture/*`.
2. If the feature needs a decision that is not recorded (identity, persistence, messaging,
   a new dependency), write or propose an ADR in `docs/adr/NNNN-slug.md` first.
3. Ask the user only for what you cannot infer and that changes scope: who the user is,
   what success looks like, and hard constraints. Record every assumption in the spec.

## File

`docs/specs/NNNN-slug.md`, numbered after the highest existing spec, with
`Status: draft | approved | done`. Only a human sets `approved`; implementers start only
from approved specs.

## Template

```markdown
# NNNN Title

Status: draft · Owner: Claude Code · Related ADRs: none

## Problem and outcome
Who has the problem, what they can do after this ships, and how we will know.

## Scope
In scope / Out of scope (explicit list).

## User stories and acceptance criteria
- As a <role>, I can <action> so that <value>.
  - Given ... when ... then ... (each criterion testable; include permissions/tenancy)

## API contract
Endpoints (method, path, request, response, status codes, error shape), auth and tenancy
rules, idempotency for writes, pagination. This becomes the OpenAPI document at
contracts/openapi/shipwise.v1.json; the frontend never invents fields beyond it.

## Data and backend design
Module and layer placement (Domain, Application, Infrastructure, API), entities and
invariants, commands vs queries, events or outbox use, observability.
Database (follow the ef-migrations skill): tables with columns, types, nullability and
max lengths; keys, unique indexes, check constraints and query indexes; organization
ownership; delete behavior; migration name(s); destructive: yes/no (yes needs an
expand/contract plan and human approval).

## UI and states
Routes, screens and components; loading, empty, error, success and permission-denied
states; responsive behavior at 375/768/1440px; accessibility notes. Reference DESIGN.md
tokens; no new tokens without a human-approved DESIGN.md change.

## Test plan
Backend unit and infrastructure tests, frontend behavior tests, Playwright E2E path.

## Tasks
| # | Owner | Repo | Paths | Depends on | Done when (commands) |
|---|-------|------|-------|------------|----------------------|
| 1 | Codex | Shipwise | src/Modules/<Name>/**, contracts/openapi/** | - | scripts/Verify-Platform.ps1 passes |
| 2 | Codex | Shipwise-Web | src/lib/api/**, src/features/<name>/api/** | 1 | npm run check passes |
| 3 | Gemini | Shipwise-Web | src/features/<name>/components/**, pages/** | 2 | npm run check, npm run test:e2e, impeccable detect clean |
| 4 | Claude Code | both | review | 1-3 | definition of done met |

## Assumptions and open questions
```

## Rules for the task table

- Follow the ownership rules in both `AGENTS.md` files: Codex owns the backend and the
  frontend data layer; Gemini owns frontend presentation; Claude reviews.
- No two tasks that can run at the same time share a path. Shared hot files
  (`Directory.Packages.props`, `Shipwise.slnx`, `package.json`/`package-lock.json`,
  `src/app/router/**`, `DESIGN.md`, `src/styles/globals.css`) belong to exactly one task.
- Each task is one pull request with verification commands that prove it done.
- Keep slices thin: one user-visible outcome per spec. Split anything bigger.

## Finish

Summarize the spec in a few lines, list open questions, and ask the user to approve it.
