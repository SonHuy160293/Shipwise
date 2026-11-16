# Delivery workflow

How a change moves from an idea to merged code across Shipwise (backend) and Shipwise-Web
(frontend). `AGENTS.md` in each repository holds the role and quality rules; this file
holds the process; the skills hold the detail: write-spec (planning), ef-migrations
(database) and review-pr (review).

## Lanes

Pick the lightest lane that fits. When in doubt, use the feature lane.

| Lane | Use when | Process |
|---|---|---|
| Small change | It changes no API contract, database schema, permission rule or approved screen behavior, and stays inside one role's paths in one repository | Branch `fix/<slug>`, the change with a focused test where behavior changes, the pull request template, review by a different agent |
| Feature | Anything else | The steps below |
| High risk | Authentication, tenancy, personal data, payments or a destructive migration | The feature lane, plus the human approves the risk explicitly in the spec, and the security tests in the spec's test plan are mandatory |

A small change that grows past its limits stops and goes back to the planner for a spec.

## Feature steps and gates

| # | Step | Who | Output | Gate |
|---|---|---|---|---|
| 1 | Challenge brief | Claude Code | Brief in chat (write-spec step 1) | **Gate 1:** the human resolves blockers and picks an approach |
| 2 | Draft spec | Claude Code | `docs/specs/NNNN-slug.md`, status `draft` | |
| 3 | Design proposal, when the table below asks for one | Gemini | `Shipwise-Web/docs/design/proposals/NNNN-slug/` | |
| 4 | Reconcile design, API contract and data model in the spec | Claude Code | Updated spec | **Gate 2:** the human sets `approved` |
| 5 | Backend, contract and frontend data layer | Codex | Pull requests from `feat/NNNN-slug` | **Gate 3:** review-pr has no open blocker, CI is green, the human merges |
| 6 | Frontend screens | Gemini | Pull request from `feat/NNNN-slug` | **Gate 3** |
| 7 | Integration check of the frontend against the real backend | Human | Manual | |
| 8 | Close | Claude Code | Spec status `done` with its delivery record | |

Design proposal depth:

| Change | Proposal |
|---|---|
| Text or small style change | None |
| Basic form or list that follows an existing page pattern | User flow and the list of screen states, in Markdown |
| New page or dashboard | The above plus layout for 375, 768 and 1440px |
| Complex multi-step flow or board | The above plus a standalone HTML prototype with mock data |

## Who decides

| Decision | Who |
|---|---|
| Naming and formatting that follow existing conventions | Agent |
| Reusing an existing helper or component without changing behavior | Agent |
| Internal implementation detail with no contract, data or security effect | Agent, noted in the pull request when it matters |
| Business rule, scope or permission | Human |
| Approved screen behavior or design tokens | Human |
| API contract shape or meaning | Human, through a spec change |
| Destructive migration, data risk or a security boundary | Human, explicitly |
| New dependency or infrastructure | Human, from a trade-off proposal or ADR |
| Hooks and agent settings | Human |
| Merge, release and deploy | Human |

## Before starting a task

- The spec is `approved`, and every task in your task's "Depends on" column is merged.
- Work on `feat/NNNN-slug` (or `fix/<slug>`), created from an up-to-date `main`. Use the
  same branch name in both repositories for one feature.
- Check the branch and working tree first. Never discard, reset or overwrite work you did
  not make, never force-push, never commit to `main`, and never merge or deploy.

## Stop and ask

Stop the affected task and write a decision request instead of guessing when:

- a requirement is missing, or two requirements contradict each other;
- the spec conflicts with the code, the API contract or the design proposal;
- you find a cross-organization access, data-loss or destructive-migration risk;
- the task needs paths outside the ones it owns, or another agent is editing them;
- the work grows in scope, dependencies or rollout risk beyond the spec.

A decision request gives the context, the options, your recommendation, the impact of
each option, and the safe default, which is not to implement the contested behavior.
Never fix a spec or contract inconsistency silently in code and document it afterward.

## Changing an approved spec

1. The planner records what was approved and what new evidence makes it inadequate.
2. It proposes the smallest fix and the alternatives, with the effect on stories, design,
   API contract, data and tasks.
3. The human approves the change. The planner edits the spec, adds a line to its change
   log, and reissues only the affected tasks.
4. Affected verification runs again.

## Pull requests and review

- One task is one pull request, filled in from `.github/pull_request_template.md`: spec
  and task, acceptance criteria covered, every command run with `PASSED`, `FAILED` or
  `NOT_RUN`, deviations from the spec, and the linked pull request in the other repository.
- Claude Code reviews with the review-pr skill and labels findings `BLOCKER`, `MAJOR`,
  `MINOR` or `SUGGESTION`. The author fixes; the reviewer rechecks; the reviewer never
  pushes fixes or widens the scope.

## Closing a feature

When every task is merged and the human confirms the integration check, the planner sets
the spec to `done` and fills its delivery record: pull request links, approved deviations,
deferred follow-ups, and any convention worth adding to `AGENTS.md` or a skill.

## Source of truth

| Question | Answer lives in |
|---|---|
| What the feature does, for whom, and how it is accepted | The approved spec in `docs/specs` |
| Why a durable technical choice was made | `docs/adr` |
| The HTTP contract | `contracts/openapi/shipwise.v1.json` |
| Visual identity and tokens | `Shipwise-Web/DESIGN.md` |
| Screen flow and states of a feature | The spec's "UI and states" section, built from the design proposal |
| What actually shipped | Git history and merged pull requests |
| What was verified | CI results and the pull request's command list |
