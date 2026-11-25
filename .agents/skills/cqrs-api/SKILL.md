---
name: cqrs-api
description: Mandatory rules for Shipwise backend application and API code - commands, queries, handlers, validators, Result and error handling, controllers, HTTP status codes and the response envelope, authorization, pipeline and transactions, repositories and host registration. Use whenever you add or change a command, query, handler, validator, controller, endpoint, repository or API host in a Shipwise module, review such a change, or write the API contract and backend design of a spec. Database mapping and migrations are in the ef-migrations skill.
---

# CQRS and API rules

These rules extend `AGENTS.md`. Every statement about platform behavior below was checked
against the BuildingBlocks source and, where marked "verified", against a running probe on
the real base controller, filter, middleware and pipeline. The platform was extracted from
another product: ignore examples, paths or type names from DeliveryPilot or DevFlow.

The Identity module is a base skeleton with no use case yet, so there is no business code to
copy from. When a module has use cases, read its nearest one first and stay consistent with
it; if it contradicts these rules, stop and ask (`AGENTS.md`, "Stop and ask").

## 1. What the platform provides

| Need | Type | Where |
|---|---|---|
| Send a request | `ISender.SendAsync(request, cancellationToken)` | `Shipwise.SharedKernel.Cqrs` |
| Command, no value | `ICommand` handled by `ICommandHandler<TCommand>`, returns `Result` | `Shipwise.SharedKernel.Abstractions.Message` |
| Command with value | `ICommand<T>` handled by `ICommandHandler<TCommand, T>`, returns `Result<T>` | same |
| Query | `IQuery<T>` handled by `IQueryHandler<TQuery, T>`, returns `Result<T>` | same |
| Handler method | `Task<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken)` | same |
| Validator | `IValidator<TRequest>` with `Task ValidateAsync(request, cancellationToken)`; it fails by throwing | same (`Pipeline.cs`) |
| Outcome | `Result`, `Result<T>`, `Error(code, message)` | `Shipwise.SharedKernel.Abstractions.Shared` |
| Expected failures | `NotFoundException`, `ConflictException`, `ForbiddenException`, `UnauthorizedException`, `BadRequestException`, `ServiceUnavailableException`, `ValidationException` | `Shipwise.SharedKernel.Exceptions` |
| Guards | `Guard.Require`, `Guard.Id`, `Guard.Text`, `Guard.OptionalText`, `Guard.DefinedEnum` (throw `ValidationException` with the field name) | `Shipwise.SharedKernel.Validation` |
| Paging | `PagedRequest` (default 20, capped at 100), `PagedResult<T>` | `Shipwise.SharedKernel.Abstractions.Shared` |
| Current user | `ICurrentUserProvider.GetCurrentUser()` (scoped, may return null) | `Shipwise.SharedKernel.Security` |
| Controllers | `ApiController`, `PublicApiController`, `IntegrationApiController` | `Shipwise.AspNetCore.Controllers` |
| Permission check | `[PermissionAuthorize(function, ActionType.X)]` | `Shipwise.AspNetCore.Security` |
| Persistence | `IUnitOfWork`, `IRepositoryBase<TEntity, TId>`, `IReadRepositoryBase<TDto, TId, TRequest>`, `IDeletableRepository<TEntity, TId>` | `Shipwise.SharedKernel.Abstractions.Persistence` |
| EF helpers | `EfRepositoryBase<TEntity, TId, TDbContext>` (protected helpers; implements no interface) | `Shipwise.EntityFrameworkCore.Repositories` |

Not in the platform, and not to be added without a human decision: MediatR, FluentValidation,
AutoMapper or Mapster, Dapper. Map by hand; validate with `IValidator<T>` and `Guard`.

## 2. Layout and naming

Project names are fixed, because `scripts/Test-Architecture.ps1` enforces references by
name: `Shipwise.<Module>.Domain`, `.Application`, `.Infrastructure`, `.API`, `.Worker`,
`.MigrationRunner`.

| Project | May reference | Holds |
|---|---|---|
| Domain | BuildingBlocks only | Aggregates, value objects, invariants, `<Resource>Errors`. No EF, no HTTP |
| Application | Domain, Contracts (never `Shipwise.AspNetCore`, never Infrastructure) | Commands, queries, handlers, validators, response DTOs, repository and service interfaces |
| Infrastructure | Application, Domain | DbContext, mappings, repositories, `IUnitOfWork`, external services |
| API | Application, Infrastructure, Contracts (not Domain directly) | Controllers, HTTP request models, host composition |

