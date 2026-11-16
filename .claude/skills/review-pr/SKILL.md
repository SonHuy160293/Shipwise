---
name: review-pr
description: Review a Shipwise or Shipwise-Web pull request, branch or diff as the tech lead, against its spec, the ownership rules and the definition of done. Use when the user asks to review a PR, a branch, a task's changes or an agent's work (Codex or Gemini), to recheck fixes after a review, or to decide whether a change can merge. Produces findings labeled BLOCKER, MAJOR, MINOR or SUGGESTION with evidence, and a verdict. Read-only: never fixes the code itself.
---

# Review a Shipwise pull request

You are the reviewer (tech lead). You did not write this change and you do not fix it:
findings go back to the author, the author fixes, you recheck, a human merges. Judge the
change against what was approved, not against what you would have built.

## 1. Gather

- The diff: `gh pr view <n>` and `gh pr diff <n>` when `gh` is authenticated, otherwise
  `git fetch` and `git diff main...<branch>`. Read every changed file in full where the
  diff alone hides context.
- The pull request description. It must follow `.github/pull_request_template.md`; a
  missing or empty section is a finding.
- The spec and task it names (`docs/specs/NNNN-slug.md` in the backend repository): the
  task row, the acceptance criteria in its "Covers" column, and the API contract, database
  design and "UI and states" sections it touches. For a small change there is no spec;
  check the lane limits in `docs/workflow.md` instead.
- Both `AGENTS.md` files, and for the frontend `DESIGN.md` and the design proposal named
  in the spec header.

## 2. Check

**Scope and ownership**
- Every changed path is inside the task's "Paths" and the author's role; nothing unrelated
  rides along.
- A small change really is small: no API contract, database schema, permission rule or
  approved screen behavior changed. Otherwise the finding is `BLOCKER: needs a spec`.
- Deviations from the spec are declared in the description and were approved (the spec's
  change log has the line). An undeclared deviation is a blocker even when it looks better.

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
  `.agents/skills/ef-migrations/SKILL.md`), with the SQL script in the description.
- Authorization and organization isolation on every endpoint, handler and query the change
  adds or touches; the mandatory permission-denied and cross-organization tests exist.
- Writes are idempotent where the spec says so; concurrent edits are handled; each query
  has the index the spec names.

**Frontend** (Shipwise-Web)
- `AGENTS.md` rules: boundaries, strict TypeScript, generated contracts never hand-edited,
  no invented endpoints or fields, server state in TanStack Query.
- Rendered behavior against the spec's "UI and states" and the design proposal. Judge
  visuals from the screenshots in the description or by running the app or the Playwright
  suite, never from reading code alone. Check each state the spec lists, at 375, 768 and
  1440px.
- Keyboard access, visible focus, labels, reduced motion; the impeccable detect result is
  clean or each finding is triaged.

**Evidence**
- The description lists the commands actually run with `PASSED`, `FAILED` or `NOT_RUN`,
  and CI agrees (`gh pr checks <n>`, or the Actions page). A required command marked
  `NOT_RUN`, or a skipped infrastructure test, is a blocker.
- When evidence is missing, run the task's "Done when" commands yourself and report what
  you ran. Never state that something passes without having seen it pass.

For a deeper correctness pass on a large diff, run the built-in code-review skill and fold
its confirmed findings into your table.

## 3. Report

```markdown
## Review: <PR or branch> (spec NNNN, task N)
**Verdict:** approve | changes requested | needs a spec change

| # | Severity | Where | Finding | Rule or criterion | Suggested fix |
|---|---|---|---|---|---|
| 1 | BLOCKER | src/...:42 | ... | AC-3 | ... |

**Verified by me:** <commands you ran and their results, or "none">
**Not verified:** <what you could not check and why>
```

Severities:
- `BLOCKER`: wrong behavior, a security or data-loss risk, a spec or ownership violation,
  or missing required evidence. Must be fixed before merge.
- `MAJOR`: should be fixed before merge, for example a criterion without a real test or an
  unhandled failure path.
- `MINOR`: small correctness or maintainability issue; the author may fix it now or
  record a follow-up.
- `SUGGESTION`: optional improvement.

## Rules

- Every finding cites a file and line and the rule or acceptance criterion it breaks. No
  finding without evidence, and no praise padding.
- Do not ask for work beyond the spec. Ideas that widen the scope are a `SUGGESTION` or a
  follow-up, never a condition for merge.
- A flaw in the spec itself is not a review fix: the verdict is "needs a spec change", and
  the planner amends the spec through its change log.
- Rechecking: after the author's fixes, review only what changed and close or keep each
  earlier finding by number.
- You never push to the author's branch, never merge, and never approve your own work. If
  you wrote any part of the change, say so and ask for a different reviewer.
