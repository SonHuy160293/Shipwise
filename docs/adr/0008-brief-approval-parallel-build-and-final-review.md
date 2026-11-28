# 0008 Brief approval, parallel build and one final review

Status: accepted · Date: 2026-11-28 · Decider: owner · Related: ADR 0003, ADR 0006, ADR 0007, `docs/workflow.md`

## Context

Spec 0003 ran under ADR 0006 and ADR 0007. The owner approved the challenge brief, then
approved the spec again, and then waited on a review, a push and CI between every
task. The frontend tasks waited for the backend to be pushed, although the API contract
was already fixed in the approved spec. Most of the wall-clock time went to those waits,
not to building.

The owner wants:
- one approval, at the end of the challenge brief, and no second approval of the spec;
- the backend and the frontend built at the same time once the contract exists;
- an automated integration test against the real stack;
- one conformance review at the end, after the integration test;
- the owner's own manual check of the app outside the process, done when the owner
  chooses.

Two facts constrain how:
- ADR 0003: the committed OpenAPI document is generated from the API host, and a test
  fails when they differ. A hand-written contract would never match that output.
- The two repositories are separate checkouts. Workers in Shipwise and in Shipwise-Web
  share no files and need no branches.

## Options

| Option | For | Against |
|---|---|---|
| A. Keep ADR 0006 and ADR 0007 as they are | Every task is reviewed and pushed before the next | Slowest; two owner approvals; the frontend waits on the backend push |
| **B. Brief approval, contract skeleton first, parallel build, integration test, one final review** | One owner approval; backend and frontend run at once; the review sees the whole feature once | Nothing is pushed until the end, so CI runs once, late; one larger review |
| C. As B, but the frontend writes against a hand-written OpenAPI file | The frontend starts before any backend code | Breaks ADR 0003's drift test |

## Decision

Option B. This replaces ADR 0006 decisions 2 (Run only after Gate 2), 3 (a review task per
implementation task), 5 (one worker at a time) and 9 (review and push before each next
task), and the per-task timing in ADR 0007 decisions 3 and 4. Everything else in those
ADRs stands.

1. **Approval.** The owner's decision at the end of the challenge brief (Gate 1) is the
   approval. The planner writes the spec from the brief and sets it to `approved` itself.
   If writing the spec raises a decision the brief did not settle (any "Who decides →
   Human" row: a business rule, scope, a permission, the contract's meaning, a
   destructive migration, a new dependency, a design token), the planner asks the owner
   first and records the answer in the spec. There is no Gate 2.
2. **Order of tasks.** The spec's task table follows this shape:
   1. Migration, when the spec changes the database (Shipwise).
   2. Contract skeleton (Shipwise): controllers, request and response models and the
      generated OpenAPI document, with endpoints that answer 501, so ADR 0003's drift
      test passes. No owner review or approval.
   3. In parallel: the backend implementation (Shipwise) and the frontend (Shipwise-Web),
      which generates its types from the local `../Shipwise/contracts/openapi` document.
   4. Integration test (Astra 6) against the real stack, reporting only.
   5. Final conformance review (Sonnet) of the whole feature in both repositories.
3. **Parallel workers.** At most one worker per checkout at a time, so one in Shipwise and
   one in Shipwise-Web can run at once. A task waits only for the tasks in its "Depends
   on" column to report `succeeded`, not for a push. The coordinator edits nothing in a
   checkout while a worker is dispatched there.
4. **Integration test.** Codex with `--model gpt-6-astra` runs the integration task named in
   the spec: it starts the stack, writes and runs tests only in the paths the task row
   names, and reports every failure with its evidence. It changes no product code. The
   coordinator sends each failure to the implementation worker that owns the failing
   side, or to the owner when the owning side is unclear; that worker fixes it in a new
   commit, and the integration task runs again.
5. **Final review.** After the integration test passes, one Sonnet conformance review
   (review-change skill, read-only) covers every task's commits in both repositories,
   all the criteria, the change log and the commit rule. Findings go back to the owning
   worker, who fixes them in new commits, and the reviewer rechecks only those.
6. **Push and close.** Nothing is pushed until the final review says "ready to push". Then
   the owner pushes both repositories and confirms CI is green, and the planner closes the
   spec. A red CI is fixed forward before any other work. The owner's manual check of the
   app is the owner's own, outside this process, and does not block closing.
7. **Models.** Unchanged from `docs/workflow.md`: implementation with
   `--model gpt-6.1-sol`, the integration test with `--model gpt-6-astra`, and the final
   review with `--agent claude --model claude-sonnet-5-5 --effort low`.

## Consequences

- The owner's attention is needed at the brief, for questions a worker or the planner
  cannot settle, for permission prompts while Agent Permissions is Manual, and for the
  push.
- Unpushed work grows during a feature. A crash or a bad local state loses more, and CI
  finds problems only at the end. Each commit still passes its own "Done when" commands.
- With Manual permissions, two workers can ask for approval at the same time.
- Without Gate 2, an API or data design detail the brief did not cover reaches the build
  without the owner reading it, unless the planner asks under decision 1. The final
  review checks the build against the spec, not the spec's quality.
- `docs/workflow.md`, the write-spec and review-change skills, `docs/specs/README.md` and
  both `AGENTS.md` files change to match.

## Revisit when

- A defect reaches `main` that a per-task review or a spec reading would have caught.
- CI fails after a final push often enough to cost more than per-task pushes.
- Two tasks in one checkout need to run at once.
- The pull request model in `docs/workflow.md` starts.
