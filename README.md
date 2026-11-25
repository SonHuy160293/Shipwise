# Shipwise

.NET 10 backend for Shipwise. The platform foundation was extracted from DeliveryPilot
commit `092de07` on 2026-11-14; the source repository was left unchanged, and its
Identity, Notification and Projects implementations, contracts, migrations, business
tests and product documents were not imported.

The solution contains six BuildingBlocks (SharedKernel, AspNetCore,
EntityFrameworkCore, Messaging, Observability and Utilities), an optional YARP
Gateway, the Identity module, and four test projects. See
[ARCHITECTURE.md](ARCHITECTURE.md). Identity provides account registration and sign-in,
session refresh, sign-out, and the signed-in user's account.

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

## Run the local stack

From the repository root, use PowerShell 7 and a running Docker Linux engine with Compose.

```powershell
./scripts/shipwise.ps1 init
./scripts/shipwise.ps1 check
./scripts/shipwise.ps1 up
./scripts/shipwise.ps1 status
./scripts/shipwise.ps1 logs
```

`init` copies the local template to `deploy/env/local.env` and generates missing secrets
in `deploy/secrets/shipwise-local/`. Both are Git-ignored. Running `init` again preserves
existing settings and secret values. On Linux it uses `sudo` when needed to set ownership.
`check` validates settings, images, secret files and port availability. `up` repeats the
checks, builds the local images and waits for startup. In `status`, `db-bootstrap` and
`identity-migration` should show exit code 0. PostgreSQL, Identity API and Gateway should
show healthy.

| Service | Default local port | How to check it |
|---|---:|---|
| Gateway | 19090 | `http://127.0.0.1:19090/health/ready` |
| Identity API | 19091 | `http://127.0.0.1:19091/health/ready` |
| Application PostgreSQL | 25441 | Database `shipwise_identity` |
| Test PostgreSQL (separate infrastructure profile) | 25431 | Database `shipwise_tests` |

Use `./scripts/shipwise.ps1 down` to stop the stack and retain its database for the next
`up`. To delete the stack's volumes and their data, run `./scripts/shipwise.ps1 down -Reset`
and type the stack name at the confirmation prompt.

### Secret files

Compose mounts each file only in the services listed below. The environment file holds
settings and file paths. The secret values stay in the files.

| File in `SHIPWISE_SECRETS_DIR` | Services that read it | Mount path inside each service |
|---|---|---|
| `postgres-admin-password` | `postgres`, `db-bootstrap` | `/run/secrets/postgres-admin-password` (`POSTGRES_PASSWORD_FILE` in PostgreSQL) |
| `identity-migrator-password` | `db-bootstrap`, `identity-migration` | `/run/secrets/identity-migrator-password` (`Database:PasswordFile` in MigrationRunner) |
| `identity-api-password` | `db-bootstrap`, `identity-api` | `/run/secrets/identity-api-password` (`Database:PasswordFile` in the API) |
| `identity-jwt-key` | `identity-api` | `/run/secrets/config/Jwt__SecretKey` |
| `identity-integration-key` | `identity-api` | `/run/secrets/config/IntegrationApi__ApiKeys__0` |

On a Linux server, `./scripts/shipwise.ps1 init -Environment server` creates
`deploy/env/server.env` from the server template. The operator supplies the files in
`/etc/shipwise/secrets/<stack>/` with mode `0400`. Set owner UID 999 for
`postgres-admin-password` and UID 1654 for the other four. Fill in the template
placeholders, including the published application image references (commit tag or
digest) and `SHIPWISE_INGRESS_PROXY_IP`, the IP of the ingress proxy trusted by the
Gateway. Run `check` and `up` with `-Environment server`. Only the Gateway publishes a
loopback port on the server.

## Run the Gateway with dotnet

```powershell
dotnet run --project src/Gateway/Shipwise.Gateway --no-launch-profile -- --environment Development --urls http://127.0.0.1:19090
```

The default route file is empty. Development selects `proxy.Development.json`, which
forwards registration, sign-in, refresh, sign-out and account requests to Identity. Readiness verifies route configuration,
not upstream availability. Selecting the empty route file in Development with
`Gateway:AllowEmptyConfiguration` enabled gives a live but not ready Gateway.
Production rejects empty routing even with the flag set;
it also requires explicit trusted ingress proxies, allowed hosts and HTTPS CORS origins.

Route `Metadata:CacheControl` can be set to `no-store` for private responses. JWT
helpers retain signature/lifetime validation and require an authoritative
`IAuthenticatedContextValidator`; a missing validator rejects every token.

## Run the Identity module with dotnet

