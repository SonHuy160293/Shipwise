# 0002 Containers and the local stack

Status: approved · Owner: Claude Code · Related ADRs: 0004, 0001 · Design proposal: none (no screens)

## Problem and outcome

Running Shipwise today means starting PostgreSQL, the MigrationRunner, the Identity API,
the Gateway and the frontend by hand, with settings typed into user secrets or the shell.
Every process connects as one database superuser. Only the Gateway has a container image,
it is not pinned, and CI builds no images.

After this ships, a developer runs `init` once and `up`, and the whole backend runs in
containers with generated secrets, least-privilege database roles and hardened runtime
settings. CI builds, scans and publishes an image per host on every push to `main`, and a
smoke test proves the stack works end to end. We know it works when a fresh clone reaches
a successful registration through the Gateway with only `init` and `up`.

## Scope

In scope
- One multi-target `Dockerfile` for the Gateway, Identity API and Identity MigrationRunner.
- `Database:PasswordFile` support in the Identity module.
- Trusting forwarded headers from a network range in `AddTrustedForwarding`.
- Environment templates `deploy/env/local.env.example` and `deploy/env/server.env.example`.
- A compose layout for the application stack: a base file and a local override.
- A database bootstrap job with per-module roles and default privileges.
- `scripts/shipwise.ps1` with `init`, `check`, `up`, `down`, `status`, `logs`.
- CI: build, scan and push images; a compose smoke test.
- README run guide; module-skeleton skill and `AGENTS.md` updated with the new steps.

Out of scope
- A frontend image and how the frontend is hosted.
- Worker hosts, the message broker in the application stack, broker accounts.
- TLS, public ingress, DNS, backups, high availability.
- The telemetry stack (it keeps its existing `telemetry` profile).
- Deploying to a real server (the server template is written, not exercised).

## Research and alternatives

DeliveryPilot's setup was studied on 2026-11-21; ADR 0004 records what is kept and what is
simplified, and the owner chose that option. Main simplifications: one Dockerfile instead
of three, one PostgreSQL server locally, the official PostgreSQL image for database setup
instead of a custom image, compose secrets instead of a staging job, default privileges
instead of a grants job after each migration, a network range instead of a fixed Gateway
IP, and one PowerShell 7 wrapper instead of a PowerShell and a Bash copy.

Two techniques are new to the owner's projects and are proven first (task 1):
- compose `secrets:` mounted at a custom path inside `/run/secrets/config/`, readable by
  the container's non-root user;
- `ALTER DEFAULT PRIVILEGES FOR ROLE identity_migrator` granting schema use and table
  access to `identity_api` for objects created later by migrations.

## User stories and acceptance criteria

- US-1: As a developer, I can start the whole backend in containers with two commands.
  - AC-1: Given a fresh clone, when `scripts/shipwise.ps1 init` runs, then
    `deploy/env/local.env` is created from the example if missing, and every secret listed
    in "Secrets" exists under `deploy/secrets/shipwise-local/`. Running `init` again changes
    no existing file. Both locations are ignored by Git.
  - AC-2: Given a required environment key is missing, a secret file is missing or empty,
    a configured host port is already in use, or an image reference in a server
    environment file is `latest`, when `check` runs, then it exits non-zero and names the
    problem without printing any secret value.
  - AC-3: Given `init` has run, when `up` runs, then PostgreSQL, the bootstrap job, the
    migration job, the Identity API and the Gateway start in that order, the two jobs exit
    0, and `/health/ready` answers 200 on the Gateway (19090) and on the Identity API
    (19091, local only).
  - AC-4: Given the stack is up, when a registration is sent through the Gateway, then it
    answers 201, and a `Users` row and a `PasswordCredentials` row exist in
    `shipwise_identity`.
  - AC-5: Given the stack was stopped with `down`, when `up` runs again, then the earlier
    account still exists and no secret has changed.
