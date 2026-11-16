# Shipwise

.NET 10 backend for Shipwise. The platform foundation was extracted from DeliveryPilot
commit `092de07` on 2026-11-14; the source repository was left unchanged, and its
Identity, Notification and Projects implementations, contracts, migrations, business
tests and product documents were not imported.

The solution contains six BuildingBlocks (SharedKernel, AspNetCore,
EntityFrameworkCore, Messaging, Observability and Utilities), an optional YARP
Gateway, the Identity module skeleton, and four test projects. See
[ARCHITECTURE.md](ARCHITECTURE.md). There is no business API, token issuer or business
table yet: the Identity module is an empty, running shell.

## Build and verify

Install a .NET SDK compatible with [global.json](global.json) (10.0.301 with
`latestFeature` roll-forward; extraction verified using 10.0.401).

```powershell
dotnet restore Shipwise.slnx
dotnet build Shipwise.slnx -c Release --no-restore
dotnet test Shipwise.slnx -c Release --no-build
& ./scripts/Test-Architecture.ps1
# Or run all checks:
& ./scripts/Verify-Platform.ps1
```

The PostgreSQL and RabbitMQ tests explicitly skip when their connection variables
are absent. Core tests require no external infrastructure. Build analyzers remain
enabled; the imported source includes existing style/analyzer warnings.

## Run the optional Gateway

```powershell
dotnet run --project src/Gateway/Shipwise.Gateway --no-launch-profile -- --environment Development --urls http://127.0.0.1:19090
```

The shipped route file is empty. In Development, `Gateway:AllowEmptyConfiguration`
permits startup: `/health/live` returns 200, `/health/ready` returns 503, and business
paths return 404. Test routes exist only in test fixtures. Configure actual upstreams
before readiness can pass. Production rejects empty routing even with the flag set;
it also requires explicit trusted ingress proxies, allowed hosts and HTTPS CORS origins.

Route `Metadata:CacheControl` can be set to `no-store` for private responses. JWT
helpers retain signature/lifetime validation and require an authoritative
`IAuthenticatedContextValidator`; a missing validator rejects every token.

## Run the Identity module skeleton

`src/Modules/Shipwise.Identity` holds five projects (Domain, Application, Infrastructure,
API, MigrationRunner). It has no entity, migration or endpoint; it exposes only
`/health/live` and `/health/ready`, and Swagger in Development.

The API does not start without these settings. Keep the secrets in user secrets locally,
or as files under `SHIPWISE_CONFIG_SECRETS_DIRECTORY` (default `/run/secrets/config`) when
deployed; never put them in `appsettings.json`.

| Key | Purpose |
|---|---|
| `ConnectionStrings:ApplicationDb` | The module database, `shipwise_identity` |
| `Jwt:SecretKey` | Token signing key, at least 32 bytes |
| `Jwt:Issuer`, `Jwt:Audience`, `Jwt:AccessTokenExpirationMinutes` | Token validation |
| `IntegrationApi:ApiKeys:0` | Required at startup even without integration endpoints |
| `Cors:*`, `Serilog:*`, `Observability:*` | Non-secret; defaults are in `appsettings.json` |

```powershell
$api = 'src/Modules/Shipwise.Identity/Shipwise.Identity.API'
dotnet user-secrets set 'ConnectionStrings:ApplicationDb' '<connection string>' --project $api
dotnet user-secrets set 'Jwt:SecretKey' '<random value, 32 bytes or more>' --project $api
dotnet user-secrets set 'Jwt:Issuer' 'shipwise-local' --project $api
dotnet user-secrets set 'Jwt:Audience' 'shipwise-local' --project $api
dotnet user-secrets set 'Jwt:AccessTokenExpirationMinutes' '15' --project $api
dotnet user-secrets set 'IntegrationApi:ApiKeys:0' '<random value>' --project $api

# Apply migrations (creates the database and the identity schema), then start the API.
$env:ConnectionStrings__ApplicationDb = '<connection string>'
dotnet run --project src/Modules/Shipwise.Identity/Shipwise.Identity.MigrationRunner
dotnet run --project $api --launch-profile Local    # http://localhost:19091
```

The MigrationRunner reads its connection string from the environment or the command
line, exits with 0 on success and 1 on failure, and is the only thing that applies
migrations. No token is accepted yet: there is no `IAuthenticatedContextValidator`, so
every protected endpoint answers 401 by design.

## Local Docker profiles

Docker Desktop's Linux engine must be running. Compose is for local development;
ports bind to loopback. Infrastructure defaults use public, disposable local-test
passwords. Override them with the corresponding environment variables when needed.

```powershell
& ./scripts/shipwise.ps1 -Action config
& ./scripts/shipwise.ps1 -Action up -Profile gateway
$env:SHIPWISE_OBSERVABILITY_ENABLED = 'true'
& ./scripts/shipwise.ps1 -Action up -Profile gateway,telemetry
& ./scripts/shipwise.ps1 -Action up -Profile infrastructure
```

On Bash, use `bash scripts/shipwise.sh up gateway telemetry` (or `infrastructure`).
Grafana is available at `http://localhost:23000` with username `admin` and password
from `SHIPWISE_GRAFANA_PASSWORD` (local default `local-observability-only`).

For isolated infrastructure tests, use a dedicated empty database: tests create the
`reliability` schema and must never target an application database. The Identity tests
create and drop a database of their own on the same server, so the test user must be
allowed to create databases.

```powershell
$env:SHIPWISE_TEST_POSTGRES = 'Host=localhost;Port=25431;Database=shipwise_tests;Username=shipwise_tests;Password=local-tests-only'
$env:SHIPWISE_TEST_RABBITMQ = 'amqp://shipwise_tests:local-tests-only@localhost:25672/'
& ./scripts/Verify-Platform.ps1 -RequireInfrastructure
```

Stop containers with `& ./scripts/shipwise.ps1 -Action down -Profile gateway,telemetry,infrastructure`.
Named data volumes are retained. No deployment script creates business databases,
email-provider credentials or module broker topology.

## Add business functionality

Start at [src/Modules](src/Modules/README.md) and [src/Contracts](src/Contracts/README.md).
New modules are created with the module-skeleton skill in `.agents/skills`, and business
code follows the cqrs-api and ef-migrations skills. Each module owns its DbContext,
migrations, unit of work, consumers and event registrations. Shared reliability mappings do not create a runtime database by themselves.
No DeliveryPilot product requirements or approval records were imported.
