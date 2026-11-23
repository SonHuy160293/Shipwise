# 0005 Sessions and tokens

Status: accepted · Date: 2026-11-24 · Decider: owner · Related: ADR 0001, ADR 0002, spec 0003

## Context

ADR 0001 gives Shipwise its own accounts and passwords and leaves sessions to this ADR.
ADR 0002 puts each user's role per organization in the access token and needs this ADR
to fix the access token lifetime, the refresh flow, the membership claim, a size limit,
and how a removed member's token stops working.

What the platform already does (`ARCHITECTURE.md`, `AuthExtensions.cs`,
`CustomJwtBearerEvents.cs`):
- Every host validates bearer JWTs signed with HS256 and a shared key, with issuer,
  audience and expiry required and no clock skew.
- After validation it requires nonempty `sub` and `sid` GUIDs and calls
  `IAuthenticatedContextValidator.IsActiveAsync(userId, sessionId)` on every protected
  request. The interface requires authoritative state with no caching, and a missing
  validator rejects every token.

The frontend forbids credentials in `localStorage` and asks for cookie, CSRF and transport
rules before any session exists (`Shipwise-Web/AGENTS.md`,
`docs/architecture/api-integration.md`).

Sign-in is password-only, which is NIST SP 800-63B-4 assurance level 1: re-authenticate
at least every 30 days; an inactivity timeout is optional.

