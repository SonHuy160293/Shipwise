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

What it provides (sources below):
- A **Run** holds the coordinator's inbox. A **Task** has a spec and may depend on other
  tasks (`--deps`). A **Dispatch** is one attempt of a task by one worker; a retry is a
  new Dispatch, and three failed starts in one retry chain fail the task.
- Every worker receives a preamble: it reports exactly once with `worker_done`
  (`succeeded` or `failed`, with a summary and its task and dispatch IDs) and asks
  blocking questions with `orchestration ask`, which wakes the coordinator.
- The coordinator steers a worker with `send --to dispatch:<id>`, including one that has
  finished: the message becomes a follow-up task that reports again.
- A worker runs in the caller's checkout (`--worktree current`) or in a new Git worktree
  (`new-child`, `new-top-level`), which is a new branch.

Constraints from this repository:
- Agents commit directly to `main`, one agent per checkout, and only the human pushes.
- Both `AGENTS.md` files reach the other repository through relative paths
  (`../Shipwise-Web`, `../Shipwise/docs/specs`), which a new worktree elsewhere on disk
  breaks.
- The human decides everything in the "Who decides → Human" table, and approves hooks
  and agent settings.
- Orca starts agents with their permission-bypass flags by default
  (`--dangerously-skip-permissions` for Claude, `--dangerously-bypass-approvals-and-sandbox`
  for Codex, `--yolo` for Gemini). It can be set to Manual under Settings → Agents →
  Agent Permissions.
- Orchestration is marked experimental and must be enabled under Settings → Experimental.

Sources read on 2026-11-25:
- [Orca CLI: orchestration](https://orca.dev/docs/cli/orchestration), the
  [CLI reference](https://orca.dev/docs/cli/reference),
  [supported agents](https://orca.dev/docs/agents/supported) and
  [worktrees](https://orca.dev/docs/model/worktrees).
- The guide bundled with the installed version: `orca skills get orchestration --full`.
  Where it differs from the web pages, the bundled guide wins.

## Options

| Option | How it works | For | Against |
|---|---|---|---|
| A. Keep manual handoffs | The human carries every brief, report, question and finding between agents | No new tool; nothing changes | Slow; questions and reports get lost between chats; no record of attempts |
| **B. Orca orchestration, one task at a time in the existing checkouts** | Claude Code coordinates a Run per spec; workers use `--worktree current` in the primary checkouts; reviews and fixes go through Orca messages | Removes the copying; questions reach the coordinator; no change to branches, the commit rule or pushes | Experimental tool; no parallel work; still one agent per checkout |
| C. Orca orchestration with parallel task branches | As B, but independent tasks run at once in new worktrees on local task branches, merged into `main` after review | Faster for specs with independent tasks | Breaks "no feature branches"; the relative paths between repositories break; concurrent infrastructure tests are unproven; specs so far are strict chains and would not gain |

## Decision

Option B.

1. **Scope.** Tasks of the feature and high-risk lanes run under Orca orchestration. A
   small change may run as a single worker the same way. When Orca is unavailable, the
   manual handoff in `docs/workflow.md` still applies.
2. **Coordinator.** A Claude Code session started in an Orca terminal on the Shipwise
   primary checkout. It creates one Run per spec, and only after the human sets the spec
   to `approved` (Gate 2). Gates 1 and 2 are unchanged and happen before the Run.
3. **Tasks.** Each row of the spec's task table becomes one Orca task, and each
   implementation task gets its own review task (review-change skill) that depends on it.
   The spec stays the source of truth: an Orca task spec points at the spec file and task
   number and repeats only the owner, paths, criteria and "Done when" commands.
4. **Placement.** Every worker uses an existing primary checkout: `--worktree current`
   for Shipwise, and the Shipwise-Web primary checkout's worktree selector for
   Shipwise-Web. No worker creates a worktree or a branch. One worker works in a checkout
   at a time, and the coordinator edits no file in a checkout while a worker is
   dispatched there.
5. **Agents.** Codex tasks start with `--agent codex`, Gemini tasks with `--agent gemini`,
   reviews with `--agent claude`. The reviewer is always a different agent than the author.
   `--model` and `--effort` are passed only when the spec or the human names them.
6. **Worker contract.** A dispatched worker keeps every rule in its repository's
   `AGENTS.md`, and in addition:
   - Stop and ask becomes `orchestration ask` with the decision request (context,
     options, recommendation, impact, safe default), then ends its turn.
   - The handoff report becomes the `worker_done` body. A failing "Done when" command
     means `--outcome failed`, never `succeeded`.
   - It never pushes, never rewrites history, and never edits paths its task does not own.
7. **Questions.** The coordinator answers a worker's question only when the approved spec
   or the "Who decides → Agent" rows settle it. Anything else is forwarded to the human
   word for word, and the worker waits. A worker's question never changes the spec; that
   follows "Changing an approved spec".
8. **Review and Gate 3.** When an implementation task reports `succeeded`, the coordinator
   starts its review task. Findings go back to the author with `send --to
   dispatch:<id>`, fixed in new commits, and rechecked by the reviewer. With no open
   blocker, the coordinator reports to the human, who pushes. The coordinator starts a
   dependent task only after the human confirms the push and `git fetch` shows the
   commits on `origin/main`. Task dependencies in Orca alone never count as "pushed".
9. **Failures.** The coordinator follows each command's `next` and recovery output. It
   retries a dispatch only after the cause has changed, never marks a reported failure
   completed, never works around the three-failure limit with a new task or Run, and
   never runs `orchestration reset` unless the human gives up that Run's state.
10. **Agent permissions.** The permission mode is an agent setting, so the owner chooses
    it and records it here before this ADR is accepted. Recommended: Manual until one
    feature has run cleanly under Orca, because a worker with bypass flags could push or
    edit outside its paths with nothing to stop it.
    Owner's choice (2026-11-25): Manual. Workers stop at each CLI's own permission
    prompt, which the human answers in that worker's terminal or chat. Revisit after the
    first feature runs cleanly under Orca.
11. **Records.** Orca's Run state is working state, not a record. What shipped stays in
    Git history and the spec's delivery record, as `docs/workflow.md` says. No Orca
    configuration (`orca.yaml`, `.worktreeinclude`) is committed while no worker creates
    a worktree.

## Consequences

- `docs/workflow.md` gains a "Running a task with Orca" section. Both `AGENTS.md` files
  already point to it for the process, so they do not change.
- The human stops relaying messages but still decides every gate, every human decision
  and every push.
- One task at a time, as today: Orca does not make features faster yet, only the
  handoffs cheaper and traceable.
- Things to confirm in the first Run, before relying on them:
  - The agent ids `gemini` and `claude` start their CLIs (`claude` is not on the shell
    `PATH` of the development machine today).
  - A worker starts in the Shipwise-Web primary checkout through its worktree selector.
  - Orca decision gates block a task the way the docs suggest. If they do, the push
    confirmation in decision 8 can become a gate the human resolves.
- Orca changes quickly and orchestration is experimental. Commands come from
  `orca skills get orchestration --full` for the installed version, not from memory.

## Revisit when

- Two tasks of one spec could run at once (option C). That needs, first: the human
  decision in "Later: pull requests" in `docs/workflow.md`, cross-repository paths that
  work from any worktree, and proof that two `scripts/Verify-Platform.ps1
  -RequireInfrastructure` runs can share the machine.
- The first Run shows that a decision above does not work.
- Orca leaves experimental, or renames or retires the commands used here.
