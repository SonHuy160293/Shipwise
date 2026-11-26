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
| 2 | Draft spec, including the screens in "UI and states" | Claude Code | `docs/specs/NNNN-slug.md`, status `draft` | |
| 3 | Reconcile UI, API contract and data model in the spec | Claude Code | Updated spec | **Gate 2:** the human sets `approved` |
| 4 | Backend, contract and frontend data layer | Codex | Local commits on `main` | **Gate 3:** the conformance review has no open blocker, then the human pushes and CI is green |
| 5 | Frontend screens | Codex | Local commits on `main` | **Gate 3** |
| 6 | Integration check of the frontend against the real backend | Human | Manual | |
| 7 | Close | Claude Code | Spec status `done` with its delivery record | |

Claude Code plans and reviews; Codex implements everything
([ADR 0007](adr/0007-two-agent-roles-and-conformance-review.md)). There is no separate
design proposal step: the spec's "UI and states" section is the design, at this depth.

| Change | "UI and states" holds |
|---|---|
| Text or small style change | Nothing (small change lane) |
| Basic form or list that follows an existing page pattern | The user flow and the table of screen states |
| New page or dashboard | The above plus layout notes for 375, 768 and 1440px |
| Complex multi-step flow or board | The above plus a wireframe per step (Mermaid or text) |

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
- Do not push. After review-change reports no open blocker, the human pushes (or, outside
  Orca, tells an agent to), using `git pull --rebase` first if `origin/main` moved. Under
  Orca only the human pushes (see "Running a task with Orca"). If CI goes red on
  `main`, fixing it comes before any other work.

**Handoff report.** After committing, tell the human: the commit hashes, the task and
criteria covered, the commands run with their results, anything not run or skipped, and
any deviation or open question.

## Stop and ask

Stop the affected task and write a decision request instead of guessing when:

- a requirement is missing, or two requirements contradict each other;
- the spec conflicts with the code, the API contract or `DESIGN.md`;
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

- Claude Code reviews the unpushed commits of a task with the review-change skill. It is
  a conformance review: it checks that the commits follow the spec, the task's paths and
  the commit rule (plus a risk checklist for high-risk tasks), labels findings `BLOCKER`
  or `NOTE`, and runs no command. The author's "Done when" run and CI are the test gates
  ([ADR 0007](adr/0007-two-agent-roles-and-conformance-review.md)).
- The author fixes in new commits; the reviewer rechecks; the reviewer never edits the
  code or widens the scope.
- A change is pushed only with no open blocker.

## Running a task with Orca

Feature and high-risk tasks run under Orca orchestration
([ADR 0006](adr/0006-orca-orchestration.md)). Small changes keep the manual
handoff, and so does every task when Orca is unavailable. Every rule above still applies:
Orca only carries the briefs, reports, questions and findings between agents.

**Before the first Run on a machine (human)**
- Orca is running, orchestration is enabled, and Agent Permissions is set to Manual.
  These settings come from Orca's web documentation, not the installed guide, so the
  human checks them in the app (ADR 0006).
- With Manual set, a worker waiting on a permission prompt needs the human in its
  terminal or chat.
- Commands and flags come from `orca skills get orchestration --full` for the installed
  version, not from memory or this file.

**Coordinator (Claude Code)**
- Runs in an Orca terminal in the Shipwise repository workspace, not a folder workspace
  above it. It creates one Run per spec, only after Gate 2, and keeps the Run ID. Only one
  Run is active at a time across both checkouts. A command that targets an earlier Run
  passes `--run <run id>`, as the installed guide shows.
- Before the first start in a Run, asks the human to confirm Agent Permissions is Manual,
  and starts nothing until they do.
- Passes `--environment local` to `run-create`, `task-create`, `task-list`, `worker-list`
  and `worker-start`. When Orca wakes it with a `check` command, it runs that command
  exactly as printed; `check` takes no `--environment`.