Sources read on 2026-11-24:
- [RFC 10017, OAuth 2.0 for Browser-Based Applications](https://www.rfc-editor.org/rfc/rfc10017.html):
  keeping tokens out of JavaScript is the strongest browser design.
- [RFC 9700, OAuth 2.0 Security BCP](https://www.rfc-editor.org/rfc/rfc9700.html):
  refresh tokens held by a browser must be sender-constrained or rotated.
- [NIST SP 800-63B-4 session management](https://pages.nist.gov/800-63-4/sp800-63b/session).
- [OWASP Session Management Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Session_Management_Cheat_Sheet.html):
  cookie attributes, idle and absolute timeouts.

## Options

| Option | How it works | For | Against |
|---|---|---|---|
| A. Access token in page memory, refresh token in a cookie | Sign-in returns the JWT in the body; the frontend sends it as a bearer header and refreshes through a cookie | No platform change; CSRF only on the refresh endpoints | A script injected into the page can read access tokens; the frontend handles tokens |
| B. Gateway holds the session (backend for frontend) | The browser has an opaque session cookie; the Gateway attaches a JWT to each proxied request | Strongest option in RFC 10017 | The Gateway becomes stateful and needs session data on every request |
| **C. Both tokens in HttpOnly cookies** | Sign-in sets the JWT and the refresh token as HttpOnly, SameSite=Strict cookies; hosts read the JWT from the cookie or the bearer header | Scripts never see a token; little token code in the frontend; the bearer header keeps working | CSRF must be defended on every write; a cookie holds at most 4 KB, which caps the membership claim |

## Decision

Option C.

1. **Tokens.** Identity issues a JWT access token (HS256 with the existing key) with
   `iss`, `aud`, `sub` (user id), `sid` (session id), `iat` and `exp`, valid for 10
   minutes. It also issues an opaque refresh token in the form `<sessionId>.<secret>`:
   the session id as 32 hex digits, then 32 random bytes encoded as base64url. Only the
   SHA-256 hash of the secret is stored.
2. **Cookies.** Both tokens travel only in cookies; no response body ever contains them.
   - Access cookie: `HttpOnly`, `SameSite=Strict`, `Path=/`, lasting as long as the token.
   - Refresh cookie: `HttpOnly`, `SameSite=Strict`, `Path=/api/v1/auth`, lasting until
     the session's idle deadline.
   - Outside Development both are `Secure`, named `__Host-shipwise-access` and
     `__Secure-shipwise-refresh`.
   - In Development they are not `Secure`, so they work over `http://localhost`, and are
     named `shipwise-access` and `shipwise-refresh`.
   - The frontend never reads them. Hosts accept the access token from the cookie, or
     from an `Authorization: Bearer` header for tests and non-browser clients.
3. **Sessions.** A session is a row in Identity's database, created at sign-in.
   - It ends 7 days after its last refresh (idle) or 30 days after sign-in (absolute),
     whichever comes first.
   - It also ends on sign-out, on a replacing sign-in, or when refresh-token reuse is
     detected.
   - Idle time is measured from the last refresh, not from every request, so a request
     costs a read and never a write. Active use refreshes every 10 minutes, so the two
     measures stay close.
   - Sign-out ends only the current session. Listing sessions and signing out everywhere
     are later work.
4. **Refresh rotation.** Every refresh replaces the refresh token and keeps the previous
   hash with its rotation time.
   - If the previous token is presented within 30 seconds of the rotation, a new access
     token is issued and the refresh cookie is left untouched. This covers two tabs
     refreshing at the same time, because tabs share the cookie jar.
   - If the previous token is presented later than that, the session is revoked as reuse
     and the caller gets 401.
   - The frontend sends one refresh at a time per tab, and retries a failed request only
     once.
5. **Per-request check.** Identity registers the `IAuthenticatedContextValidator`. It
   reads the session by primary key, without caching, and accepts the request only if the
   session belongs to `sub`, is not revoked, and is within both deadlines.
6. **Cross-site requests.** Both cookies are `SameSite=Strict`. In addition, shared
   middleware rejects with 403 `Request.OriginRejected` any unsafe request (`POST`,
   `PUT`, `PATCH`, `DELETE`) whose `Origin` is missing or not in the host's
   `Cors:AllowedOrigins`, when that request either:
   - carries an auth cookie, or
   - goes to an endpoint marked as needing an allowed origin (sign-in, refresh,
     sign-out).

   A request authenticated only by a bearer header, with no cookie, is not checked.
7. **Hosting constraint.** The frontend and the Gateway must be served from the same site,
   for example `app.` and `api.` under one registrable domain, or one origin behind a
   proxy as in local development. A frontend hosted on another site cannot use these
   cookies, and would need this ADR revisited.
8. **Membership claim (implemented with the first organization, spec 0004).**
   - The claim is named `orgs`. It holds a JSON array of
     `{ "id": "<organization uuid>", "role": "<role code>" }`, ordered by organization
     id.
   - A user may belong to at most 25 organizations, so the access cookie stays well under
     4 KB. Creating or joining beyond that answers 409 `Membership.LimitReached`.
9. **Freshness after a membership change (spec 0004).**
   - Each session gets a `TokensValidAfter` time. When a user's memberships change, it is
     set to now on all of that user's active sessions.
   - The validator then rejects any access token issued before that time. The frontend's
     refresh-and-retry gets a new token with the current memberships, without signing
     the user out.
   - This needs the validator interface to receive the token's issue time, a shared
     library change made in spec 0004.
10. **Signing key.** HS256 with the shared key stays while Identity is the only module
    host. Every host that validates tokens could also forge them, so the second module
    host's spec must move token signing to an asymmetric key (ES256), held privately by
    Identity, with public verification elsewhere.
11. **Other modules checking sessions.**
    - A future module host cannot read Identity's database, but the interface requires
      authoritative state.
    - The second module host's spec decides how it checks sessions: by calling an Identity
      endpoint per request, or through a shared revocation store.
    - Until then, no host other than Identity validates user tokens.

## Consequences

- `Shipwise.AspNetCore` gains three things: reading the access token from the cookie, the
  origin-check middleware, and an `[AuthenticatedUserOnly]` marker for actions that act
  only on the caller's own account and so have no permission to check. The cqrs-api
  skill documents the marker.
- Every unsafe browser request needs an allowed `Origin`, so `Cors:AllowedOrigins` must
  list the frontend's origin in every environment.
- The per-email sign-in throttle (spec 0003) is kept in memory, which assumes one Identity
  API instance (ADR 0004). A second instance needs a shared store.
- A script injected into the page can still act as the user while the page is open,
  though it cannot take a token away. Preventing script injection stays essential.
- Session rows accumulate. Each sign-in deletes that user's expired or revoked sessions,
  and a scheduled cleanup arrives with the first Worker.

## Revisit when

- The frontend must be hosted on a different site from the API.
- A mobile or third-party client needs tokens.
- A second module host is added (decisions 10 and 11).
- A user needs more than 25 organizations.
- An incident shows the 10-minute window or the 30-second grace is wrong.
