# Phase Session Template

Copy this template into a new phase doc or use it as the skeleton for a large sub-step
inside an existing phase. Keep all committed content in English.

## Session metadata

| Field | Value |
|---|---|
| Date | `YYYY-MM-DD` |
| Phase | e.g. A — Shadow kernel |
| Session scope | One or more sub-steps |
| Starting commit | |
| Ending commit | |
| Status | Planning / In progress / Blocked / Complete |

## Context and handoff

Read before starting:

- `docs/architecture/evolution/status.md`
- `docs/architecture/evolution/session-workflow.md`
- active phase doc
- `docs/architecture/current.md`
- relevant `docs/decisions/active.md` entries and `docs/selfchecks/` fact sheets

Previous handoff:

```text
Date:
Scope:
Unresolved:
Next actions:
```

## Objective

State the concrete outcome for this session in one paragraph.

## Mechanism inventory

List every touched mechanism, with evidence or `unverified`. This follows the repo
delivery-gate requirement before any code change.

| Mechanism | Current behavior | Evidence | Must change? |
|---|---|---|---|

## Work breakdown

- [ ] Step 1 — ...
- [ ] Step 2 — ...
- [ ] Step 3 — ...

## Exit criteria

- [ ] ...
- [ ] ...

## Verification plan

- [ ] Build and full test suite
- [ ] Format and existing architecture gates
- [ ] Phase-specific guards
- [ ] Replay / simulation evidence
- [ ] Runtime evidence (or recorded L0/static evidence per user policy)

## Open questions / risks

| Risk | Mitigation |
|---|---|

## Session log

| Date | Scope | Commits | Verification | Notes |
|---|---|---|---|---|

## Required doc updates at completion

- [ ] Update this phase doc's status and session log.
- [ ] Update `status.md`.
- [ ] Write/update `docs/selfchecks/` fact sheet for this delivery.
- [ ] Record decisions in `docs/decisions/active.md`.
- [ ] Update `docs/backlog/README.md` if open work changed.
- [ ] Update `docs/architecture/README.md` if directory/phase info changed.
- [ ] Update `docs/history/architecture-blueprint.md` when the historical blueprint is superseded.