- US-2: As the owner, I can trust that secrets and privileges are handled safely.
  - AC-6: The `identity_api` role can read and write rows in schema `identity` and cannot
    create, alter or drop any object; `identity_migrator` owns the database and schema;
    the administrator role is used only by the bootstrap job.
  - AC-7: No secret value appears in any committed file, environment file, compose file,
    image layer, container environment (`docker inspect`) or log line.
  - AC-8: Every long-running container runs as a non-root user with a read-only root file
    system, all capabilities dropped and `no-new-privileges`; every base image is pinned
    by digest.
  - AC-9: The Identity API accepts forwarded client addresses only from the configured
    network range: from inside the range the rate limiter sees the client's address; from
    outside it, the forwarded header is ignored.
- US-3: As the owner, I can rely on CI to produce safe images.
  - AC-10: On every push to `main`, CI builds the three images, fails if a scan finds a
    critical vulnerability with a fix available, and pushes passing images to
    `ghcr.io/<owner>/shipwise-<target>:<commit-sha>`. Pull requests and other branches
    build and scan but do not push.
  - AC-11: In CI, the smoke test runs `init`, `check` and `up`, waits for both readiness
    endpoints, sends one registration through the Gateway expecting 201, and runs `down`
    removing volumes, whatever the result.

## Non-functional requirements

- A rebuild after a source-only change reuses the restore layer (no NuGet download).
- `up` on a machine that already has the images reaches ready within 2 minutes.
- Memory limits: API and Gateway 512 MB, PostgreSQL 768 MB, jobs 256 MB.

## API contract

No API changes. The registration endpoint from spec 0001 is used as the smoke test.

## Backend design

**Dockerfile** (repository root, replaces the current one):
- Build arguments `DOTNET_SDK_IMAGE` and `DOTNET_RUNTIME_IMAGE`, each with a digest-pinned
  default.
- A `restore` stage that copies `global.json`, `Directory.*.props`, `.editorconfig`,
  `Shipwise.slnx` and every `*.csproj` by pattern (keeping their folders), then restores
  each host project with a NuGet cache mount. No hand-written list of projects.
- A `source` stage that copies `src/`; one publish stage per host; one final stage per
  target on the runtime image with `curl` for health checks, `ASPNETCORE_HTTP_PORTS=8080`
  and `USER $APP_UID`.
- Targets: `gateway`, `identity-api`, `identity-migration`. Normal parallel build; no
  `-m:1` or `--disable-parallel`.
- `.dockerignore` keeps tests, docs, scripts, secrets, environment files and agent folders
  out of the build context (it already does; confirm).

**Configuration in code**
- Identity `AddPersistence`: if `Database:PasswordFile` is set, read the file, trim one
  trailing newline, and set it as the connection's password; fail at startup if the file
  is missing or empty. Never log the value. Same rule in the MigrationRunner, which shares
  `AddPersistence`.
- `AddTrustedForwarding`: also read `ForwardedHeaders:KnownNetworks` (CIDR strings) into
  `KnownIPNetworks`; reject an invalid value at startup. Loopback stays the default only
  when neither proxies nor networks are configured.
- Connection strings in containers carry `Include Error Detail=false`.

**Compose layout** under `deploy/compose/`:
- `compose.yaml`: images only, no `build`, no host ports except the Gateway's; services
  `postgres`, `db-bootstrap`, `identity-migration`, `identity-api`, `gateway`; one
  `backend` network for database traffic and one `http` network for Gateway to API;
  hardening as a YAML anchor; secrets declared at the top level from
  `${SHIPWISE_SECRETS_DIR}`; required variables written as `${NAME:?Set NAME}`.
- `compose.local.yaml`: adds `build` (target and pinned base images), binds PostgreSQL
  (25441), the Identity API (19091) and the Gateway (19090) to `127.0.0.1`, and sets
  `Development` environments.
- The Gateway gets its Identity address from
  `ReverseProxy__Clusters__identity__Destinations__identity-api__Address=http://identity-api:8080/`
  over its Development routes file, and trusts the stack's `http` network.
