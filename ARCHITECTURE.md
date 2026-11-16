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
| Identity.Domain | Empty: future aggregates and invariants | SharedKernel |
| Identity.Application | CQRS registration and pipeline; no use case yet | Identity.Domain, SharedKernel |
| Identity.Infrastructure | `IdentityDbContext` (schema `identity`), unit of work, design-time factory | Identity.Application, Identity.Domain, EntityFrameworkCore |
| Identity.API | Host composition, `/health/live`, `/health/ready`; no controller yet | Identity.Application, Identity.Infrastructure, AspNetCore, Observability |
| Identity.MigrationRunner | Applies migrations and exits | Identity.Infrastructure, Observability |

BuildingBlocks never reference module implementations or contracts. The architecture
script checks source and test project references for missing/external paths and cycles,
and enforces the layer order inside a module. Contracts is still a documentation-only
extension point. Identity is a base skeleton: it owns database `shipwise_identity` and
schema `identity`, has no migration, registers no `IAuthenticatedContextValidator` (so
every token is rejected), and has no Worker, outbox tables, Docker image or Gateway route.
Its unit of work commits only when a command returns a successful result.
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
and an authoritative active-context validator. No token issuer or authentication
store is included. Legacy business permission lists were removed; neutral permission
formatting and generic claims/current-user abstractions remain as opt-in primitives.

Telemetry meter names, metric names, service namespace, heartbeat environment variable
and dashboards use Shipwise naming. Privacy processors and dimension limits remain
enabled. Telemetry Compose services are optional; excluded-module alerts and panels
are omitted. Docker Compose is a local development topology, not production deployment.
