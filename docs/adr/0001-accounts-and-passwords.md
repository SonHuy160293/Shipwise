# 0001 Accounts and passwords are owned by Shipwise

Status: accepted · Date: 2026-11-17 · Decider: owner · Related: spec 0001, ADR 0002

## Context

Shipwise needs user accounts before any other feature. The platform validates tokens but
issues none: `AddConfiguredAuthentication` pins HS256 with a shared key and requires a user
id, a session id and an `IAuthenticatedContextValidator` (`ARCHITECTURE.md`). That design
assumes Shipwise issues its own tokens. The frontend forbids credentials in `localStorage`
(`Shipwise-Web/AGENTS.md`). There is no email sending yet.

The owner decided: people sign in with email and password owned by Shipwise; email
verification comes later as its own spec; spec 0001 covers registration only.

## Options

| Option | For | Against |
|---|---|---|
| A. Shipwise owns accounts, email and password | Fits the platform's token checks; no new dependency or vendor; the owner has built it before (DeliveryPilot) | Shipwise carries the full duty of care for passwords, resets and abuse |
| B. External sign-in only (Google, Microsoft) | No passwords stored | Excludes users without those accounts; token validation code must be reworked |
| C. Hosted identity service | Least security code to own | New vendor and cost; platform JWT helpers replaced; organizations and roles split across two systems |

## Decision

Option A.

1. **One account per person.** An account is independent of any organization; the same
   account joins many organizations (ADR 0002).
2. **Email is the sign-in name.** It is stored twice: as entered (trimmed) for display, and
   normalized (trimmed, lowercase invariant) for uniqueness. No provider-specific rules
   such as removing dots or plus tags.
3. **Password storage.** ASP.NET Core `PasswordHasher<TUser>`: PBKDF2-HMAC-SHA512, 100,000
   iterations, a salt per password, and a versioned format that reports when a hash should
   be upgraded. It ships in the shared framework, so no package is added. Hashes live in
   their own table, apart from the user row. Argon2id, which OWASP prefers, was not chosen
   because .NET has no built-in implementation and it would add a third-party package.
4. **Password rules.** 12 to 128 characters, any characters including spaces and Unicode,
   no composition rules, never truncated or altered. The upper bound caps hashing cost.
5. **Existing email at registration.** The API answers 409 `Registration.EmailTaken`, and
   the endpoint is rate limited. This reveals that an address has an account. It is
   accepted for now because the safe alternative (always answer "check your email") needs
   email sending.
6. **Email verification is deferred.** Accounts start unverified. Until the verification
   spec ships, no feature may treat an email address as proven; the invitation spec depends
   on it.
7. **Secrets never leave the handler.** Passwords and hashes are never logged, traced,
   returned or put in an exception message.

Sessions and tokens (who issues them, lifetime, refresh, how the browser holds them, and
the `IAuthenticatedContextValidator` implementation) are decided in a separate ADR with
spec 0002.

## Consequences

- Shipwise must later build password reset, email verification, sign-in throttling and
  breach response. Each is a spec.
- Decision 5 must be revisited in the email verification spec: switch to one uniform
  response and notify the existing account holder instead.
- Keeping credentials in their own table leaves room for external sign-in later without
  changing the user table.

## Revisit when

A third-party cryptography package is approved (move to Argon2id through the hasher's
upgrade path), external sign-in is requested, or email sending exists.