- The existing `deploy/compose.yaml` keeps only the test infrastructure profile (PostgreSQL
  on 25431 and RabbitMQ for `Verify-Platform.ps1`) and the telemetry profile; its gateway
  service moves to the new layout.

**Secrets** (files in `${SHIPWISE_SECRETS_DIR}`, generated by `init` locally):

| File | Used by | Mounted as |
|---|---|---|
| `postgres-admin-password` | PostgreSQL, `db-bootstrap` | `POSTGRES_PASSWORD_FILE` |
| `identity-migrator-password` | `db-bootstrap`, `identity-migration` | `Database:PasswordFile` |
| `identity-api-password` | `db-bootstrap`, `identity-api` | `Database:PasswordFile` |
| `identity-jwt-key` | `identity-api` | `/run/secrets/config/Jwt__SecretKey` |
| `identity-integration-key` | `identity-api` | `/run/secrets/config/IntegrationApi__ApiKeys__0` |

Passwords are 32 random bytes as hex; keys are 32 random bytes as Base64. On a server the
files are owned by UID 1654 (PostgreSQL's by UID 999) with mode 0400.

**Environment templates** (`deploy/env/*.env.example`, literal `KEY=value`, no secrets):
`SHIPWISE_STACK`, `SHIPWISE_ENVIRONMENT`, `SHIPWISE_SECRETS_DIR`, the three image
references, the two base images and the PostgreSQL image (all digest-pinned),
`SHIPWISE_GATEWAY_PORT`, `SHIPWISE_IDENTITY_API_PORT`, `SHIPWISE_POSTGRES_PORT`,
`SHIPWISE_GATEWAY_PUBLIC_HOST`, `SHIPWISE_FRONTEND_ORIGIN`, `SHIPWISE_JWT_ISSUER`,
`SHIPWISE_JWT_AUDIENCE`, `SHIPWISE_HTTP_NETWORK` (CIDR). The server template uses
`REPLACE_WITH_...` placeholders and registry image tags.

**Wrapper** `scripts/shipwise.ps1` (PowerShell 7) replaces the current one:
- `init`, `check`, `up` (runs `check` first, builds locally), `down` (keeps volumes),
  `status`, `logs`; `-Environment local|server` selects the environment file and whether
  the local override is used; `-Reset` on `down` removes volumes, after a confirmation
  prompt.
- The test-infrastructure and telemetry actions of the current script keep working.

**CI** (`.github/workflows/images.yml`, separate from Platform CI):
- `build` job matrix over the three targets: Buildx with a GitHub Actions cache, a
  vulnerability scan with a pinned scanner action (fail on critical with a fix available),
  push to GitHub Container Registry on `main` only, with `packages: write` limited to this
  job.
- `smoke` job: runs the AC-11 flow on the runner with `-Environment local`.

No sequence diagram: the start order in AC-3 is the only flow, and compose enforces it.

Rollout and compatibility: nothing is deployed yet. Running on the host with
`dotnet run` and user secrets (README) keeps working; the stack is an alternative, and
`check` reports if both try to use the same port.

## Database design

No table changes. New roles, privileges and a database, created by the bootstrap job, not
by an EF migration.

Database: `shipwise_identity` (owner `identity_migrator`), created by the bootstrap job.

| Role | Login | Privileges |
|---|---|---|
| `shipwise_admin` | yes | Superuser; the image's administrator; used only by `db-bootstrap` |
| `identity_migrator` | yes | Owns `shipwise_identity`; creates schema `identity` and every object through migrations |
| `identity_api` | yes | `CONNECT`; by default privileges for objects created by `identity_migrator`: `USAGE` on schemas, `SELECT, INSERT, UPDATE, DELETE` on tables, `USAGE, SELECT` on sequences. No `CREATE` anywhere; `CREATE` on schema `public` revoked from `PUBLIC` |

The bootstrap SQL is idempotent: it creates what is missing and changes nothing that
exists, and passwords reach `psql` as variables, never on a command line or in a log.

Query patterns: none new. Data lifecycle: the PostgreSQL volume survives `down`;
`down -Reset` deletes it. Migration plan: none.

## UI and states

No UI changes.

## Test plan

| Criterion | Test (type and where) |
|---|---|
| AC-1, AC-2 | Scripted checks in the CI smoke job: run `init` twice and compare file hashes; run `check` with a missing key, a missing secret and an occupied port, expecting failure and no secret in output |
| AC-3, AC-4, AC-11 | CI smoke job (compose on the runner) |
| AC-5 | CI smoke job: `down`, `up`, then registering the same email answers 409 |
| AC-6 | Infrastructure test (`[PostgreSqlFact]`) that runs the bootstrap SQL and a migration on a fresh server, then proves `identity_api` can insert and read a user and gets permission denied for `CREATE TABLE` |
| AC-7 | CI smoke job: search `docker inspect` output, image history and container logs for each generated secret value; expect none |
| AC-8 | CI smoke job: `docker inspect` shows non-root user, read-only root, dropped capabilities; a check that every `FROM` and image reference carries a digest |
| AC-9 | Unit tests for `AddTrustedForwarding` with networks: inside range, outside range, invalid CIDR |
| `Database:PasswordFile` | Unit tests: password read and applied; missing or empty file fails at startup |
| AC-10 | The `images` workflow itself; first run on `main` checked by the reviewer |

## Tasks

| # | Owner | Repo | Paths | Covers | Depends on | Done when (commands) |
|---|-------|------|-------|--------|------------|----------------------|
| 1 | Codex | Shipwise | throwaway proof only, nothing committed | proves ADR 0004 consequence 2 | - | Report: a container running as UID 1654 reads a compose secret mounted under `/run/secrets/config/`, and default privileges cover a schema created later; amend nothing if both hold |
| 2 | Codex | Shipwise | `src/Modules/Shipwise.Identity/**`, `src/BuildingBlocks/Shipwise.AspNetCore/DependencyInjection/ForwardedHeadersExtensions.cs`, `tests/**` for these | AC-9, PasswordFile | 1 | `scripts/Verify-Platform.ps1 -RequireInfrastructure` passes, none skipped |
| 3 | Codex | Shipwise | `Dockerfile`, `.dockerignore`, `deploy/**`, `scripts/shipwise.ps1`, `tests/Shipwise.Identity.Tests/**` (role test) | AC-1 to AC-8 | 2 | `Verify-Platform.ps1 -RequireInfrastructure` passes; `shipwise.ps1 init`, `check`, `up` reach ready and a registration through the Gateway answers 201; `down` |
| 4 | Codex | Shipwise | `.github/workflows/images.yml` | AC-10, AC-11 | 3 | The workflow passes on a branch run of the commit; on `main` images appear in the registry |
| 5 | Codex | Shipwise | `README.md`, `ARCHITECTURE.md`, `.agents/skills/module-skeleton/**`, `AGENTS.md` (one deployment line) | - | 3 | Docs describe `init`/`up`, the secrets table and the new-host steps |
| 6 | Claude Code | Shipwise | review (review-change skill) | all | 1-5 | no open blocker, definition of done met |

Task 1 is a short proof with no commit; tasks 2 to 5 run in order.

## Assumptions and open questions

Decided by the owner on 2026-11-21: one Linux host with Docker Compose as the production
target, one PostgreSQL server with a database per module, GitHub Container Registry.

Assumptions:
- Ports 25441 (application PostgreSQL), 19090 and 19091 are free on developer machines;
  `check` reports if not.
- The scanner action and Buildx versions are chosen by Codex and pinned; the reviewer
  checks the choice.
- GitHub Actions can push to the owner's container registry with the workflow token;
  the owner may need to allow package writes for the repository.
- A CI push to `main` happens only after review, as today; images from `main` are
  therefore reviewed builds.

No open questions.

## Change log

Empty until the spec is approved.

## Delivery record

Empty until the spec is closed.
