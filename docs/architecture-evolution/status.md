# Architecture Evolution Status

Live tracker for the CUO architecture iteration.

## Summary

| Field | Value |
|---|---|
| Source baseline | `208df31` (2026-08-27) |
| Current phase | Phase B — Items authority |
| Current phase status | Completed |
| Last status update | 2026-08-28 |
| Next work session | Phase B complete. Phase C (protocol/save switch) is not started; it should begin on the next explicit phase request. |
| Protocol/save compatibility | Existing protocol and save remain untouched until Phase C. Breaking changes are allowed by project policy, but only when a phase explicitly reaches them. |

## Phase status

| Phase | Status | Last updated | Evidence / notes |
|---|---|---|---|
| A — Shadow kernel | Completed | 2026-08-27 | GameState project + typed kernel + Items first slice; production shadow wired into item decision path; replay differential green on all item `.replay` files; kernel/invariant tests + defect-family mapping; isolation gate. See phase doc and self-check. |
| B — Items authority | Completed | 2026-08-28 | Kernel owns full item payload/location/revision; `ItemKernelAuthority` + `ItemProjection`; world/transfer tables are kernel-first projections; `NativeOperationCoordinator`; capability registry; temporary item checkpoint store; item authority gate. See phase doc and `docs/selfchecks/phase-b-item-authority-selfcheck.md`. |
| C — Protocol & save switch | Not started | 2026-08-28 | Depends on B. First opportunity to remove old wire DTOs and save DTOs. |
| D — Full domain migration | Not started | 2026-08-28 | Depends on C. Domain order is defined in the phase doc. |
| E — Delete dual architecture | Not started | 2026-08-28 | Depends on D. No legacy surfaces may remain. |

## Phase completion log

Each completed phase should append one row here.

| Date | Phase | Commit / artifacts | Key evidence | Handoff |
|---|---|---|---|---|
| 2026-08-27 | A — Shadow kernel | `91efd68` foundation; `00d6791` defect-family tests; `89eebf1` production shadow + replay differential | 1594 tests green; build/format/architecture/event/isolation gates pass; all 30 item replays produce zero kernel semantic diff | Phase B Items authority depends on this phase; not started per current scope. |
| 2026-08-28 | B — Items authority | _(add after commit)_ | Full test suite green; architecture + item authority gates pass; item/replay diff green; capability/native-coordinator/checkpoint contracts tested | Phase C protocol/save switch is next; native coordinator patch-site absorption and full capability row expansion are Phase C follow-ups. |

## How to update this file

A phase is only "completed" after:

1. The phase doc's exit criteria are met and verified.
2. The phase self-check fact sheet exists under `docs/selfchecks/`.
3. Relevant decisions are recorded in `docs/tech-decisions.md`.
4. The row above is appended.

The next session should start from `Current phase` and the active phase doc's
`Next actions`, not from this file alone.
