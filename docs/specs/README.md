# Feature specs

One file per feature slice: `NNNN-slug.md`, written by the planner (Claude Code) with the
write-spec skill. Every spec starts with a challenge brief in chat: the planner checks the
idea for reasonableness, researches how real products solve it, proposes alternatives and
asks questions, and writes the file only after the user answers. The user's final decision
on the brief is the approval: the planner writes the spec and sets it to `approved`, asking
the user first about any decision the brief did not settle (ADR 0008). Status moves
`draft` → `approved` → `done`.
The implementer (Codex) starts only from approved specs and only on the tasks the
spec's task table assigns to them. Shipwise-Web agents read this folder from the sibling
backend checkout.

Acceptance criteria are numbered `AC-1`, `AC-2`, ... and referenced by tasks, tests and
commit messages. Changes after approval go through the spec's change log, and a spec is
`done` only with its delivery record filled in. The surrounding process, including the
small change lane that needs no spec, is in [docs/workflow.md](../workflow.md).

| # | Spec | Status |
|---|---|---|
| [0001](0001-user-registration.md) | User registration | approved |
| [0002](0002-containers-and-local-stack.md) | Containers and the local stack | done |
| [0003](0003-sign-in-and-sessions.md) | Sign-in and sessions | done |

Planned next, not written: 0004 create an organization, 0005 invitations and roles.
