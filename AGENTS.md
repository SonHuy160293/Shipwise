# Shipwise contributor guidance

Read README.md and ARCHITECTURE.md before changes. This repository contains shared
platform code only; business modules and contracts are deliberately absent.

- Follow .editorconfig and centralized package versions; retain net10.0.
- BuildingBlocks may reference only other BuildingBlocks, without cycles.
- Keep custom CQRS pipeline order and transaction ownership; never add MediatR.
- Commands, queries, handlers, validators, controllers, error-to-HTTP mapping and
  repositories follow the cqrs-api skill in .agents/skills/cqrs-api. `ApiController` does
  not require authentication by itself: put `[Authorize]` on every controller.
- Auth must fail closed without an authoritative active-context validator.
- Keep business catalogs, DbContexts, migrations and broker topology out of shared code.
- Database changes (EF Core code-first, PostgreSQL) follow the ef-migrations skill in
  .agents/skills/ef-migrations: naming, column types, commands, expand/contract and tests.
- Preserve telemetry privacy and cancellation. Never log credentials or sensitive payloads.
- Add focused tests for behavior changes. Run scripts/Verify-Platform.ps1 and report
  skipped infrastructure tests separately. Container validation requires a running engine.
- Do not copy source repository Git metadata, secrets, generated outputs, product
  requirements or approval records into this repository.
- This repository owns the HTTP contract consumed by Shipwise-Web. When the first API
  host ships, publish its OpenAPI document at contracts/openapi/shipwise.v1.json; land
  contract changes here before the frontend regenerates its types.
- Hooks and agent settings are approved by a human; never add or change them yourself.

## Roles and ownership

Specs live in docs/specs (written with the write-spec skill) and decisions in docs/adr.
The process (lanes, gates, who decides, changing a spec, closing) is in docs/workflow.md.
Every task comes from a spec's task table, which names its owner, repository, paths and
verification commands; work only on paths your task owns. The one exception is the small
change lane: a change that touches no API contract, database schema, permission rule or
approved screen behavior, and stays inside one role's paths, needs no spec, only a
focused test, a commit under the commit rule and a review.

- Claude Code (planner, BA, solution architect, tech lead): writes specs, ADRs and task
  tables, and reviews every change before it is pushed. Does not write product code.
- Codex (fullstack developer): everything in this repository, plus the Shipwise-Web data
  layer (generated contract, feature API hooks and schemas). Start Codex here with
  `codex --add-dir ../Shipwise-Web` when a task spans both repositories.
- Gemini (frontend developer and designer): works in Shipwise-Web only; it reads specs
  and the contract from this repository but never edits it.
- Order within a feature: challenge brief, draft spec, Gemini's design proposal for new
  pages, the approved spec, then the backend + contract task, then the frontend tasks,
  each reviewed by Claude with the review-change skill before it is pushed.

## Commit rule

No pull requests or feature branches in this phase: commit directly to `main`. The full
rule is in docs/workflow.md.

- Run `git status` before editing. If the working tree has changes you did not make,
  stop and ask; never discard, reset, stash or overwrite them.
- Commit only when the task's verification commands pass; `main` is never left red. One
  logical change per commit; stage explicit paths, never `git add -A`.
- Message: `<type>(<scope>): <summary>` in at most 72 characters (types: feat, fix,
  refactor, perf, test, docs, build, ci, chore), a body saying what and why, then the
  trailers `Spec: NNNN task N` (or `Spec: none (small change)`), `Covers: AC-n, ...` and
  `Verified: <command> PASSED|FAILED; ...`. Add `Not-run:`, `Deviations:` and
  `Migration:` lines when they apply. Never write PASSED for a command you did not run.
- Never amend, rebase or force-push pushed commits, and never use `--no-verify`; fix
  forward with a new commit.
- Do not push. Report the commit hashes and results; the human pushes after the review
  has no open blocker.

## Stop and ask

Stop the affected task and write a decision request (context, options, recommendation,
impact, safe default) instead of guessing when: a requirement is missing or contradicts
another; the spec conflicts with the code, the contract or the design proposal; you find
a cross-organization access, data-loss or destructive-migration risk; the task needs
paths it does not own; or the scope, dependencies or rollout risk grow beyond the spec.
The safe default is not to implement the contested behavior. Never fix a spec or
contract inconsistency silently in code.

## Definition of done

A change is done only when: scripts/Verify-Platform.ps1 passes (CI runs it with
PostgreSQL and RabbitMQ, so infrastructure tests must pass there, not skip); behavior
changes have focused tests; no new analyzer warnings are introduced; migrations pass
the ef-migrations checklist with their SQL script given to the reviewer; the OpenAPI
document is regenerated and committed when the contract changed; a different agent
than the author has reviewed the commits before they are pushed; and the commit
messages follow the commit rule, with the acceptance criteria covered and the actual
commands run and their results, listing skipped tests separately in the handoff report.
