# Architecture Evolution Status

Live tracker for the CUO architecture iteration.

## Summary

| Field | Value |
|---|---|
| Source baseline | `208df31` (2026-08-27) |
| Current phase | Phase A — Shadow kernel |
| Current phase status | Not started |
| Last status update | 2026-08-27 (docs-only planning) |
| Next work session | Start Phase A per `phase-a-shadow-kernel.md` |
| Protocol/save compatibility | Existing protocol and save remain untouched until Phase C. Breaking changes are allowed by project policy, but only when a phase explicitly reaches them. |

## Phase status

| Phase | Status | Last updated | Evidence / notes |
|---|---|---|---|
| A — Shadow kernel | Not started | 2026-08-27 | Planning complete; implementation begins in a new session. |
| B — Items authority | Not started | 2026-08-27 | Depends on A exit criteria. |
| C — Protocol & save switch | Not started | 2026-08-27 | Depends on B. First opportunity to remove old wire DTOs and save DTOs. |
| D — Full domain migration | Not started | 2026-08-27 | Depends on C. Domain order is defined in the phase doc. |
| E — Delete dual architecture | Not started | 2026-08-27 | Depends on D. No legacy surfaces may remain. |

## Phase completion log

Each completed phase should append one row here.

| Date | Phase | Commit / artifacts | Key evidence | Handoff |
|---|---|---|---|---|
| _(none yet)_ | | | | |

## How to update this file

A phase is only "completed" after:

1. The phase doc's exit criteria are met and verified.
2. The phase self-check fact sheet exists under `docs/selfchecks/`.
3. Relevant decisions are recorded in `docs/tech-decisions.md`.
4. The row above is appended.

The next session should start from `Current phase` and the active phase doc's
`Next actions`, not from this file alone.
