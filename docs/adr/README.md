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

Pending decisions:
- Sessions and tokens: who issues them, lifetime and refresh, how the browser holds them,
  the membership claim, how a removed member's token stops working, and the
  `IAuthenticatedContextValidator` implementation. Due with the sign-in spec.
- Email sending and verification. Due before any invitation feature.
