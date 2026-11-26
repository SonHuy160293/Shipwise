# Architecture decision records

One file per decision: `NNNN-slug.md` with context, options, decision and consequences.
The planner (Claude Code) writes them; a human accepts them. Frontend-only decisions stay
in Shipwise-Web/docs/architecture/decisions.

Status is `proposed` until the owner accepts it, then `accepted`; a replaced decision
becomes `superseded by NNNN`.

| # | Decision | Status |
|---|---|---|
| [0001](0001-accounts-and-passwords.md) | Accounts and passwords are owned by Shipwise | accepted |
| [0002](0002-organizations-roles-and-permission-checks.md) | Organizations, fixed roles, and the role per organization carried in the token | accepted |
| [0003](0003-api-contract-workflow.md) | API contract: generated from the API host and checked by a test | accepted |
| [0004](0004-deployment-and-configuration-model.md) | Deployment and configuration model | accepted |
| [0005](0005-sessions-and-tokens.md) | Sessions and tokens: both tokens in HttpOnly cookies | accepted |
| [0006](0006-orca-orchestration.md) | Agent handoffs run through Orca orchestration | accepted; decision 6 amended by 0007 |
| [0007](0007-two-agent-roles-and-conformance-review.md) | Two agents: Claude Code plans, Codex implements, review checks conformance | accepted |

Pending decisions:
- Email sending and verification. Due before any invitation feature.
