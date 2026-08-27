# Architecture Evolution Status

Live tracker for the CUO architecture iteration.

## Summary

| Field | Value |
|---|---|
| Source baseline | `208df31` (2026-08-27) |
| Current phase | Phase A — Shadow kernel |
| Current phase status | Completed |
| Last status update | 2026-08-27 |
| Next work session | Phase A complete. Phase B is not started; per current scope it should begin only on explicit request. |
| Protocol/save compatibility | Existing protocol and save remain untouched until Phase C. Breaking changes are allowed by project policy, but only when a phase explicitly reaches them. |

## Phase status

| Phase | Status | Last updated | Evidence / notes |
|---|---|---|---|
| A — Shadow kernel | Completed | 2026-08-27 | GameState project + typed kernel + Items first slice; production shadow wired into item decision path; replay differential green on all 30 item `.replay` files; kernel/invariant tests + defect-family mapping; isolation gate. See phase doc and self-check. |
| B — Items authority | Not started | 2026-08-27 | Depends on A exit criteria. |
| C — Protocol & save switch | Not started | 2026-08-27 | Depends on B. First opportunity to remove old wire DTOs and save DTOs. |
| D — Full domain migration | Not started | 2026-08-27 | Depends on C. Domain order is defined in the phase doc. |
| E — Delete dual architecture | Not started | 2026-08-27 | Depends on D. No legacy surfaces may remain. |

## Phase completion log

Each completed phase should append one row here.

| Date | Phase | Commit / artifacts | Key evidence | Handoff |
|---|---|---|---|---|
| 2026-08-27 | A — Shadow kernel | `91efd68` foundation; `00d6791` defect-family tests; `89eebf1` production shadow + replay differential | 1594 tests green; build/format/architecture/event/isolation gates pass; all 30 item replays produce zero kernel semantic diff | Phase B Items authority depends on this phase; not started per current scope. |

## How to update this file

A phase is only "completed" after:

1. The phase doc's exit criteria are met and verified.
2. The phase self-check fact sheet exists under `docs/selfchecks/`.
3. Relevant decisions are recorded in `docs/tech-decisions.md`.
4. The row above is appended.

The next session should start from `Current phase` and the active phase doc's
`Next actions`, not from this file alone.
