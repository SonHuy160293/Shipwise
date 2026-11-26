# 0007 Two agents: Claude Code plans, Codex implements, review checks conformance

Status: accepted · Date: 2026-11-26 · Decider: owner · Related: ADR 0006, `docs/workflow.md`, both `AGENTS.md` files

## Context

The workflow had three agents: Claude Code planned and reviewed, Codex built the backend
and the frontend data layer, and Gemini designed and built the frontend screens. The owner
no longer uses Gemini.

The review-change skill also asked the reviewer to rerun every "Done when" command, run
the app, judge screenshots and do a correctness pass. That duplicates what the author's
own run and CI already prove, and it costs the most Claude effort of any step. The owner
wants Claude's effort spent on planning and on helping the owner decide, and the review
reduced to checking that the implementation follows the approved plan.

Testing is already covered elsewhere: the author runs the task's "Done when" commands
before committing, and CI runs `scripts/Verify-Platform.ps1 -RequireInfrastructure` in
Shipwise and type checks, lint, unit tests, the build and Playwright in Shipwise-Web.
Gate 3 already waits for CI to be green before the next task starts.

## Options

| Option | For | Against |
|---|---|---|
| A. Keep the full review, give Gemini's work to Codex | Most checking | Most Claude effort; repeats the author's and CI's test runs |
| **B. Codex implements everything; the review checks conformance only** | Claude's effort goes to planning; one implementer, one reviewer | Bugs that no test catches are less likely to be found before the push |
| C. No review | Least effort | Breaks the definition of done ("a different agent has reviewed") and leaves nothing to check ownership, scope and the contract |

## Decision

Option B.

1. **Roles.**
   - Claude Code is the planner, business analyst, solution architect and decision
     support for the owner. It writes challenge briefs, specs, ADRs and task tables,
     prepares decision requests, and does the conformance review. It writes no product
     code.
   - Codex implements every task in both repositories: backend, contract, frontend data
     layer, screens, layouts, styles and end-to-end tests.
   - Gemini has no role. Its earlier work stays as history.
2. **Screen design.** There is no separate design proposal step. Claude Code writes the
   spec's "UI and states" section at the depth `docs/workflow.md` asks for, from
   `Shipwise-Web/DESIGN.md` and the existing pages. Codex builds from that section and
   `DESIGN.md`, using the impeccable and ui-ux-pro-max skills as guidance. A change to
   design tokens or `DESIGN.md` is a decision request to the owner. Proposals already in
   `Shipwise-Web/docs/design/proposals` stay as records.
3. **Conformance review.** The reviewer reads, and runs no command. It checks that:
   - every changed path is inside the task's Paths;
   - each covered acceptance criterion has an implementation and a test that names it;
   - the API matches the spec's contract and the committed OpenAPI document, the
     database matches the spec's database design, and the screens implement the states
     the spec lists;
   - deviations are declared and approved in the spec's change log;
   - the commits follow the commit rule, and the `Verified:` trailers show every
     "Done when" command as `PASSED`, with none left `Not-run`.

   For a high-risk task (authentication, tenancy, permissions, personal data, a
   destructive migration) it also reads a short risk checklist: authentication fails
   closed; no secret or personal data reaches a log, trace or response it should not;
   every action has `[PermissionAuthorize]` or `[AuthenticatedUserOnly]`, or is a
   `PublicApiController` action the spec marks public; the migration
   is not destructive, or the owner approved it; organization-owned data has its
   cross-organization tests.

   It does not run tests, the app or Playwright, judge visuals, or hunt for bugs beyond
   those checks. Findings are `BLOCKER` (fix before the push) or `NOTE` (optional or a
   follow-up).
4. **Evidence of testing.** The author's "Done when" run before committing, and CI after
   the push, are the test gates. Gate 3 is unchanged: the next task starts only after the
   human pushes and CI is green. A red CI on `main` is fixed before any other work.
5. **Reviewer under Orca.** This amends ADR 0006 decision 6. Every implementation task
   starts with `--agent codex`. Reviews start with `--agent claude --model
   claude-sonnet-5-5 --effort low`, the owner's standing choice. ADR 0006's rule that only
   the human names `--model` and `--effort` is met by this decision; changing them is the
   owner's call.

## Consequences

- `AGENTS.md` in both repositories, `docs/workflow.md`, `docs/specs/README.md`, the
  write-spec and review-change skills and the Shipwise-Web guides drop Gemini and the
  design proposal step.
- Open specs reassign Gemini's tasks to Codex through their change logs (spec 0003
  task 3).
- Gemini's project configuration in Shipwise-Web (`.gemini/settings.json`) is an agent
  setting, so only the owner removes it.
- A defect that the tests miss and the checklist does not cover reaches `main` and is
  found by CI, the human's integration check, or later use. The spec's test plan carries
  more weight: every criterion needs a test that would fail without it.

## Revisit when

- A defect reaches `main` that the old full review would have caught.
- A second human developer joins, or the pull request model in `docs/workflow.md` starts.
- The owner adds another implementing agent.
