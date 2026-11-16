---
name: module-skeleton
description: Mandatory rules for creating the base skeleton of a new Shipwise backend module - project set and references, folder layout, naming, host composition, required configuration, persistence base, MigrationRunner, test project and the checks that prove it works - before any business feature exists. Use whenever you plan, create or review a new module under src/Modules, add a new host (API, Worker, MigrationRunner) to a module, or write the spec for one. Business code inside the module follows the cqrs-api and ef-migrations skills.
---

# Module skeleton rules

A skeleton is the empty, running shell of a module: its projects, wiring, hosts and tests,
with no entity, no migration, no endpoint and no business rule. Its job is to prove the
whole path works (build, architecture check, host start, health, request pipeline,
authentication, database wiring, migration tooling, tests, CI) so the first feature only
adds business code.

The layout follows the owner's reference structure (the DeliveryPilot Identity module),
corrected to Shipwise rules. The Shipwise Identity module (`src/Modules/Shipwise.Identity`,
`tests/Shipwise.Identity.Tests`) was created from these rules and is the reference:
[templates.md](templates.md) is a snapshot of it with the module renamed. That skeleton
built with no analyzer warning, passed `scripts/Verify-Platform.ps1 -RequireInfrastructure`
with no skipped test, and its MigrationRunner and API were run as processes against
PostgreSQL. What is still unproven is listed in section 9.

## 1. Before you start

- A new module is never a small change: it needs an approved spec (write-spec skill) that
  states the module name, what data and behavior it will own, which hosts it needs now,
  and any new package.
- New packages are a human decision. A skeleton needs
  `Microsoft.EntityFrameworkCore.Design` (EF tooling) and
  `Microsoft.AspNetCore.Mvc.Testing` (host tests); if `Directory.Packages.props` lacks
  them, the spec must approve them.
- `scripts/Test-Architecture.ps1` holds a list of module names it rejects outright
  (`Notification` and `Projects`, left over from the extraction; `Identity` was removed
  when that module was created). Creating one of those modules includes removing its name
  from that list, stated in the spec.
- Read the cqrs-api and ef-migrations skills first; this skill only adds what is specific
  to creating a module.

## 2. Names

| Thing | Rule | Example |
|---|---|---|
| Module name | One PascalCase noun for a bounded context | `Identity` |
| Folder | `src/Modules/Shipwise.<Module>/` | `src/Modules/Shipwise.Identity/` |
| Projects | `Shipwise.<Module>.<Layer>`; the layer names are fixed because the architecture script matches them | `Shipwise.Identity.Application` |
| Root namespace | Same as the project name | `Shipwise.Identity.Application` |
| Database | `shipwise_<module>`, lowercase | `shipwise_identity` |
| Schema | `<module>`, lowercase, as `const string Schema` on the DbContext | `identity` |
| DbContext | `<Module>DbContext`, `<Module>DbContextFactory`, `<Module>UnitOfWork` | `IdentityDbContext` |
| Connection string | `ConnectionStrings:ApplicationDb` in every host of the module | |
| User secrets id | `shipwise-<module>-api`, lowercase, on the API project only | `shipwise-identity-api` |
| Local port | The next free port after the Gateway's 19090, in `launchSettings.json` | Identity uses 19091 |
| Test project | `tests/Shipwise.<Module>.Tests` | `tests/Shipwise.Identity.Tests` |

## 3. Projects

Create only the hosts the spec asks for. Domain, Application, Infrastructure, API,
MigrationRunner and the test project are the normal minimum.

| Project | SDK | References | Packages |
|---|---|---|---|
| Domain | `Microsoft.NET.Sdk` | SharedKernel | none |
| Application | `Microsoft.NET.Sdk` | Domain, SharedKernel, Contracts when needed | none |
| Infrastructure | `Microsoft.NET.Sdk` + `FrameworkReference Microsoft.AspNetCore.App` | Application, Domain, Shipwise.EntityFrameworkCore (Shipwise.Messaging only with a Worker) | `Npgsql.EntityFrameworkCore.PostgreSQL`, `Microsoft.EntityFrameworkCore.Design` (`PrivateAssets=all`) |
| API | `Microsoft.NET.Sdk.Web` | Application, Infrastructure, Shipwise.AspNetCore, Shipwise.Observability | none |
| MigrationRunner | `Microsoft.NET.Sdk`, `OutputType=Exe` | Infrastructure, Shipwise.Observability | none |
| Worker (only when the module publishes or consumes a message) | `Microsoft.NET.Sdk`, `OutputType=Exe` | Application, Infrastructure, Shipwise.Observability | none |
| Tests | `Microsoft.NET.Sdk`, `IsTestProject=true` | API | `Microsoft.NET.Test.Sdk`, `Microsoft.AspNetCore.Mvc.Testing`, `xunit`, `xunit.runner.visualstudio` |