`src/Modules/Shipwise.Identity` holds five projects (Domain, Application, Infrastructure,
API, MigrationRunner). It exposes `/health/live`, `/health/ready` and the public
`POST /api/v1/auth/register` endpoint. Swagger is available in Development. Registration
is enabled in Development and disabled by default elsewhere. Set `Registration:Enabled`
only when registration should be available. Disabled registration returns 404 before
rate limiting; enabled registration is limited to five requests per minute per client IP.
The Development Gateway forwards Identity routes to port 19091. Sign in with
`POST /api/v1/auth/sign-in`, then read `GET /api/v1/account`. Use
`POST /api/v1/auth/refresh` to rotate the refresh cookie, and
`POST /api/v1/auth/sign-out` to end the current session. Send an allowed `Origin`
on these three POSTs and on every unsafe request carrying an auth cookie. Compose
uses `SHIPWISE_FRONTEND_ORIGIN` for the Identity and Gateway allowlists.

For host development, `dotnet run` still works with .NET user secrets for the API.
Use a running development PostgreSQL instance and free the API and Gateway ports before
starting the hosts.

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
dotnet user-secrets set 'Jwt:AccessTokenExpirationMinutes' '10' --project $api
dotnet user-secrets set 'IntegrationApi:ApiKeys:0' '<random value>' --project $api

# Scope the migrator connection to a separate process. The API uses its own user secrets.
pwsh -NoProfile -Command {
    $env:ConnectionStrings__ApplicationDb = '<migrator connection string>'
    dotnet run --project src/Modules/Shipwise.Identity/Shipwise.Identity.MigrationRunner
}
dotnet run --project $api --launch-profile Local    # http://localhost:19091
```

The MigrationRunner reads its connection string from the environment or the command
line, exits with 0 on success and 1 on failure, and is the only thing that applies
migrations. Identity validates access tokens against the session on each protected request.
Access tokens last 10 minutes. Sessions expire after 7 idle days or 30 days in total.
The previous refresh token can be used for 30 seconds after rotation. Configure these
intervals with `Jwt:AccessTokenExpirationMinutes`, `Session:IdleTimeoutDays`,
`Session:AbsoluteTimeoutDays` and `Session:RefreshGraceSeconds`. The HTTP contract is generated at
`contracts/openapi/shipwise.v1.json`. To regenerate it after an approved contract change,
set `SHIPWISE_UPDATE_CONTRACT=1` and run the `OpenApiContractTests` test.

## Test infrastructure and telemetry

The `infrastructure` and `telemetry` profiles use a separate Compose project. Start
the infrastructure profile for PostgreSQL and RabbitMQ tests. Start telemetry when
you need the local observability services.

```powershell
./scripts/shipwise.ps1 up -Profile infrastructure
./scripts/shipwise.ps1 up -Profile telemetry
```

Grafana listens on loopback port 23000. Its initial username is `admin`.
Set `SHIPWISE_GRAFANA_PASSWORD` before starting the telemetry profile, or use the
disposable default in [deploy/compose.yaml](deploy/compose.yaml).

For isolated infrastructure tests, use a dedicated empty database: tests create the
`reliability` schema and must never target an application database. The Identity tests
create and drop a database of their own on the same server, so the test user must be
allowed to create databases.

For the connection strings below, use the PostgreSQL password from
`SHIPWISE_TEST_POSTGRES_PASSWORD` and the RabbitMQ password from
`SHIPWISE_TEST_RABBITMQ_PASSWORD`. If you started the infrastructure profile without
setting them, use their disposable defaults in [deploy/compose.yaml](deploy/compose.yaml).
URL-encode the RabbitMQ password when inserting it into the URI.

```powershell
$env:SHIPWISE_TEST_POSTGRES = 'Host=localhost;Port=25431;Database=shipwise_tests;Username=shipwise_tests;Password=<test database password>'
$env:SHIPWISE_TEST_RABBITMQ = 'amqp://shipwise_tests:<URL-encoded test broker password>@localhost:25672/'
& ./scripts/Verify-Platform.ps1 -RequireInfrastructure
```

Stop these services with `./scripts/shipwise.ps1 down -Profile infrastructure,telemetry`.
Named data volumes are retained.

## Add business functionality

Start at [src/Modules](src/Modules/README.md) and [src/Contracts](src/Contracts/README.md).
New modules are created with the module-skeleton skill in `.agents/skills`, and business
code follows the cqrs-api and ef-migrations skills. Each module owns its DbContext,
migrations, unit of work, consumers and event registrations. Shared reliability mappings do not create a runtime database by themselves.
No DeliveryPilot product requirements or approval records were imported.
