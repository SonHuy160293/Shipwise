---
name: review-change
description: Conformance review of a Shipwise or Shipwise-Web change before it is pushed - checks that Codex's commits follow the approved spec and task, the ownership rules and the commit rule, plus a short risk checklist for high-risk tasks. Read-only and runs no command; testing is the author's run and CI. Use when the user asks to review commits, a task's changes or Codex's work, to recheck fixes after a review, or to decide whether a change can be pushed. Produces BLOCKER and NOTE findings with evidence, and a verdict.
---

# Conformance review of a Shipwise change

You check that the change follows the plan, not how well it is built ([ADR 0007](../../../docs/adr/0007-two-agent-roles-and-conformance-review.md)).
You do not run tests, builds, the app or Playwright: the author's "Done when" run and CI
are the test gates. You do not fix anything: findings go back to the author, who fixes
them in new commits; you recheck; the human pushes. Keep the review short.

The unit of review is the commits of one task, normally the unpushed commits on `main`.

## 1. Gather

- The commits: `git log --stat <range>` and `git diff <range>` in the repository under
  review, with the range the coordinator or the user gives (default
  `origin/main..HEAD`). Read a whole file only where the diff hides what you must check.
- `git status`: uncommitted changes are not part of the review; mention them.
- The author's handoff report, if given.
- The spec and task named in the `Spec:` trailer (`docs/specs/NNNN-slug.md` in
  Shipwise): the task row, the criteria in its "Covers" column, the change log, and the
  API contract, database design and "UI and states" sections the task touches. For
  `Spec: none (small change)`, check the lane limits in `docs/workflow.md` instead.

## 2. Check

1. **Paths.** Every changed path is inside the task's Paths. A small change touches no
   API contract, database schema, permission rule or approved screen behavior; otherwise
   `BLOCKER: needs a spec`.
2. **Criteria.** Each covered acceptance criterion has an implementation and at least one
   test that clearly targets it (by name or by what it asserts). Read; do not run.
3. **Contract and data.** Endpoints, status codes, error codes and fields match the spec's
   API contract, and the committed OpenAPI document changed with them. Tables, columns,
   indexes and constraints match the spec's database design.
4. **Screens.** Each route and state the spec's "UI and states" lists has code that
   renders it. No visual judgment.
5. **Deviations.** Anything that departs from the spec is in a `Deviations:` trailer and
   in the spec's change log. An undeclared deviation is a `BLOCKER`, even if it looks
   better.
6. **Commit rule.** Message format and trailers; `Verified:` lists every "Done when"
   command of the task as `PASSED`; nothing required is `Not-run`; no skipped
   infrastructure test is reported. You check the claim is complete, not that it is true.
7. **Risk checklist, high-risk tasks only** (authentication, tenancy, permissions,
   personal data, destructive migration):
   - authentication fails closed;
   - no password, token, secret or personal data in logs, traces, metrics or exception
     messages, and none in a response body the spec does not list;
   - every API action has `[PermissionAuthorize]` or `[AuthenticatedUserOnly]` (or is a
     `PublicApiController` action the spec marks public);
   - a migration drops or rewrites nothing, or the spec records the owner's approval;
   - organization-owned data has the permission-denied and cross-organization tests.

Out of scope: code style, naming, refactoring ideas, performance, and bugs these checks
do not reveal. Mention something outside them only if it is plainly a security or
data-loss risk, as a `NOTE`.

## 3. Report

```markdown
## Review: <repo> <first>..<last> (spec NNNN, task N)
**Verdict:** ready to push | changes requested | needs a spec change

| # | Severity | Where | Finding | Check or criterion |
|---|---|---|---|---|
| 1 | BLOCKER | src/...:42 | ... | AC-3 / Paths / Commit rule |
```

- `BLOCKER`: a failed check above. Must be fixed before the push.
- `NOTE`: optional, or a follow-up for the planner.
- "ready to push" means no open `BLOCKER`.

## Rules

- Every finding cites a file and line (or a commit) and the check or criterion it fails.
- Do not ask for work beyond the spec.
- A flaw in the spec itself is not a code fix: the verdict is "needs a spec change".
- Rechecking: read only the new commits, and close or keep each earlier finding by number.
- Never edit code, amend commits or push. If you wrote any part of the change, say so and
  ask for a different reviewer.
