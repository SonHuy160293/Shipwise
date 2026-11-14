# Shipwise

Reusable .NET 10 backend foundation extracted from DeliveryPilot commit `092de07`
on 2026-11-14. The source repository was left unchanged. Identity, Notification and
Projects implementations, contracts, migrations, business tests and product documents
are excluded. This repository has no business API, token issuer or module database.

The solution contains six BuildingBlocks (SharedKernel, AspNetCore,
EntityFrameworkCore, Messaging, Observability and Utilities), an optional YARP
Gateway, and three independent test projects. See [ARCHITECTURE.md](ARCHITECTURE.md).

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
`reliability` schema and must never target an application database.

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
Future services own their DbContext, migrations, unit of work, consumers and event
registrations. Shared reliability mappings do not create a runtime database by themselves.
No DeliveryPilot product requirements or approval records were imported.
