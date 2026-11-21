# 0004 Deployment and configuration model

Status: accepted · Date: 2026-11-21 · Decider: owner · Related: spec 0002, ADR 0001

## Context

Shipwise has one unpinned Dockerfile that builds only the Gateway, and one compose file
whose test database uses a default password written in the file. Every process connects
to PostgreSQL as one superuser. The Identity API already reads secrets as files
(`AddKeyPerFile` on `SHIPWISE_CONFIG_SECRETS_DIRECTORY`), but its database password can
only come from the connection string.

The owner's DeliveryPilot project was studied as a reference (2026-11-21). Worth keeping:
- digest-pinned base images, a NuGet cache, non-root users;
- hardened containers (read-only, capabilities dropped, no privilege escalation, memory
  and log limits, health checks);
- secrets only as files;
- least-privilege database roles;
- migrations as a one-off job with explicit start ordering;
- loopback-only ports;
- non-secret per-environment files with required-value checks;
- checks before start.

Too heavy for Shipwise now:
- three Dockerfiles, one listing every project file by hand;
- a PostgreSQL server per module even locally;
- a custom "setup" image running shell scripts;
- the same 300-line wrapper written twice (PowerShell and Bash);
- fixed subnets and a fixed Gateway IP so forwarded headers can be trusted.

DeliveryPilot builds no images in CI.

The owner decided (2026-11-21): production is one Linux host running Docker Compose,
with images built so they can move to an orchestrator later; locally one PostgreSQL
server holds one database per module; images go to GitHub Container Registry.

## Options

| Option | For | Against |
|---|---|---|
| A. Copy DeliveryPilot's setup | Proven by the owner | Most of the maintenance cost above, for one module |
| **B. Keep its principles, simplify its tooling** | Same security properties with fewer moving parts; adds CI image builds | Some parts are new and need proving (compose secrets, default privileges) |
| C. Orchestrator first (Kubernetes) | Production-grade scheduling | Large up-front cost with no operator yet |

## Decision

Option B.

1. **Images.** One multi-target `Dockerfile` with a target per host (`gateway`,
   `identity-api`, `identity-migration`; later one per new host). Base images pinned by
   digest through build arguments. Project files are copied with a pattern rather than
   listed by hand; NuGet uses a BuildKit cache mount. Runtime runs as `$APP_UID`. Images
   are named `shipwise-<target>`.
2. **Configuration layers**, each with one job:
   - `appsettings.json` in the image: non-secret defaults;
   - `deploy/env/<environment>.env`: non-secret values that differ per environment
     (images, ports, hostnames, origins), literal `KEY=value`, never a secret;
   - secret files: mounted by compose `secrets:` and read by the app through key-per-file
     (`Jwt__SecretKey`) or a `*PasswordFile` setting. Never an environment variable,
     never a committed file, never baked into an image.
   - Running on the host without Docker keeps using .NET user secrets.
3. **Secrets lifecycle.** Locally, `scripts/shipwise.ps1 init` generates every secret
   once with a cryptographic random generator into `deploy/secrets/<stack>/` (ignored by
   Git) and never overwrites one. On a server the operator provides the same files under
   `/etc/shipwise/secrets/<stack>/`, owned by the container user (UID 1654, PostgreSQL's
   file by UID 999) with mode 0400.
4. **Database.** One PostgreSQL server per environment, one database per module. Each
   module gets two login roles: `<module>_migrator` (owns the database and its schemas)
   and `<module>_api` (data access only). A one-off bootstrap job on the official
   PostgreSQL image runs a mounted SQL script that creates the database and roles and
   sets default privileges, so tables created by later migrations are reachable by the
   API role without a grants job after each migration. A `<module>_worker` role arrives
   with the first Worker. The administrator role is used only by the bootstrap job.
5. **Start order.** PostgreSQL healthy, then bootstrap, then the migration job, then the
   module API, then the Gateway. API and Worker never migrate.
6. **Runtime hardening** on every long-running container: read-only root, `tmpfs` for
   `/tmp`, all capabilities dropped, `no-new-privileges`, `init`, memory limit, log
   rotation, health check, restart policy. Locally, ports bind to `127.0.0.1` only; on a
   server only the Gateway publishes a port.
7. **Trusting the Gateway.** The API trusts forwarded headers from the Gateway's network
   range (CIDR) instead of a fixed Gateway IP, so no fixed subnet or static address is
   needed. `AddTrustedForwarding` gains a `ForwardedHeaders:KnownNetworks` setting.
8. **One wrapper.** `scripts/shipwise.ps1` (PowerShell 7, which also runs on Linux)
   provides `init`, `check`, `up`, `down`, `status` and `logs`. `check` verifies required
   environment keys, secret files, image references and free ports before anything starts.
9. **CI images.** On every push to `main`, CI builds every image, scans it for known
   vulnerabilities and fails on any critical finding, and pushes it to
   `ghcr.io/<owner>/shipwise-<target>` tagged with the commit hash. A smoke test brings
   the local stack up in CI and checks it end to end. Server environment files refer to
   images by commit tag or digest, never `latest`.

## Consequences

- Adding a host means a Dockerfile target, a compose service, its secrets and its row in
  the environment templates. The module-skeleton skill gains these steps.
- Decision 2 relies on compose `secrets:` with a custom target path inside
  `/run/secrets/config/`, and decision 4 on PostgreSQL default privileges covering
  schemas created by later migrations. Both are proven in the first tasks of spec 0002;
  if either fails, this ADR is amended before work continues.
- The test database used by `Verify-Platform.ps1` stays separate from the application
  stack, so tests never touch application data.
- Production backups, TLS termination, public ingress, the frontend's hosting and the
  monitoring stack are not decided here.

## Revisit when

A second production host is needed, an orchestrator is adopted, or a managed PostgreSQL
service replaces the self-run server.
