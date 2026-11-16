## What and why

<!-- Spec and task, e.g. "Spec 0001, task 1". For the small change lane write "Small change" and one sentence on why no spec is needed. -->

- Spec / task:
- Acceptance criteria covered:
- Linked pull request in Shipwise-Web:

## Changes

<!-- What changed, in a few lines. -->

## Verification

<!-- Every command you actually ran. Result is PASSED, FAILED or NOT_RUN; explain every NOT_RUN. -->

| Command | Result | Notes |
|---|---|---|
| `scripts/Verify-Platform.ps1` | | |

- Skipped infrastructure tests (list, or "none"):
- New analyzer warnings (list, or "none"):

## Database (delete if no database change)

- [ ] ef-migrations checklist (section 7) passes
- [ ] `has-pending-model-changes` prints "No changes"
- Destructive: no

<details><summary>Idempotent SQL script</summary>

```sql
```

</details>

## Contract (delete if the HTTP contract did not change)

- [ ] `contracts/openapi/shipwise.v1.json` regenerated and committed
- [ ] The change matches the spec's API contract section

## Deviations and risks

- Deviations from the spec (each needs an approved change log line, or "none"):
- Known risks or follow-ups:
