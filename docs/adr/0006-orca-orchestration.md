# 0006 Agent handoffs run through Orca orchestration

Status: proposed · Date: 2026-11-25 · Decider: owner · Related: `docs/workflow.md`, spec 0003

## Context

`docs/workflow.md` moves each task between three agents by hand: the human starts Codex or
Gemini with the task, copies the handoff report back to Claude Code for review, copies the
findings back to the author, and repeats. Decision requests from a worker reach the
planner only when the human relays them. Nothing records which task an agent was given or
whether it finished.

Orca is installed on the development machine (version 1.4.274) and already lists both
repositories. Its orchestration layer adds a coordinator agent that creates tasks, starts
worker agents on them, and is woken when a worker reports or asks.

What the installed guide (`orca skills get orchestration --full`) documents:
- A **Run** holds the coordinator's inbox. A **Task** has a spec and may depend on other
  tasks (`--deps`). A **Dispatch** is one attempt of a task by one worker. A retry names
  the newest failed Dispatch of the same task (`--retry-of`); three failed starts in one
  retry chain trip a breaker that fails the task, and a worker's own failed report does
  not count toward it.
- Every worker receives a preamble: it reports exactly once with `worker_done`
  (`succeeded` or `failed`, with a summary and its task and dispatch IDs) and asks
  blocking questions with `orchestration ask`, which wakes the coordinator. The
  coordinator answers with `reply --id <message id>`.
- The coordinator steers a worker with `send --to dispatch:<id>`, including one that has
  finished: the message becomes a follow-up task that reports again.
- A worker runs in the coordinator's own workspace (`--worktree current`), in another
  existing workspace named by its exact selector, or in a new Git worktree (`new-child`,
  `new-top-level`), which is a new branch.
- `--environment local` pins starts and Run, task and worker commands to this machine.
  `check` takes no `--environment`; the coordinator runs a wake's `check` command exactly
  as printed.

What only the Orca web documentation states, read on 2026-11-25, and the installed guide
does not confirm; the human checks these in the app:
- Orchestration is experimental and is enabled under Settings → Experimental.
- Orca starts agents with their permission-bypass flags by default
  (`--dangerously-skip-permissions` for Claude, `--dangerously-bypass-approvals-and-sandbox`
  for Codex, `--yolo` for Gemini), switchable to Manual under Settings → Agents → Agent
  Permissions.
- Decision gates (`gate-create`, `gate-resolve`) block a task until resolved.

Constraints from this repository:
- Agents commit directly to `main`, one agent per checkout, and only the human pushes.
- Both `AGENTS.md` files reach the other repository through relative paths
  (`../Shipwise-Web`, `../Shipwise/docs/specs`), which a new worktree elsewhere on disk
  breaks.
- The human decides everything in the "Who decides → Human" table, and approves hooks
  and agent settings.