- Project files carry no `TargetFramework`, `Nullable`, `ImplicitUsings` or package
  versions: `Directory.Build.props` and `Directory.Packages.props` supply them.
- Application never references `Shipwise.AspNetCore` or Infrastructure; API never
  references Domain directly; nothing references another module. The architecture script
  enforces this.
- Add every project to `Shipwise.slnx` under a `/src/Modules/<Module>/` folder (tests under
  `/tests/`), or `Verify-Platform.ps1` will not build or test it.

## 4. Folder layout

```
src/Modules/Shipwise.<Module>/
  Shipwise.<Module>.Domain/
    AssemblyReference.cs
    Models/                          aggregates and value objects (empty in a skeleton)
  Shipwise.<Module>.Application/
    AssemblyReference.cs
    DependencyInjection/ServiceCollectionExtensions.cs     AddApplication()
    Abstractions/                    Repositories/, Security/, Messaging/ ... as they appear
    Features/<Area>/Commands/<UseCase>/                     request, handler, validator
    Features/<Area>/Queries/<UseCase>/                      request, handler, validator, response
  Shipwise.<Module>.Infrastructure/
    DependencyInjection/ServiceCollectionExtensions.cs     AddInfrastructure(), AddPersistence(), AddUnitOfWork()
    Persistence/
      DbContexts/<Module>DbContext.cs, <Module>DbContextFactory.cs
      Core/<Module>UnitOfWork.cs
      Configurations/                one IEntityTypeConfiguration<T> per entity
      Migrations/                    generated by dotnet ef only
      Repositories/
  Shipwise.<Module>.API/
    Program.cs
    appsettings.json                 no secrets
    Properties/launchSettings.json
    Health/DatabaseHealthCheck.cs
    Controllers/
    Contracts/<Area>/                HTTP request models
  Shipwise.<Module>.MigrationRunner/
    Program.cs
tests/Shipwise.<Module>.Tests/
```

Empty folders are not committed; a folder appears with its first file. Spell
`DependencyInjection` correctly (the reference module has a typo). One type per file,
file-scoped namespace matching the folder.

## 5. What each piece must do

Copy the code from [templates.md](templates.md) and replace `Sample` and `sample`.

- **`AddApplication()`** registers CQRS from the Application assembly and the pipeline
  with `AddApplicationPipelineBehaviors()`, once.
- **`AddPersistence(configuration)`** throws when `ConnectionStrings:ApplicationDb` is
  missing, registers `AuditSaveChangesInterceptor` as scoped and adds it to the DbContext,
  and sets the migrations history table to `__EFMigrationsHistory` in the module schema.
- **DbContext** calls `HasDefaultSchema(Schema)` and `ApplyConfigurationsFromAssembly`. It
  adds `ApplyReliabilityConfigurations(...)` only when the module gets a Worker.
- **Unit of work** opens a read-committed transaction, saves and commits only when the
  response is not a failed `Result`, and otherwise rolls back and clears the change
  tracker. On an exception it lets the disposed transaction roll back instead of rolling
  back explicitly, so a rollback error cannot hide the original failure. It contains no business constraint names; map a specific database error to a
  domain exception in the feature that owns the constraint. Do not enable
  `EnableRetryOnFailure`: it conflicts with this explicit transaction.
- **Design-time factory** reads `SHIPWISE_DESIGN_POSTGRES` and falls back to the local
  compose server without a password. It is used by `dotnet ef` only.
- **API `Program.cs`** composes in the template's order. Rules that are easy to get wrong:
  - `UseGlobalExceptionHandling()` comes before `UseApiStatusCodeEnvelope()`, both before
    authentication.
  - Health endpoints are `/health/live` (process only) and `/health/ready` (database
    reachable), both `.AllowAnonymous()`, outside `/api`.
  - `UseConfiguredSwagger()` has no environment check of its own: call it only in
    Development.
  - Do not register an `IAuthenticatedContextValidator` in a skeleton. Without one every
    token is rejected, which is the correct fail-closed state until the identity model
    exists.
  - API and Worker never call `MigrateAsync` or `EnsureCreated`.
- **MigrationRunner** applies migrations and exits with 0, or logs the exception type only
  and exits with 1. It reads `ConnectionStrings:ApplicationDb` from the environment
  (`ConnectionStrings__ApplicationDb`) or the command line; it has no user secrets, since
  a plain console project does not load them without an extra package. `MigrateAsync`
  creates the database when it does not exist.
