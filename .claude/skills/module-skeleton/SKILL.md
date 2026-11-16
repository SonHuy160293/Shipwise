---
name: module-skeleton
description: Mandatory rules for creating the base skeleton of a new Shipwise backend module - project set and references, folder layout, naming, host composition, required configuration, persistence base, MigrationRunner, test project and the checks that prove it works - before any business feature exists. Use whenever you plan, create or review a new module under src/Modules, add a new host (API, Worker, MigrationRunner) to a module, or write the spec for one. Business code inside the module follows the cqrs-api and ef-migrations skills.
---

# module-skeleton (Claude Code entry point)

The rules live in `.agents/skills/module-skeleton/SKILL.md`, with the verified code in
`.agents/skills/module-skeleton/templates.md`, shared with Codex. Read them and follow
them. When writing the spec for a new module, take names, projects, hosts, configuration
keys and tests from its sections 2 to 7; when reviewing, check the commits against its
section 8 checklist.