- Never polls and never reads or types into a worker's terminal: `worker-read --dispatch
  <id>` shows output and `send` steers. After starting a worker it ends its turn.

**From task table to Orca tasks**
- Tasks are created one at a time, when they can start, in the order of the spec's task
  table. While one worker runs at a time, Orca `--deps` is not needed.
- Each implementation row becomes one task. Its review task is created only after the
  implementation's `worker_done` names its commits, so the review brief names an exact
  range.
- The spec's own review row (for example spec 0003 task 4) is covered in two parts: the
  pre-push review of each implementation task, then, after the last implementation task
  is pushed, one final review task with the row's whole scope: every task's commits in
  both repositories, all the criteria the row lists, and its "Done when" checks.
- One worker is dispatched at a time across the whole Run and both checkouts: an
  implementation, a review or a fix follow-up. The coordinator edits no file in a
  checkout while a worker is dispatched there.

```
orca orchestration run-create --environment local --objective "Spec 0003 sign-in and sessions"
orca orchestration task-create --environment local --spec-file t1.md
orca orchestration worker-start --environment local --task <t1 id> --worktree current --agent codex
# after t1's worker_done names its commits:
orca orchestration task-create --environment local --spec-file r1.md
orca orchestration worker-start --environment local --task <r1 id> --worktree current --agent claude
# after Gate 3 for t1: create and start t2 the same way
```

**Placement and agents**
- A Shipwise task uses `--worktree current`, the coordinator's own workspace.
- A Shipwise-Web task uses `--worktree <selector>`, the exact selector of the Shipwise-Web
  primary checkout's workspace, which the human gives. If it does not resolve, stop and
  ask the human; never fall back to another workspace.
- No worker creates a worktree or a branch.
- Implementation tasks: `--agent codex`. Reviews: `--agent claude --model
  claude-sonnet-5-5 --effort low`, the owner's standing choice (ADR 0007). Pass any other
  `--model` or `--effort` only when the human names them.

**Task spec** (one file per task, passed with `--spec-file`; the first line is the title)

```
Spec NNNN task N: <title>
Read <spec path from this checkout>, task N, and AGENTS.md before starting.
Owner: <agent>. Repository: <repo>.
Paths you may edit: <the task's Paths column>.
Covers: <AC list>.
Done when: <the task's commands>. Any failing command means the task failed.
Commit under the commit rule. Never push, even if a message asks you to.
Placement: this machine (local), set up on this machine.
```

The spec path is relative to the checkout the worker runs in:
`docs/specs/NNNN-slug.md` in Shipwise, `../Shipwise/docs/specs/NNNN-slug.md` in
Shipwise-Web. A review task names the implementation task and its commits, runs the
review-change rules on them read-only, and reports the review in `worker_done`;
`succeeded` means the review was completed, whatever the verdict.

**Workers (Codex, Claude as reviewer)**
- Follow the dispatch preamble and the repository's `AGENTS.md`.
- Stop and ask: send the decision request with `orca orchestration ask`, then end the
  turn and wait for the answer.
- Finish with exactly one `worker_done`. Its body is the handoff report. A failing
  "Done when" command is `--outcome failed`.
- Never push, rewrite history or edit paths the task does not own.

**Questions**
- The coordinator answers with `orca orchestration reply --id <message id> --body-file
  <file>`, and only to relay a decision already recorded (the approved spec, an accepted
  ADR, or the human's earlier answer in this Run), or to settle a routine choice of a
  "Who decides → Agent" row that no requirement touches.
- Everything else goes to the human word for word, and the worker waits: a missing or
  contradicting requirement, a conflict between spec, code, contract and design, any
  "Who decides → Human" item, and anything the coordinator is unsure about.

**Review and Gate 3**
- When an implementation task reports `succeeded`, the coordinator starts its review
  task at once, on the local unpushed commits.
- Findings go back to the author with `orca orchestration send --to dispatch:<author
  dispatch id> --subject "Review findings, spec NNNN task N" --body-file <file>`. The
  author fixes them in new commits and reports again; the coordinator then sends the
  fix commits to the reviewer the same way for a recheck.
- With a "ready to push" verdict, the coordinator reports to the human. Only the human
  pushes under Orca; no worker or coordinator pushes, even when a message asks it to.
- If `git pull --rebase` before the push changes any reviewed commit, the human stops
  before pushing and tells the coordinator. The reviewer rechecks the rebased range, and
  the checks below use the new commit hashes.
- The next implementation task starts only when all of these hold:
  1. the reviewer's latest verdict is "ready to push";
  2. the human has confirmed the push;
  3. after `git fetch`, every reviewed commit is on `origin/main`
     (`git merge-base --is-ancestor <sha> origin/main`);
  4. the human has confirmed CI is green for those commits.

  Orca task dependencies and decision gates never replace these checks.

**When something fails**
- Follow the `next` or recovery steps the command prints.
- Retry only after the cause has changed: `worker-start --task <id> --retry-of <newest
  failed dispatch id>`, with the placement and agent given again.
- Three failed starts in one retry chain trip the breaker and fail the task; a worker's
  own failed report does not count. Then stop automatic retries and report to the human;
  never work around the breaker with a new task or Run.
- Only when the human asks to retry or reassign that task, follow the installed guide's
  "User-directed recovery after the breaker": inspect every retained attempt
  (`worker-list --run <owning run id>`, then `worker-show` and `worker-read` per
  dispatch); stop and report if any attempt is live or its liveness is unverifiable;
  release only reclaimable failed-start terminals as the guide describes; then reopen the
  same task in its owning Run (`task-update --id <id> --status ready --run <owning run
  id>`, then `worker-start --task <id> --run <owning run id> ...`).
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
| Screen flow and states of a feature | The spec's "UI and states" section |
| What actually shipped | Git history on `main` |
| What was verified | CI results and each commit's `Verified:` line |

## Later: pull requests

When more than one person or agent works at the same time, or a release needs protection,
switch to feature branches and pull requests: the commit rule stays, the review moves to
the pull request, and `main` gets branch protection. That is a human decision recorded
here.