- No controller, command, query, entity or migration ships in a skeleton.

## 6. Required configuration

The shared helpers validate these at startup; a host missing any of them does not start.

| Key | Required by | Rule |
|---|---|---|
| `ConnectionStrings:ApplicationDb` | `AddPersistence` | Module database; never committed with a password |
| `Jwt:SecretKey`, `Jwt:Issuer`, `Jwt:Audience`, `Jwt:AccessTokenExpirationMinutes` | `AddConfiguredAuthentication` | Key is at least 32 bytes; from user secrets or a secret file, never `appsettings.json` |
| `Cors:AllowedOrigins`, `Cors:AllowedMethods`, `Cors:AllowedHeaders` | `AddConfiguredCors` | At least one of each; no `*` origin |
| `IntegrationApi:ApiKeys` | `AddConfiguredAuthorization` | At least one key, even when the module has no integration endpoint; a secret |
| `Observability:*`, `Serilog:*` | optional | Copy the Gateway's sections |

Secrets come from user secrets locally and from files under
`SHIPWISE_CONFIG_SECRETS_DIRECTORY` (default `/run/secrets/config`) when deployed. Commit
an `appsettings.json` with the non-secret sections only, and document every key in the
module's section of `README.md`.

## 7. Tests the skeleton ships

Use `WebApplicationFactory<Program>` (`Program` is public automatically on .NET 10). Pass
test settings with `builder.UseSetting(key, value)`: the host reads configuration while it
registers services, so the values must exist before `Program` runs. To exercise the
pipeline without shipping an endpoint, the test project adds its own controller, query,
command and validator through `ConfigureTestServices` (`AddApplicationPart` plus
`AddSelfMadeCqrs` for the test assembly), a fake `IAuthenticatedContextValidator`, and
tokens signed with the test key.

Required, no database needed:
- `/health/live` answers 200 without credentials; `/health/ready` answers 503 when the
  database is unreachable.
- A controller deriving from `ApiController` with no attribute of its own answers 401
  without a token, 403 with a token that lacks the permission, and 200 with it.
- A query runs through the pipeline; `NotFoundException` becomes 404 with its code.
- An invalid command answers 400 with field-keyed errors.
- `context.Database.HasPendingModelChanges()` is false and the default schema is the
  module schema.

Required, with PostgreSQL (`[PostgreSqlFact]`, must run in CI):
- `MigrateAsync` on an empty database succeeds and creates
  `<schema>."__EFMigrationsHistory"`. Use a database created for the test run on the
  `SHIPWISE_TEST_POSTGRES` server and dropped afterwards, not the shared test database.
- A command that throws, and one that returns a failed `Result`, leave no change behind;
  a successful one commits.
- `/health/ready` answers 200.

## 8. Done when

- [ ] Spec approved; module name, hosts and packages match it
- [ ] Names, references and folders follow sections 2 to 4; every project is in
      `Shipwise.slnx`
- [ ] `scripts/Verify-Platform.ps1` passes, including the architecture check, with no new
      analyzer warning from the new projects
- [ ] `dotnet ef migrations has-pending-model-changes` prints no changes
- [ ] The tests in section 7 pass, and the PostgreSQL ones run in CI without being skipped
- [ ] The API starts locally with user secrets and both health endpoints answer
- [ ] No secret, entity, migration, endpoint or business rule was added
- [ ] `README.md`, `ARCHITECTURE.md`, `AGENTS.md` and `src/Modules/README.md` describe the
      new module, its hosts and its configuration keys
- [ ] Commits follow the commit rule, one logical step each (projects, hosts, tests, docs)

## 9. Not covered by the Identity skeleton

State these as unverified in the handoff report of the work that first needs them:
- Reading secrets from the secrets directory (the code path exists; no test or run used it).
- User secrets on a developer machine: the Identity runs passed settings through
  environment variables.
- A Worker. Add one only when the module publishes or consumes its first message, in the
  spec that introduces that message: it needs the reliability tables in a migration, the
  outbox relay, broker topology and the contracts in `src/Contracts`. Read
  `Shipwise.Messaging` and the cqrs-api skill section 8 first.
- A Docker image, a compose service and a Gateway route. They belong to the spec that
  first exposes an endpoint.

Known platform limits to plan around: the Swagger title is fixed in
`Shipwise.AspNetCore` and cannot be set per module, and HTTPS redirection is not part of
the template (decide it with the deployment topology).
