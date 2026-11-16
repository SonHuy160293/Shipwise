---
name: cqrs-api
description: Mandatory rules for Shipwise backend application and API code - commands, queries, handlers, validators, Result and error handling, controllers, HTTP status codes and the response envelope, authorization, pipeline and transactions, repositories and host registration. Use whenever you add or change a command, query, handler, validator, controller, endpoint, repository or API host in a Shipwise module, review such a change, or write the API contract and backend design of a spec. Database mapping and migrations are in the ef-migrations skill.
---

# cqrs-api (Claude Code entry point)

The rules live in `.agents/skills/cqrs-api/SKILL.md`, shared with Codex. Read it and follow
it. When writing a spec, take status codes, the response envelope, error codes and
permission names from its sections 4 and 5; when reviewing, check the commits against its
section 10 checklist.
