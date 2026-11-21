# 0002 Organizations, fixed roles, and the role per organization carried in the token

Status: accepted · Date: 2026-11-17 · Decider: owner · Related: ADR 0001

## Context

One person can belong to many organizations and hold a different role in each: PM in
organization A, BA in organization B. What they may do depends on the organization they
act in.

The platform's `[PermissionAuthorize]` reads one flat list of `Permission` claims from the
token, plus `TenantIds` and `BranchIds` claims (`PermissionAuthorizeAttribute.cs`,
`CustomClaims.cs`). A flat list has no organization attached, so as it stands it cannot
express a different role per organization.

The platform already calls `IAuthenticatedContextValidator.IsActiveAsync(userId,
sessionId)` on every authenticated request (`CustomJwtBearerEvents.TokenValidated`), so
one server-side check per request exists whatever is decided here.

The owner decided: Shipwise defines a fixed set of roles; a person has exactly one role
per organization; any registered user can create an organization and becomes its owner;
everyone else joins by invitation; and permissions are read from the token, not looked up
per request.

## Options

| Option | How it works | For | Against |
|---|---|---|---|
| A. Server looks up the role per request | The token says only who the user is; the server loads the membership for the organization in the request | Changes apply at once; small token | A membership lookup on every organization request |
| B. Flat permissions in the token | The platform attribute as it is | No lookup | Cannot express a role per organization |
| **C. Role per organization in the token** | The token lists each organization the user belongs to with their role there; the server reads the role for the organization in the request | No membership lookup; one sign-in covers every organization | A change is visible only after the token is refreshed; the token grows with the number of organizations |
| D. One token per organization | The token carries one active organization and its permissions; switching organization fetches a new token | Small token; reuses the attribute | More token handling in the frontend; the same delay as C |

## Decision

Option C.

1. **Module.** Organizations, memberships and roles live in the Identity module (database
   `shipwise_identity`, schema `identity`), next to users. Modules cannot share foreign
   keys, and these four are one consistency boundary.
2. **Membership.** A membership links one user to one organization; at most one per pair.
   It carries exactly one role.
3. **Roles.** Fixed by Shipwise: Owner, Admin, PM, BA, Developer, QA, Viewer. Stored as a
   `smallint` enum with a check constraint; existing numbers are never reused. Each role
   also has a stable lowercase code (`owner`, `admin`, `pm`, `ba`, `developer`, `qa`,
   `viewer`) that is never renamed, because it travels in tokens.
4. **Permissions.** Each role maps to permission codes (`<area>.<resource>.<action>`, for
   example `organization.members.invite`). The mapping is code, not data: changing it is a
   reviewed change, and it takes effect on deploy without reissuing tokens. Each feature
   spec lists the codes it adds and which roles hold them.
   - Owner: everything, including deleting the organization and transferring ownership.
   - Admin: manages members and settings, except ownership.
   - Viewer: read only.
   - PM, BA, Developer, QA: until delivery features exist they differ only by name; each
     feature spec gives them their permissions.
5. **What the token carries.** Besides the user and session ids, one claim holding the
   user's memberships as organization id and role code pairs. It carries roles, not
   permissions, so the token stays small and a permission change needs no new token.
6. **How a request is authorized.** Organization-scoped routes are
   `/api/v1/organizations/{organizationId}/...`. The organization id comes from the route,
   never from the body. The server reads the caller's role for that organization from the
   token and checks the permission code against the role mapping.
   - The organization is not in the token (not a member, or it does not exist): 404.
   - The role does not hold the permission: 403.
   - Every organization-scoped query still filters by `OrganizationId`.
7. **Freshness.** A new membership or a role change becomes visible when the access token
   is next refreshed, so access tokens are short-lived (lifetime set in the sessions ADR).
   Losing access must not wait for that: when a member is removed or their role is
   lowered, tokens issued before the change stop being accepted. The check the platform
   already runs on every request (`IAuthenticatedContextValidator`) does this by comparing
   the token with the user's last membership change. The exact mechanism is fixed in the
   sessions ADR.
8. **Creating and joining.** Any registered user can create an organization and becomes
   its Owner. Others join by invitation only. An organization always has at least one
   Owner.

## Concern raised by the planner, decided against by the owner

The planner recommended option A. Its argument: the platform validates the session on
every request anyway, so option C saves only the membership lookup, while it adds a
freshness problem that decision 7 then has to solve through that same per-request check.
The owner chose C. The decision stands; this note is kept so reviewers know it was weighed.

## Consequences

- The platform's `[PermissionAuthorize]` cannot be used as it is: it reads flat
  `Permission` claims and has no notion of an organization. The spec that adds the first
  organization-scoped endpoint adds an organization-aware check (organization id from the
  route, role from the membership claim) and updates the cqrs-api skill section 5.
  `TenantIds` and `BranchIds` stay unused.
- The sessions ADR (due with the sign-in spec) must define the membership claim's exact name and
  shape, the access token lifetime, the refresh flow, and the mechanism in decision 7.
- The token grows by roughly 50 bytes per organization. Dozens are fine; a user in
  hundreds of organizations would exceed common header limits, so the sessions ADR sets a
  membership limit or a fallback.
- Other modules authorize from the token without calling Identity. The role codes they
  need are published in `src/Contracts`.
- Custom roles per organization are out. Adding them later means moving roles and
  permissions from code to tables, and tokens would then need permissions or a lookup: a
  new ADR.
- A person who needs two roles in one organization cannot have them; the fixed role list
  must contain the combination, or this decision is revisited.

## Revisit when

An organization needs roles Shipwise does not define, a person needs several roles in one
organization, users join enough organizations to make tokens too large, or a stale token
causes an access incident.
