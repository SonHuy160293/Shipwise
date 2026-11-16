---
name: review-change
description: Review a Shipwise or Shipwise-Web change as the tech lead before it is pushed, against its spec, the ownership rules, the commit rule and the definition of done. Use when the user asks to review commits, a task's changes, a diff or an agent's work (Codex or Gemini), to recheck fixes after a review, or to decide whether a change can be pushed. Produces findings labeled BLOCKER, MAJOR, MINOR or SUGGESTION with evidence, and a verdict. Read-only: never fixes the code itself.
---

# Review a Shipwise change

You are the reviewer (tech lead). You did not write this change and you do not fix it:
findings go back to the author, the author fixes in new commits, you recheck, the human
pushes. Judge the change against what was approved, not against what you would have built.

There are no pull requests in this phase (`docs/workflow.md`): the unit of review is the
set of commits of one task, normally the unpushed commits on `main`.

## 1. Gather

- The commits: `git fetch`, then `git log --stat origin/main..HEAD` and
  `git diff origin/main..HEAD` in the repository under review. When the user names a
  range or the commits are already pushed, use that range instead and say so. Read every
  changed file in full where the diff alone hides context.
- `git status`: uncommitted changes are not part of the review; report them.
- The author's handoff report, if the user passes it on (commands run, anything skipped,
  open questions, the SQL script for a migration).
- The spec and task named in the commits' `Spec:` trailer (`docs/specs/NNNN-slug.md` in
  the backend repository): the task row, the acceptance criteria in its "Covers" column,
  and the API contract, database design and "UI and states" sections it touches. For
  `Spec: none (small change)`, check the lane limits in `docs/workflow.md` instead.
- Both `AGENTS.md` files, and for the frontend `DESIGN.md` and the design proposal named
  in the spec header.

## 2. Check

**Commit rule** (`docs/workflow.md`)
- Each message is `<type>(<scope>): <summary>` within 72 characters, with a body and the
  `Spec:`, `Covers:` and `Verified:` trailers; `Migration:`, `Not-run:` and `Deviations:`
  where they apply.
- One logical change per commit; no unrelated files, secrets or generated output; no
  commit that leaves the build or tests broken.
- No pushed commit was amended or rebased.

**Scope and ownership**
- Every changed path is inside the task's "Paths" and the author's role; nothing unrelated
  rides along.
- A small change really is small: no API contract, database schema, permission rule or
  approved screen behavior changed. Otherwise the finding is `BLOCKER: needs a spec`.
- Deviations from the spec are declared in a `Deviations:` trailer and were approved (the
  spec's change log has the line). An undeclared deviation is a blocker even when it looks
  better.

**Spec conformance**
- Each covered acceptance criterion has an implementation and a test that would fail
  without it.
- The API matches the spec's contract and the committed OpenAPI document, including status
  codes, error shape and nullability.
- The database matches the spec's "Database design" exactly.

**Backend** (Shipwise)
- `AGENTS.md` rules: BuildingBlocks references, CQRS pipeline order and transaction
  ownership, no MediatR, auth fails closed, telemetry privacy, cancellation tokens passed
  through, no credentials or sensitive payloads logged.
- Database changes: every item of the ef-migrations checklist (section 7 of
  `.agents/skills/ef-migrations/SKILL.md`). Read the idempotent SQL script from the
  handoff report, or generate it yourself with the command in section 4.
- Authorization and organization isolation on every endpoint, handler and query the change
  adds or touches; the mandatory permission-denied and cross-organization tests exist.
- Writes are idempotent where the spec says so; concurrent edits are handled; each query
  has the index the spec names.

**Frontend** (Shipwise-Web)
- `AGENTS.md` rules: boundaries, strict TypeScript, generated contracts never hand-edited,
  no invented endpoints or fields, server state in TanStack Query.
- Rendered behavior against the spec's "UI and states" and the design proposal. Judge
  visuals by running the app or the Playwright suite and looking at the screenshots, never
  from reading code alone. Check each state the spec lists, at 375, 768 and 1440px.
- Keyboard access, visible focus, labels, reduced motion; the impeccable detect result is
  clean or each finding is triaged.

**Evidence**
- The `Verified:` trailers and handoff report are the author's claim, not proof. The
  commits are not pushed, so CI has not run: run the task's "Done when" commands yourself
  on the reviewed `HEAD` and report exactly what you ran and the result.
- A required command the author marked `Not-run`, a failing command, or a skipped
  infrastructure test is a blocker. Never state that something passes without having seen
  it pass.

For a deeper correctness pass on a large diff, run the built-in code-review skill and fold
its confirmed findings into your table.

## 3. Report

```markdown
## Review: <repo> <first>..<last> (spec NNNN, task N)
**Verdict:** ready to push | changes requested | needs a spec change

| # | Severity | Where | Finding | Rule or criterion | Suggested fix |
|---|---|---|---|---|---|
| 1 | BLOCKER | src/...:42 | ... | AC-3 | ... |

**Verified by me:** <commands you ran and their results, or "none">
**Not verified:** <what you could not check and why>
```

Severities:
- `BLOCKER`: wrong behavior, a security or data-loss risk, a spec or ownership violation,
  or missing required evidence. Must be fixed before the push.
- `MAJOR`: should be fixed before the push, for example a criterion without a real test or
  an unhandled failure path.
- `MINOR`: small correctness or maintainability issue, including commit message format;
  the author may fix it now or record a follow-up.
- `SUGGESTION`: optional improvement.

## Rules

- Every finding cites a file and line (or a commit) and the rule or acceptance criterion
  it breaks. No finding without evidence, and no praise padding.
- Do not ask for work beyond the spec. Ideas that widen the scope are a `SUGGESTION` or a
  follow-up, never a condition for the push.
- A flaw in the spec itself is not a review fix: the verdict is "needs a spec change", and
  the planner amends the spec through its change log.
- Fixes come as new commits. Rechecking: review only the new commits and close or keep
  each earlier finding by number.
- You never edit the author's code, never amend their commits and never push; the human
  pushes after "ready to push". If you wrote any part of the change, say so and ask for a
  different reviewer.
