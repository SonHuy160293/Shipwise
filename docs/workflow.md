# Delivery workflow

How a change moves from an idea to code on `main` across Shipwise (backend) and
Shipwise-Web (frontend). `AGENTS.md` in each repository holds the role and quality rules;
this file holds the process; the skills hold the detail: write-spec (planning),
ef-migrations (database) and review-change (review).

In this phase there are no pull requests and no feature branches: agents commit directly
to `main` under the commit rule below, and the review happens on local commits before
they are pushed.

## Lanes

Pick the lightest lane that fits. When in doubt, use the feature lane.

| Lane | Use when | Process |
|---|---|---|
| Small change | It changes no API contract, database schema, permission rule or approved screen behavior, and stays inside one role's paths in one repository | The change with a focused test where behavior changes, one commit under the commit rule, review by a different agent |
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
| 5 | Backend, contract and frontend data layer | Codex | Local commits on `main` | **Gate 3:** review-change has no open blocker, then the human pushes and CI is green |
| 6 | Frontend screens | Gemini | Local commits on `main` | **Gate 3** |
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
| Internal implementation detail with no contract, data or security effect | Agent, noted in the commit body when it matters |
| Business rule, scope or permission | Human |
| Approved screen behavior or design tokens | Human |
| API contract shape or meaning | Human, through a spec change |
| Destructive migration, data risk or a security boundary | Human, explicitly |
| New dependency or infrastructure | Human, from a trade-off proposal or ADR |
| Hooks and agent settings | Human |
| Push, release and deploy | Human |

## Before starting a task

- The spec is `approved`, and every task in your task's "Depends on" column is reviewed
  and pushed.
- You are on `main`, up to date with `origin/main`. Run `git status` first: if the working
  tree has changes you did not make, stop and ask; never discard, reset, stash or
  overwrite them.

## Commit rule

Agents commit directly to `main`. One agent works in a checkout at a time.

**When to commit**
- Only when the task's "Done when" commands pass (`scripts/Verify-Platform.ps1`, or
  `npm run check` plus `npm run test:e2e` for screens). `main` is never left red.
- One logical change per commit. A task may take several commits, but each one builds and
  passes on its own. Never mix unrelated changes, two tasks, or formatting noise with
  behavior.
- Stage explicit paths (`git add <path>`), never `git add -A` or `git add .`. Read
  `git diff --staged` before committing: no secrets, no generated output, nothing outside
  the paths your task owns.

**Message**

```
<type>(<scope>): <summary>

<what changed and why, wrapped at 72 columns>

Spec: 0001 task 1
Covers: AC-1, AC-3
Verified: scripts/Verify-Platform.ps1 PASSED
```

- `type`: `feat`, `fix`, `refactor`, `perf`, `test`, `docs`, `build`, `ci` or `chore`.
- `scope`: the module or feature in lowercase (`organizations`), or `platform`,
  `contract`, `spec`, `workflow`, `agents`.
- `summary`: imperative, lowercase start, no period; the whole first line is at most 72
  characters. It says what the change does, not "update" or "fix stuff".
- `Spec:` is required. A small change writes `Spec: none (small change)` and omits
  `Covers:`.
- `Verified:` lists every command actually run with `PASSED` or `FAILED`. Add
  `Not-run: <command>, <reason>` for anything required that was not run, and
  `Deviations: <what and the spec change log line that approved it>` when the change
  departs from the spec. Never write `PASSED` for a command you did not run.
- A contract change that breaks existing clients adds `!` after the scope
  (`feat(contract)!: ...`) and needs an approved spec.
- A database change adds `Migration: <name>`, and the author gives the reviewer the
  idempotent SQL script in the handoff report.

**History**
- Never amend, rebase, squash or force-push a commit that has been pushed; fix forward
  with a new commit. Never use `--no-verify`.
- Review findings are fixed in new commits (`fix(<scope>): ...` naming the finding), not
  by rewriting the reviewed ones.
- Do not push. After review-change reports no open blocker, the human pushes (or tells an
  agent to), using `git pull --rebase` first if `origin/main` moved. If CI goes red on
  `main`, fixing it comes before any other work.

**Handoff report.** After committing, tell the human: the commit hashes, the task and
criteria covered, the commands run with their results, anything not run or skipped, and
any deviation or open question.

## Stop and ask

Stop the affected task and write a decision request instead of guessing when:

- a requirement is missing, or two requirements contradict each other;
- the spec conflicts with the code, the API contract or the design proposal;
- you find a cross-organization access, data-loss or destructive-migration risk;
- the task needs paths outside the ones it owns, or the working tree has someone else's
  uncommitted changes;
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

## Review

