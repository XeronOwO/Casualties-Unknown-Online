# Architecture Evolution Status

Live tracker for the CUO architecture iteration.

## Summary

| Field | Value |
|---|---|
| Source baseline | `208df31` (2026-08-27) |
| Current phase | Phase C — Protocol & save switch |
| Current phase status | In progress |
| Last status update | 2026-08-28 |
| Next work session | Continue Phase C: complete old item DTO removal, missing-range requests, StateStream projection, named random streams, projection-rebuild tests, tech decisions, and commit. |
| Protocol/save compatibility | Phase C has begun: new four-envelope protocol and checkpoint save stack exist; some old item wire families remain for projection/stream paths until the cutover completes. |

## Phase status

| Phase | Status | Last updated | Evidence / notes |
|---|---|---|---|
| A — Shadow kernel | Completed | 2026-08-27 | GameState project + typed kernel + Items first slice; production shadow wired into item decision path; replay differential green on all item `.replay` files; kernel/invariant tests + defect-family mapping; isolation gate. See phase doc and self-check. |
| B — Items authority | Completed | 2026-08-28 | Kernel owns full item payload/location/revision; `ItemKernelAuthority` + `ItemProjection`; world/transfer tables are kernel-first projections; `NativeOperationCoordinator`; capability registry; temporary item checkpoint store; item authority gate. See phase doc and `docs/selfchecks/phase-b-item-authority-selfcheck.md`. |
| C — Protocol & save switch | In progress | 2026-08-28 | Protocol project + four envelopes + golden tests; `KernelProtocolService`/handler; host wire commands, checkpoint+tail, RunEpoch/version/gap filters; `KernelSaveFileStore`; guest world projection; spawn/pickup/drop/destroy production sends switched. Remaining: full old item DTO removal, missing-range/rebuild, StateStream, random streams, projection rebuild. See phase doc and `docs/selfchecks/phase-c-protocol-core-selfcheck.md`. |
| D — Full domain migration | Not started | 2026-08-28 | Depends on C. Domain order is defined in the phase doc. |
| E — Delete dual architecture | Not started | 2026-08-28 | Depends on D. No legacy surfaces may remain. |

## Phase completion log

Each completed phase should append one row here.

| Date | Phase | Commit / artifacts | Key evidence | Handoff |
|---|---|---|---|---|
| 2026-08-27 | A — Shadow kernel | `91efd68` foundation; `00d6791` defect-family tests; `89eebf1` production shadow + replay differential | 1594 tests green; build/format/architecture/event/isolation gates pass; all 30 item replays produce zero kernel semantic diff | Phase B Items authority depends on this phase; not started per current scope. |
| 2026-08-28 | B — Items authority | `bf40394` | Full test suite green; architecture + item authority gates pass; item/replay diff green; capability/native-coordinator/checkpoint contracts tested | Phase C protocol/save switch is next; native coordinator patch-site absorption and full capability row expansion are Phase C follow-ups. |

## How to update this file

A phase is only "completed" after:

1. The phase doc's exit criteria are met and verified.
2. The phase self-check fact sheet exists under `docs/selfchecks/`.
3. Relevant decisions are recorded in `docs/tech-decisions.md`.
4. The row above is appended.

The next session should start from `Current phase` and the active phase doc's
`Next actions`, not from this file alone.
