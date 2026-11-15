# Architecture decision records

One file per decision: `NNNN-slug.md` with context, options, decision and consequences.
The planner (Claude Code) writes them; a human accepts them. Frontend-only decisions stay
in Shipwise-Web/docs/architecture/decisions.

Pending decisions before the first feature:
- Identity provider and session model (the JWT helpers fail closed without an
  authoritative active-context validator).
- Contract workflow: how the API host emits contracts/openapi/shipwise.v1.json and how CI
  detects drift.