Sources read on 2026-11-25:
- [Orca CLI: orchestration](https://orca.dev/docs/cli/orchestration), the
  [CLI reference](https://orca.dev/docs/cli/reference),
  [supported agents](https://orca.dev/docs/agents/supported) and
  [worktrees](https://orca.dev/docs/model/worktrees).
- The guide bundled with the installed version: `orca skills get orchestration --full`.
  Where it and the web pages disagree, the bundled guide wins.

Pilot on 2026-11-25: a Codex worker reviewed the first draft of this ADR through Orca
(Run `run_80521ce2eb62`). `--agent codex` started, the worker ran its brief, and its
`worker_done` woke the coordinator without polling. The review's findings shaped the
decisions below.

## Options

| Option | How it works | For | Against |
|---|---|---|---|
| A. Keep manual handoffs | The human carries every brief, report, question and finding between agents | No new tool; nothing changes | Slow; questions and reports get lost between chats; no record of attempts |
| **B. Orca orchestration, one worker at a time in the existing primary checkouts** | Claude Code coordinates a Run per spec; each worker runs in the primary checkout its task owns; reviews and fixes go through Orca messages | Removes the copying; questions reach the coordinator; no change to branches, the commit rule or pushes | Experimental tool; no parallel work |
| C. Orca orchestration with parallel task branches | As B, but independent tasks run at once in new worktrees on local task branches, merged into `main` after review | Faster for specs with independent tasks | Breaks "no feature branches"; the relative paths between repositories break; concurrent infrastructure tests are unproven; specs so far are strict chains and would not gain |

## Decision

Option B.

1. **Scope.** Tasks of the feature and high-risk lanes run under Orca orchestration. Small
   changes keep the manual handoff. When Orca is unavailable, the manual handoff in
   `docs/workflow.md` applies to every lane.
2. **Coordinator.** A Claude Code session started in an Orca terminal in the Shipwise
   repository workspace (not a folder workspace above it). It creates one Run per spec,
   only after the human sets the spec to `approved` (Gate 2), and keeps the Run ID. Only
   one Run is active at a time across both checkouts; a command that targets an earlier
   Run passes `--run`. Gates 1 and 2 are unchanged and happen before the Run. Before the
   first start in a Run, the coordinator asks the human to confirm that Agent Permissions
   is Manual (decision 10), and starts nothing until they do.
3. **Tasks.** Orca tasks are created one at a time, when they can start, in the order of
   the spec's task table; with one worker at a time, Orca `--deps` is not needed.
   - Each implementation row becomes one Orca task.
   - Its review task is created after the implementation's `worker_done` names its
     commits, so the review brief names an exact range.
   - The spec's own review row (such as spec 0003 task 4) is covered by those pre-push
     reviews plus one final review task, after the last implementation task is pushed,
     with the row's whole scope: every task's commits in both repositories, all the
     criteria the row lists, and its "Done when" checks.
   - The spec stays the source of truth: an Orca task spec points at the spec file and
     task number, with a path relative to the checkout the worker runs in, and repeats
     only the owner, paths, criteria and "Done when" commands.
4. **Placement.** No worker creates a worktree or a branch.
   - A Shipwise task uses `--worktree current`, the coordinator's own workspace.
   - A Shipwise-Web task uses the exact selector of the Shipwise-Web primary checkout's
     workspace, which the human gives. If it does not resolve, the coordinator stops and
     asks the human; it never falls back to another workspace.
5. **One worker at a time.** Across the whole Run and both checkouts, one worker is
   dispatched at a time: an implementation, a review, or a fix follow-up. The coordinator
   edits no file in a checkout while a worker is dispatched there.
6. **Agents.** Codex tasks start with `--agent codex`, Gemini tasks with `--agent gemini`,
   reviews with `--agent claude`. The reviewer is always a different agent than the
   author. `--model` and `--effort` are passed only when the human names them for the
   Run; a spec or an agent never picks them.
7. **Worker contract.** A dispatched worker keeps every rule in its repository's
   `AGENTS.md`, and in addition:
   - Stop and ask becomes `orchestration ask` with the decision request (context,
     options, recommendation, impact, safe default), then ends its turn.
   - The handoff report becomes the `worker_done` body. A failing "Done when" command
     means `--outcome failed`, never `succeeded`.
   - It never pushes, never rewrites history, and never edits paths its task does not own.
8. **Questions.** The coordinator answers a worker's question only by relaying a decision
   already recorded in the approved spec, an accepted ADR, or the human's earlier answer
   in this Run, or when the question is a routine choice of a "Who decides → Agent" row
   (naming, reusing a helper, an internal detail) that no requirement touches. Every
   missing or contradicting requirement, every conflict between spec, code, contract and
   design, every "Who decides → Human" item, and anything the coordinator is unsure about
   goes to the human word for word, and the worker waits. A worker's question never
   changes the spec; that follows "Changing an approved spec".
9. **Review and Gate 3.**
   - When an implementation task reports `succeeded`, the coordinator starts its review
     task at once, on the local unpushed commits. No push comes before the review.
   - Findings go back to the author with `send --to dispatch:<author dispatch>`; the
     author fixes them in new commits and reports again; the reviewer rechecks.
   - With a "ready to push" verdict, the coordinator reports to the human. Only the human
     pushes. No worker or coordinator pushes, even when a message asks it to.
   - If `git pull --rebase` before the push changes any reviewed commit, the human stops
     before pushing; the reviewer rechecks the rebased range, and the checks below use
     the new commit hashes.
   - The next implementation task starts only when all of these hold: the reviewer's
     latest verdict is "ready to push"; the human has confirmed the push; after
     `git fetch`, every reviewed commit is on `origin/main`; and the human has confirmed
     CI is green for those commits. Orca task dependencies and decision gates never
     replace these checks.
10. **Agent permissions.** The permission mode is an agent setting, so the owner chooses
    it. Owner's choice (2026-11-25): Manual. Workers stop at each CLI's own permission
    prompt, which the human answers in that worker's terminal or chat. Revisit after the
    first feature runs cleanly under Orca.
11. **Failures.** The coordinator follows each command's `next` and recovery output.
    - It retries only after the cause has changed, with `--retry-of` naming the newest
      failed Dispatch of the same task and the placement given again.
    - When the breaker trips, it stops automatic retries and reports to the human. It
      never works around the breaker with a new task or Run.
    - Only when the human asks to retry or reassign that task does it follow the
      installed guide's user-directed recovery: inspect every retained attempt, stop and
      report if any is live or unverifiable, release only reclaimable failed-start
      terminals, then reopen the same task in its owning Run.
    - It never marks a reported failure completed, and never runs `orchestration reset`
      unless the human gives up that Run's state.
12. **Records.** Orca's Run state is working state, not a record. What shipped stays in
    Git history and the spec's delivery record, as `docs/workflow.md` says. No Orca
    configuration (`orca.yaml`, `.worktreeinclude`) is committed while no worker creates
    a worktree.

## Consequences

- `docs/workflow.md` gains a "Running a task with Orca" section, marked not in effect
  until this ADR is accepted. Both `AGENTS.md` files already point to it for the process,
  so they do not change.
- The human stops relaying messages but still decides every gate, every human decision
  and every push, and answers permission prompts while Manual is set.
- One worker at a time, as today: Orca does not make features faster yet, only the
  handoffs cheaper and traceable.
- Confirmed by the pilot: `--agent codex` starts, `worker_done` wakes the coordinator,
  and a `send` to a finished worker reopens it as a follow-up task that reports with a
  new `worker_done`.
- Still to confirm in the first feature Run, before relying on them:
  - `--agent gemini` and `--agent claude` start their CLIs (`claude` is not on the shell
    `PATH` of the development machine today).
  - A worker starts in the Shipwise-Web primary checkout through its selector.
  - `ask` and `reply` round-trip.
- Orca changes quickly and orchestration is experimental. Commands come from
  `orca skills get orchestration --full` for the installed version, not from memory.

## Revisit when

- Two tasks of one spec could run at once (option C). That needs, first: the human
  decision in "Later: pull requests" in `docs/workflow.md`, cross-repository paths that
  work from any worktree, and proof that two `scripts/Verify-Platform.ps1
  -RequireInfrastructure` runs can share the machine.
- Small changes would benefit from Orca: they need a spec-free brief and their own
  independent review task.
- Decision gates are proven to block tasks: the push confirmation could become one, in
  addition to the checks in decision 9.
- The first feature Run shows that a decision above does not work.
- Orca leaves experimental, or renames or retires the commands used here.