```
Shipwise.<Module>.Application/
  Features/<Area>/                           e.g. Features/Organizations/
    Commands/<UseCase>/                      e.g. Commands/CreateOrganization/
      CreateOrganizationCommand.cs           public sealed record ... : ICommand<Guid>
      CreateOrganizationCommandHandler.cs    sealed class ... : ICommandHandler<CreateOrganizationCommand, Guid>
      CreateOrganizationCommandValidator.cs  sealed class ... : IValidator<CreateOrganizationCommand>
    Queries/<UseCase>/                       e.g. Queries/GetOrganization/
      GetOrganizationQuery.cs                public sealed record ... : IQuery<OrganizationResponse>
      GetOrganizationQueryHandler.cs
      OrganizationResponse.cs                response DTO (record), local to the use case
  Abstractions/<Kind>/                       Repositories/, Security/, Messaging/: ports the handlers need
Shipwise.<Module>.API/
  Controllers/<Resources>Controller.cs
  Contracts/<Area>/<UseCase>Request.cs       HTTP body model, separate from the command
```

Keep a use case's request, handler, validator and local response DTOs together in its
folder. `Contracts/` in an API project holds HTTP request models only; cross-module
contracts live in `src/Contracts`. The projects themselves, their wiring and hosts are
created by the module-skeleton skill.

- One type per file, file-scoped namespaces, `sealed` unless inheritance is designed.
- Names: `<Verb><Noun>Command`, `Get<Noun>Query` or `List<Nouns>Query`, plus `Handler`,
  `Validator`, `<Noun>Response`. Exactly one handler per request type.
- Modules talk to each other only through `src/Contracts` or messaging. Another module's
  id is a plain `Guid` column: no navigation, no foreign key, no reference to its Domain,
  DbContext or Infrastructure.

## 3. Handlers and validators

- A handler does one use case: load, call the domain, hand the result back. Invariants live
  in the domain object, not in the handler and not in the controller.
- Pass `cancellationToken` to every async call. Never use `.Result`, `.Wait()` or
  `CancellationToken.None` in request code.
- Take the caller's identity and organization from `ICurrentUserProvider`, never from the
  request body. A null current user in a protected use case is `UnauthorizedException`.
- Queries never change state, and return response DTOs projected in the query (`Select`),
  never entities. List queries take a `PagedRequest` subtype and return `PagedResult<T>`;
  use `GetNormalizedPageSize()` and `GetSkip()`. `SortBy` is matched against a fixed list
  of allowed names; it is never passed into SQL or dynamic LINQ.
- Validators check the shape of the input (required, length, format, ranges). Rules that
  need the database (uniqueness, existence, permissions) belong in the handler or domain.
- A validator collects every problem, then throws once:
  `throw new ValidationException([new ValidationError("displayName", "..."), ...])`, with
  the field named as in the JSON request (camelCase). Verified: the client receives
  `errors: { "displayName": ["..."] }`. `CqrsValidationException` takes plain strings and
  produces `errors` keyed by the message text, so use it only for a problem tied to no field.
- Use times from `TimeProvider` or `DateTimeOffset.UtcNow`; never local time.
- `LoggingBehavior` logs the request type name only. Do not log request or response
  objects, tokens, passwords or personal data.

## 4. Failures and HTTP status codes

Verified behavior of `ApiController` with `ApiResponseEnvelopeFilter` and
`GlobalExceptionHandlingMiddleware`:

| What the code does | HTTP result |
|---|---|
| Returns a failed `Result` and the controller calls `HandleFailure(result)` | Always **400**, with the error's code and message, whatever the code says |
| Throws `NotFoundException` | 404 |
| Throws `ConflictException`, or EF throws `DbUpdateConcurrencyException` | 409 |
| Throws `ForbiddenException` | 403 |
| Throws `UnauthorizedException` | 401 |
| Throws `ServiceUnavailableException` | 503 |
| Throws `ValidationException` or `CqrsValidationException`, or model binding fails | 400 with code `Validation.Failure` and `errors` |
| Throws any other `DomainException` | 400 |
| Throws anything else, including reading `.Value` of a failed `Result` | 500 `Server.Unhandled` |

