---
name: ef-migrations
description: Mandatory rules for Shipwise database changes with EF Core code-first on PostgreSQL. Use whenever you add or change an entity, DbContext, entity configuration, index, constraint, seed data or migration in a Shipwise module, review such a change, or plan a spec that changes the database. Covers naming conventions, column types, migration commands, safe (expand/contract) changes, tests and the review checklist.
---

# ef-migrations (Claude Code entry point)

The rules live in `.agents/skills/ef-migrations/SKILL.md`, shared with Codex. Read it and
follow it; when reviewing, check the commits against its section 7 checklist.
