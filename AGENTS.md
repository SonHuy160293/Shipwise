# Shipwise contributor guidance

Read README.md and ARCHITECTURE.md before changes. This repository contains shared
platform code only; business modules and contracts are deliberately absent.

- Follow .editorconfig and centralized package versions; retain net10.0.
- BuildingBlocks may reference only other BuildingBlocks, without cycles.
- Keep custom CQRS pipeline order and transaction ownership; never add MediatR.
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
Every task comes from a spec's task table, which names its owner, repository, paths and
verification commands; work only on paths your task owns.

- Claude Code (planner, BA, solution architect, tech lead): writes specs, ADRs and task
  tables, and reviews every pull request. Does not write product code.
- Codex (fullstack developer): everything in this repository, plus the Shipwise-Web data
  layer (generated contract, feature API hooks and schemas). Start Codex here with
  `codex --add-dir ../Shipwise-Web` when a task spans both repositories.
- Gemini (frontend developer and designer): works in Shipwise-Web only; it reads specs
  and the contract from this repository but never edits it.
- Order within a feature: spec, then the backend + contract pull request, then the
  frontend pull requests, each reviewed by Claude.

## Definition of done

A change is done only when: scripts/Verify-Platform.ps1 passes (CI runs it with
PostgreSQL and RabbitMQ, so infrastructure tests must pass there, not skip); behavior
changes have focused tests; no new analyzer warnings are introduced; migrations pass
the ef-migrations checklist with their SQL script in the pull request; the OpenAPI
document is regenerated and committed when the contract changed; a different agent
than the author has reviewed the diff; and the handoff reports the actual commands
run and their results, listing skipped tests separately.