Rules that follow:
- Signal every expected failure by throwing the matching exception: `BadRequestException`
  for a business rule the client broke, and the not found, conflict, forbidden,
  unauthenticated and unavailable types for the rest. Handlers return only successful
  results; do not add `Result.Failure` paths. A failed `Result` always answers 400 and
  hides the real status, and one mechanism keeps every failure path the same.
- Controllers still guard the result: `return result.IsFailure ? HandleFailure(result) :
  ...`. Never read `.Value` before that check; on a failed result it throws and answers
  500.
- Error codes are `<Resource>.<Reason>` in PascalCase (`Organization.NotFound`,
  `Organization.SlugTaken`), defined once in `<Resource>Errors`. They are part of the API
  contract: the frontend switches on them, so never rename one without a spec change.
  `new NotFoundException("Organization", id)` yields `Organization.NotFound`.
- Messages are safe to show a user: no stack traces, SQL, internal ids of other
  organizations, or secrets. Do not reveal whether a resource exists to a caller who may
  not see it: answer 404, not 403.

## 5. Controllers and the HTTP contract

- A controller only translates HTTP to a request and back. No DbContext, repository,
  domain service or business rule in a controller; everything goes through `Sender`.
- Base classes: `ApiController` for signed-in users, `PublicApiController` for anonymous
  endpoints (it has no `Sender`; inject `ISender` yourself), `IntegrationApiController`
  for machine callers with an API key (route prefix `api/v1/integrations/...`; answers 503
  until keys are configured).
- `ApiController` carries `[Authorize]`, so every controller deriving from it requires a
  valid token (proven in `tests/Shipwise.Identity.Tests`: a controller with no attribute
  of its own answers 401). Never remove or override that. Every action also carries
  `[PermissionAuthorize("<Function>", ActionType.<Action>)]` with the function name the
  spec gives: a token alone is not permission. Never invent a permission name, and never
  use `PublicApiController` or `[AllowAnonymous]` unless the spec says the endpoint is
  public. For an action explicitly approved by its spec to operate only on the caller's
  own account, use `[AuthenticatedUserOnly]` in place of `[PermissionAuthorize]`.
  The inherited authorization and the handler's ownership check both apply.
- An attribute is not tenant isolation. Every handler also filters by the caller's
  organization in the query itself, and the tests prove another organization gets 404.
- Routes come from the base class: `api/v{version}/[controller]`. Ids come from the route,
  never duplicated in the body. The HTTP request model lives in the API project and is
  mapped by hand to the command.
- Every response is wrapped as `{ "success", "data", "error" }`; errors are
  `{ "code", "message", "errors", "traceId" }`. Return plain values (`Ok(value)`), never
  build `ApiResponse` yourself.
- Verified: `NoContent()` is rewritten to **200** with an empty envelope. Use `Ok()` for
  commands without a value and write 200, not 204, in the contract.
- Created resources: `CreatedAtAction(nameof(GetById), new { id, version = "1" }, body)`.
  The target action must exist; verified to produce 201 with a `Location` header.
- Declare every status code an action can return with `[ProducesResponseType]`, matching
  the spec's API contract. A contract change lands in the spec first.

```csharp
public sealed class OrganizationsController(ISender sender) : ApiController(sender)
{
    [HttpPost]
    [PermissionAuthorize("Organizations", ActionType.Create)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(CreateOrganizationRequest request, CancellationToken cancellationToken)
    {
        Result<Guid> result = await Sender.SendAsync(
            new CreateOrganizationCommand(request.Slug, request.DisplayName), cancellationToken);

        return result.IsFailure
            ? HandleFailure(result)
            : CreatedAtAction(nameof(GetById), new { id = result.Value, version = "1" }, new { id = result.Value });
    }
}
```

## 6. Pipeline, transactions and persistence

- Registered order, outermost first: validation, logging, performance, tracing,
  transaction, handler. Register it once with `AddApplicationPipelineBehaviors()`; do not
  reorder it, and do not also register `AddValidationPipelineBehavior()`.
- The transaction behavior wraps commands only, in `IUnitOfWork.ExecuteInTransactionAsync`.
  Handlers and repositories never call `SaveChangesAsync`, never open their own
  transaction, and never commit.
- The module's Infrastructure supplies the real `IUnitOfWork`. Every request, queries
  included, resolves it, so a host without one fails on the first request; never register
  a fake to make that go away.
