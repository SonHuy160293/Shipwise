# Modules

Each module is a folder `Shipwise.<Module>/` with Domain, Application, Infrastructure, API
and MigrationRunner projects, plus a Worker once it publishes or consumes messages. Domain
depends only on BuildingBlocks; Application depends inward on Domain and neutral contracts;
Infrastructure implements Application abstractions; executable hosts compose them. Do not
add references from BuildingBlocks to modules, or from one module to another.

| Module | State |
|---|---|
| `Shipwise.Identity` | Base skeleton: hosts, wiring, database context and unit of work, with tests in `tests/Shipwise.Identity.Tests`. No entity, migration or endpoint yet. |

Create a module with the module-skeleton skill (`.agents/skills/module-skeleton`), which
uses the Identity skeleton as its reference. Business code inside a module follows the
cqrs-api and ef-migrations skills. `scripts/Test-Architecture.ps1` enforces the project
references and still rejects modules named `Notification` or `Projects`.
