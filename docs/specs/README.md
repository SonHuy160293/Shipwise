# Feature specs

One file per feature slice: `NNNN-slug.md`, written by the planner (Claude Code) with the
write-spec skill. Every spec starts with a challenge brief in chat: the planner checks the
idea for reasonableness, researches how real products solve it, proposes alternatives and
asks questions, and writes the file only after the user answers. Status moves `draft` → `approved` (set by a human) → `done`.
Implementers (Codex, Gemini) start only from approved specs and only on the tasks the
spec's task table assigns to them. Shipwise-Web agents read this folder from the sibling
backend checkout.

Acceptance criteria are numbered `AC-1`, `AC-2`, ... and referenced by tasks, tests and
commit messages. Changes after approval go through the spec's change log, and a spec is
`done` only with its delivery record filled in. The surrounding process, including the
small change lane that needs no spec, is in [docs/workflow.md](../workflow.md).

| # | Spec | Status |
|---|---|---|
| [0001](0001-user-registration.md) | User registration | approved |
| [0002](0002-containers-and-local-stack.md) | Containers and the local stack | approved |

Planned next, not written: 0003 sign-in and sessions, 0004 create an organization, 0005
invitations and roles.
