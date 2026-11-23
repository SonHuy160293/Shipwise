# Shipwise platform architecture

This is a shared infrastructure baseline, extracted from DeliveryPilot `092de07`.
It preserves separately composable hosts; it does not introduce a modular monolith.

| Project | Responsibility | Project dependencies |
|---|---|---|
| SharedKernel | Domain/result abstractions, custom CQRS, validation, security contracts | None |
| AspNetCore | Transport envelopes, middleware, auth helpers, resilient integration clients | SharedKernel |
| EntityFrameworkCore | Audit interceptor, repository primitives, reliability mappings | SharedKernel |
| Messaging | Outbox storage/relay, broker primitives, envelope encryption | EntityFrameworkCore |
| Observability | OTLP export and safe telemetry processors | None |
| Utilities | Configuration helpers | None |
| Gateway | Explicit YARP route allowlist and host composition | AspNetCore, Observability |
| Identity.Domain | User and password credential models | SharedKernel |
| Identity.Application | Account registration command, validation and handler | Identity.Domain, SharedKernel |
| Identity.Infrastructure | `IdentityDbContext` (schema `identity`), unit of work, design-time factory | Identity.Application, Identity.Domain, EntityFrameworkCore |
| Identity.API | Public registration endpoint, `/health/live` and `/health/ready` | Identity.Application, Identity.Infrastructure, AspNetCore, Observability |
| Identity.MigrationRunner | Applies migrations and exits | Identity.Infrastructure, Observability |

BuildingBlocks never reference module implementations or contracts. The architecture
script checks source and test project references for missing/external paths and cycles,
and enforces the layer order inside a module. Contracts is still a documentation-only
extension point. Identity owns database `shipwise_identity`, schema `identity` and the EF
migration for account registration. The Gateway can forward the public registration
route to Identity. Protected endpoints reject tokens until an authoritative
`IAuthenticatedContextValidator` is registered.
The Identity unit of work commits only when a command returns a successful result.
`ApiController` requires an authenticated user for every derived controller.

CQRS uses `ISender`, not MediatR. Registered behavior order is validation, logging,
performance, tracing, transaction, handler. Only commands enter `IUnitOfWork`
transactions. Future unit-of-work implementations must save and commit only after
successful handlers and roll back failures; queries do not start command transactions.

Expected domain exceptions pass through `GlobalExceptionHandlingMiddleware`.
It does not interpret failed Result values. Consumers must not read failed `Result.Value`.
Instants use UTC DateTimeOffset. Preserve cancellation through each layer.

EF reliability entities map to `reliability.OutboxMessages`, `InboxMessages`, and
`IdempotencyRecords`. They include leases, concurrency tokens and recovery transitions.
Future module hosts must supply DbContexts, migrations, unit of work, publisher
registrations, broker topology and authoritative authorization. No business migrations,
consumers or contracts are supplied by the platform.

Gateway startup accepts empty routing only with explicit Development opt-in. Empty
configuration is live but not ready. Nonempty routes retain method/path allowlisting,
body limits, deadlines, cluster validation and last-good configuration on rejected
reloads. Readiness validates routing configuration, not upstream availability. Private
routes can opt into `no-store` through route metadata; no business paths are hardcoded.

JWT validation pins HS256 and requires issuer, audience, expiry, user/session GUIDs
and an authoritative active-context validator. Identity stores registration credentials.
Token issuance is outside the registration flow. The shared platform provides permission
formatting and generic claims and current-user abstractions.

Telemetry meter names, metric names, service namespace, heartbeat environment variable
and dashboards use Shipwise naming. Privacy processors and dimension limits remain
enabled. Telemetry Compose services are optional; excluded-module alerts and panels
are omitted.

## Deployment and configuration

The application uses one multi-target Dockerfile and Compose on a Linux engine.
The local override builds the images and exposes loopback ports for the Gateway,
Identity API and PostgreSQL. The server configuration uses published images and exposes
only the Gateway on loopback. The configuration has three layers.

| Layer | Contents | Reader |
|---|---|---|
| `appsettings.json` in the image | Non-secret host defaults | .NET configuration |
| `deploy/env/<environment>.env` | Literal, non-secret settings such as image references, ports, origins, the trusted network and secret directory | The PowerShell wrapper and Compose |
| Compose secret files | Database passwords, JWT signing key and integration API key | PostgreSQL uses `POSTGRES_PASSWORD_FILE`, bootstrap reads the password mounts, and .NET uses `Database:PasswordFile` or key-per-file under `/run/secrets/config` |

On the server, the Gateway trusts the ingress IP from `SHIPWISE_INGRESS_PROXY_IP`.
Identity trusts forwarded headers from the HTTP network configured by `SHIPWISE_HTTP_NETWORK`.
The operator provisions the mounted files. Local `init` generates them. See the
[run instructions and secret mounts](README.md#run-the-local-stack).

### Database roles and startup

The application PostgreSQL instance holds one database per module. The bootstrap job
creates `shipwise_<module>` with `<module>_migrator` as owner. MigrationRunner connects
as that role to create schemas and apply EF migrations. The API connects as
`<module>_api` with database connect access. Default privileges on objects created by
the migrator grant the API schema usage, table select/insert/update/delete and sequence
usage/select. The API role cannot create tables. Identity uses `identity_migrator` and
`identity_api`. Test databases use a separate PostgreSQL instance.

Compose waits for PostgreSQL to be healthy before running `db-bootstrap`. Its successful
exit releases `identity-migration`. After the migration exits successfully,
`identity-api` starts. The Gateway starts when the API is healthy.

### Images workflow

The `Images` workflow uses Buildx with the GitHub Actions cache to build `gateway`,
`identity-api` and `identity-migration`. Each matrix job scans its local image and fails
on critical vulnerabilities with an available fix. On a push to `main`, each passing job
pushes that same image to `ghcr.io/<owner>/shipwise-<target>:<sha>`. Pull requests and
other branches run the builds and scans without publishing.

An independent Ubuntu smoke job initializes and starts the local stack, checks both
readiness endpoints and registers through the Gateway. It verifies persistence across a
restart and searches container inspection output, logs and image history for generated
secrets. Its cleanup step always removes the stack and its volumes.