- Verified: the pipeline hands a failed `Result` to the unit of work as a normal return.
  The module's `IUnitOfWork` must therefore save and commit only when the response is a
  successful `Result`, and roll back for a failed one or an exception. Independently of
  that, a handler decides to fail before it changes any tracked entity.
- Repository interfaces live in Application, implementations in Infrastructure, deriving
  from `EfRepositoryBase`. Reads use `Query(...)` (no tracking); writes load with
  `TrackedQuery(...)` or `FindByIdAsync` (tracked). Do not call `UpdateEntity` on an
  entity that is already tracked: it marks every column modified.
- `IRepositoryBase.GetByIdAsync` returns a non-null entity, so the implementation throws
  `NotFoundException` when the row is missing or belongs to another organization.
- Deleting follows the ef-migrations skill: soft delete through the domain method, never
  `RemoveEntity` on a soft-deletable entity.
- Raw SQL is the exception: always parameterized, identifiers from a fixed list.

## 7. Host registration (first API host)

The complete host (`Program.cs`, health checks, configuration keys, test host) is in the
module-skeleton skill and its templates; when the two differ, that skill wins. The order
that matters for request handling, verified in a probe unless marked otherwise:

```csharp
services.AddControllers(options => options.Filters.Add<ApiResponseEnvelopeFilter>());
services.AddSelfMadeCqrs(typeof(ApplicationAssemblyMarker).Assembly).AddApplicationPipelineBehaviors();
services.AddScoped<IUnitOfWork, ModuleUnitOfWork>();          // the module's real implementation

app.UseGlobalExceptionHandling();                             // outermost
app.UseApiStatusCodeEnvelope();
app.UseRouting();
app.UseAuthentication();                                      // not exercised by the probe
app.UseAuthorization();
app.MapControllers();
```

- Also required, from their signatures only: `AddConfiguredApiVersioning()`,
  `AddCurrentUserProvider()`, `AddConfiguredAuthentication(configuration, environment)`,
  `AddConfiguredAuthorization()`, `UseCorrelationId()`, and the audit interceptor
  registration from the ef-migrations skill.
- `AddConfiguredAuthorization()` sets no fallback policy; protection comes from the
  `[Authorize]` on `ApiController`. A fallback policy requiring an authenticated user would
  also cover controllers that do not derive from it, but it would apply to
  `IntegrationApiController`, which authenticates by API key only. Whether to add one is
  decided in the identity ADR, not in a feature task.
- Never assume a route, registration, permission, policy or migration exists: open the
  file and check, and say in the handoff report what you checked.

## 8. Messaging and workers

Integration events derive from `IntegrationEvent` and live in `src/Contracts`. Publish
through the outbox inside the command's transaction, never directly to the broker from a
handler. Consumers are idempotent (inbox or idempotency record). Before changing a
contract, consumer or broker topology, read the current `Shipwise.Messaging` code and the
spec; a published contract changes only through a spec. Keep the correlation id flowing and
never log message payloads.

## 9. Required tests

- Handler tests for each acceptance criterion the task covers, including each failure path.
- Tests through the HTTP boundary for every endpoint: success, invalid input (400 with
  field errors), no credentials (401), missing permission (403), another organization's
  data (404), not found (404), and conflict (409) where it applies.
- A test that a failed command leaves the database unchanged.
- `scripts/Verify-Platform.ps1` passes; it runs the architecture reference check.

## 10. Checklist (author checks before committing, reviewer rechecks)

- [ ] Files, names and project references follow section 2; no forbidden package added
- [ ] Controller is thin, derives from the right base class and has a `[PermissionAuthorize]`
      per action or the spec-approved `[AuthenticatedUserOnly]` marker for the caller's own
      account. The spec names each anonymous endpoint.
- [ ] Failures are thrown, never returned; actions check `IsFailure` before `Value`; status codes follow section 4 and
      match the spec and `[ProducesResponseType]`
- [ ] Validation errors carry field names; error codes come from `<Resource>Errors`
- [ ] Handlers take identity and organization from `ICurrentUserProvider` and filter every
      query by organization
- [ ] No `SaveChangesAsync`, transaction or commit in handlers or repositories; a failed
      command changes nothing
- [ ] Queries are read-only, projected to DTOs, paged, with a fixed sort list
- [ ] Cancellation tokens passed through; nothing sensitive logged
- [ ] Tests from section 9 added and passing
