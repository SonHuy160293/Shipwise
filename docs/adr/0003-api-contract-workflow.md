# 0003 API contract: generated from the API host and checked by a test

Status: accepted · Date: 2026-11-17 · Decider: owner · Related: spec 0001

## Context

`AGENTS.md` says this repository owns the HTTP contract at
`contracts/openapi/shipwise.v1.json`, and that contract changes land here before the
frontend regenerates its types. Nothing yet says how that file is produced or how drift is
caught. Spec 0001 adds the first endpoint, so the decision is due.

Facts:
- The Identity API already serves an OpenAPI document at `/swagger/v1/swagger.json`
  (Swashbuckle, wired by `AddConfiguredSwagger`).
- The frontend generates `src/lib/api/generated/schema.d.ts` from a file or URL named by
  `OPENAPI_SPEC` (`Shipwise-Web/scripts/generate-api.mjs`) and commits the result. Its CI
  does not check out this repository.
- Every response is wrapped as `{ success, data, error }` by `ApiResponseEnvelopeFilter`,
  but `ApiResponse.Data` is typed `object`, so the generated document cannot describe the
  payload unless each action declares it.
- The document title is fixed to "Company.Project API" in `SwaggerExtensions.cs`.

## Options

| Option | For | Against |
|---|---|---|
| A. Export the host's document in a test and compare it with the committed file | No new package or build step; drift fails `Verify-Platform.ps1` and CI; the file is always what the code serves | The contract follows the code, so the spec's API section is the design-time contract |
| B. Hand-write the OpenAPI file first, then implement to it | True contract-first; frontend can start before the backend | Two sources to keep equal; needs a validator to prove the code matches |
| C. Generate at build time with a CLI tool | Standard tooling | New package and build step; slower builds |

## Decision

Option A.

1. **Design-time contract.** The approved spec's "API contract" section is what both sides
   build to. A change to it is a spec change.
2. **Committed contract.** `contracts/openapi/shipwise.v1.json` is the document the
   Identity API serves, formatted with stable indentation and property order.
3. **Drift check.** A test in `tests/Shipwise.Identity.Tests` starts the host, reads
   `/swagger/v1/swagger.json` and compares it with the committed file. It fails with a
   message that says how to regenerate. Setting `SHIPWISE_UPDATE_CONTRACT=1` makes the same
   test write the file instead. No database is needed.
4. **Typed envelope.** Each action declares its responses with `[ProducesResponseType]`
   using documentation-only generic types added to `Shipwise.AspNetCore`
   (`ApiSuccessResponse<T>` for `data`, `ApiFailureResponse` for `error`), so the document
   and the frontend types describe the real payload. Runtime wrapping stays in the filter.
5. **Document quality.** The fixed title becomes configurable and the Identity API sets
   "Shipwise API". Schema names are readable, C# nullability decides `nullable` and
   `required`, and paths are lowercase (consequence 3 explains why).
6. **Frontend.** After a backend contract change is pushed, the frontend data-layer task
   runs `npm run api:generate` against the sibling checkout and commits the generated
   file. Generated files are never edited by hand.
7. **One file for now.** While Identity is the only API host, its document is the whole
   contract. When a second host appears, a new ADR decides between one file per module
   and a merged file.

## Consequences

- Backend and frontend cannot be built in parallel against a hand-written contract; the
  order stays backend first, then frontend data layer, then screens.
- The frontend's generated types can lag the committed contract, because its CI cannot
  see this repository. Review catches it for now; a pinned copy or a cross-repository
  check is a later decision.
- Decision 4 was checked on 2026-11-17 with a throwaway host on the real platform Swagger
  setup. Declaring `ApiSuccessResponse<RegisterUserResponse>` makes `data` reference the
  real payload schema, so the approach works. The same check found three defects in the
  current Swagger setup that the first contract must fix, because they would otherwise be
  frozen into the frontend's generated types:
  - schema names come out as `ApiSuccessResponse`1[RegisterUserResponse]`; set readable
    schema ids such as `ApiSuccessResponseOfRegisterUserResponse`;
  - every string is `nullable` and nothing is `required`, so the frontend would treat
    every field as optional; describe C# nullability (non-nullable means required);
  - paths keep the controller's casing (`/api/v1/Auth/register`); emit lowercase paths
    so the document matches the routes the spec defines.

## Revisit when

A second API host ships, frontend and backend need to work in parallel, or the lag in
consequence 2 causes a real defect.
