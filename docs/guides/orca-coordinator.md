# Starting an Orca coordinator session

The coordinator is the Claude Code session that runs a spec's Orca Run (ADR 0006, ADR 0008).
The process is in [docs/workflow.md](../workflow.md), section "Running a task with Orca".
This page is the practical part for the human.

## When to start a new session

- A new spec is approved and needs its own Run.
- The coordinator rules or the workflow changed (the prompt file or `docs/workflow.md`).

Within one spec, keep the same session. It holds the Run ID and carries on from the
reports you send it.

## Steps

1. In Orca, open the **Shipwise → main** workspace (`D:/Projects/Shipwise/Shipwise`).
   Not a folder workspace above it.
2. Open a new Claude agent tab there. Make sure it opens as a **chat**. If it opens as a
   terminal, set Claude's Chat | CLI option to Chat in Orca's settings.
3. Paste this prompt once:

   ```text
   You are the Orca coordinator for Shipwise. Read D:\Projects\Shipwise\orca-briefs\coordinator-generic.md and follow it exactly. Start by asking me for the spec path, the Shipwise-Web selector if the spec has frontend tasks, and confirmation that Agent Permissions is Manual.
   ```

4. Answer its three questions: the spec path, the Shipwise-Web selector
   (`id:3e7da8c7-7a81-44ba-b7ee-490fccf29c1d::D:/Projects/Shipwise/Shipwise-Web`), and
   `yes` to Manual permissions.

## During the Run

- Codex's permission prompts appear in that worker's own tab. Approve them there.
- Paste reports and questions from workers into the coordinator chat when it asks, or
  bring them to the planner.
- You push both repositories yourself, after the final review says "ready to push" and
  CI is green. The coordinator never pushes.

## Closing

When the spec is `done`, end the coordinator session. The Run stays in Orca's history and
does not need resetting.