- Claude Code reviews the unpushed commits of a task with the review-change skill
  (`git log origin/main..HEAD`) and labels findings `BLOCKER`, `MAJOR`, `MINOR` or
  `SUGGESTION`.
- The author fixes in new commits; the reviewer rechecks; the reviewer never edits the
  code or widens the scope.
- A change is pushed only with no open blocker.

## Running a task with Orca

Feature and high-risk tasks run under Orca orchestration
([ADR 0006](adr/0006-orca-orchestration.md)); a small change may too. Every rule above still applies: Orca only carries the briefs,
reports, questions and findings between agents. When Orca is unavailable, hand off by
hand as above.

**Before the first Run on a machine (human)**
- Orca is running (`orca status --json` succeeds), Settings → Experimental →
  Orchestration is on, and Settings → Agents → Agent Permissions is Manual (ADR 0006).
  A worker waiting on a permission prompt needs the human in its terminal or chat.
- Commands and flags come from `orca skills get orchestration --full` for the installed
  version. Pass `--environment local` on every orchestration command.

**Coordinator (Claude Code)**
- Runs in an Orca terminal on the Shipwise primary checkout. It creates one Run per spec,
  only after Gate 2 (`run-create --objective "Spec NNNN <title>"`).
- Creates one task per task-table row, plus one review task per implementation task that
  depends on it (`task-create --spec-file <file> --deps '["<task id>"]'`).
- Starts each worker in a primary checkout, never a new worktree: `--worktree current`
  for Shipwise, the Shipwise-Web checkout's worktree selector for Shipwise-Web. One worker
  per checkout at a time; the coordinator edits no file in a checkout while a worker is
  dispatched there.
- Agent per owner: Codex `--agent codex`, Gemini `--agent gemini`, reviews
  `--agent claude`.
- Starts a worker, then ends its turn. Orca wakes it when a worker reports or asks; it
  never polls or reads a worker's terminal (`worker-read --dispatch <id>` shows output).

**Task spec** (a file per task, passed with `--spec-file`; the first line is the title)

```
Spec NNNN task N: <title>
Read docs/specs/NNNN-slug.md, task N, and AGENTS.md before starting.
Owner: <agent>. Repository: <repo>.
Paths you may edit: <the task's Paths column>.
Covers: <AC list>.
Done when: <the task's commands>. Any failing command means the task failed.
Commit under the commit rule. Do not push.
Placement: this machine (local), set up on this machine.
```

A review task names the implementation task, runs the review-change skill on its
unpushed commits, and reports the findings and the verdict in `worker_done`.

**Workers (Codex, Gemini, Claude as reviewer)**
- Follow the dispatch preamble and the repository's `AGENTS.md`.
- Stop and ask: send the decision request with `orca orchestration ask`, then end the
  turn and wait for the answer.
- Finish with exactly one `worker_done`. Its body is the handoff report. A failing
  "Done when" command is `--outcome failed`.
- Never push, rewrite history or edit paths the task does not own.

**Questions, review and Gate 3**
- The coordinator answers a question only when the approved spec or a "Who decides →
  Agent" row settles it. Anything else goes to the human word for word; the worker waits.
- After an implementation task succeeds, the coordinator starts its review task. Findings
  go back to the author with `send --to dispatch:<id> --body-file <findings>`; the author
  fixes them in new commits and reports again; the reviewer rechecks.
- With no open blocker, the coordinator reports to the human, who pushes. A dependent
  task starts only after the human confirms the push and `git fetch` shows the commits
  on `origin/main`.

**When something fails**
- Follow the `next` or recovery steps the command prints. Retry with
  `worker-start --task <id> --retry-of <dispatch id> ...` only after the cause has
  changed. A task that fails three starts stays failed; report it to the human.
- Never mark a reported failure completed, and never run `orchestration reset` unless
  the human gives up that Run.

## Closing a feature

When every task is pushed and the human confirms the integration check, the planner sets
the spec to `done` and fills its delivery record: commit hashes per task, approved
deviations, deferred follow-ups, and any convention worth adding to `AGENTS.md` or a
skill.

## Source of truth

| Question | Answer lives in |
|---|---|
| What the feature does, for whom, and how it is accepted | The approved spec in `docs/specs` |
| Why a durable technical choice was made | `docs/adr` |
| The HTTP contract | `contracts/openapi/shipwise.v1.json` |
| Visual identity and tokens | `Shipwise-Web/DESIGN.md` |
| Screen flow and states of a feature | The spec's "UI and states" section, built from the design proposal |
| What actually shipped | Git history on `main` |
| What was verified | CI results and each commit's `Verified:` line |

## Later: pull requests

When more than one person or agent works at the same time, or a release needs protection,
switch to feature branches and pull requests: the commit rule stays, the review moves to
the pull request, and `main` gets branch protection. That is a human decision recorded
here.
