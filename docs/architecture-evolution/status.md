# Architecture Evolution Status

Live tracker for the CUO architecture iteration.

## Summary

| Field | Value |
|---|---|
| Source baseline | `208df31` (2026-08-27) |
| Current phase | Phase D — Full domain migration |
| Current phase status | In progress — all six Phase D domain areas have kernel foundations; Fluids host production wiring landed; enemy stream lifecycle slice landed; Players carry kernel shadow + production projection and limb terminal facts landed; WorldEntities guest checkpoint projection landed and the legacy world-entity snapshot wire removed |
| Last status update | 2026-08-29 |
| Next work session | Continue Phase D: finish player cross-player interaction kernel supplements and high-frequency stream alignment. |
| Protocol/save compatibility | The new four-envelope protocol and checkpoint save stack are the production item paths. Old item packet handlers/DTOs and the corresponding `NetMsg` item enums have been fully removed; the only legacy item-frame survivor is `ItemReject` for block-break drop refusal. World/Run, WorldEntities, Players (status, limb latches, carry), Enemy, and Fluids kernel/checkpoint baselines now exist; WorldEntities world-entry backfill rides the kernel checkpoint. |

## Phase status

| Phase | Status | Last updated | Evidence / notes |
|---|---|---|---|
| A — Shadow kernel | Completed | 2026-08-27 | GameState project + typed kernel + Items first slice; production shadow wired into item decision path; replay differential green on all item `.replay` files; kernel/invariant tests + defect-family mapping; isolation gate. See phase doc and self-check. |
| B — Items authority | Completed | 2026-08-28 | Kernel owns full item payload/location/revision; `ItemKernelAuthority` + `ItemProjection`; world/transfer tables are kernel-first projections; `NativeOperationCoordinator`; capability registry; temporary item checkpoint store; item authority gate. See phase doc and `docs/selfchecks/phase-b-item-authority-selfcheck.md`. |
| C — Protocol & save switch | Completed | 2026-08-28 | Protocol project + four envelopes + golden tests; `KernelProtocolService`/`KernelProtocolCommandHandler`; host wire commands, checkpoint+tail, RunEpoch/version/gap filters; guest range request/out-of-order buffering/journal fallback; named random streams in checkpoint/save/wire; checkpoint projection rebuild; latency/duplicate simulation; `KernelSaveFileStore`; spawn/pickup/drop/destroy; ItemUse/Slot/ContainerSync via CommandEnvelope; carried-fact and world-correction batch projection; item snapshot StateStream; atomic Cook batch; command-rejection feedback; old item handlers/DTOs/NetMsg enums fully removed. See `docs/selfchecks/phase-c-protocol-core-selfcheck.md`. |
| D — Full domain migration | In progress (all domain areas have kernel foundations) | 2026-08-29 | World/Run/Epoch and WorldEntities authority switches complete. Players terminal-status, limb latches and carry relations, Enemy/Entity lifecycle-health, and Fluids region-checkpoint kernel baselines landed; Fluids host grid derives and commits the coarse kernel checkpoint; enemy 20 Hz stream is update-only with explicit reliable removal; guest WorldEntities checkpoint projection landed and the legacy WorldEntities snapshot wire was removed. 1687 tests green. See the phase-d selfcheck set in `docs/selfchecks/`. |
| E — Delete dual architecture | Not started | 2026-08-28 | Depends on D. No legacy surfaces may remain. |

## Phase completion log

Each completed phase should append one row here.

| Date | Phase | Commit / artifacts | Key evidence | Handoff |
|---|---|---|---|---|
| 2026-08-27 | A — Shadow kernel | `91efd68` foundation; `00d6791` defect-family tests; `89eebf1` production shadow + replay differential | 1594 tests green; build/format/architecture/event/isolation gates pass; all 30 item replays produce zero kernel semantic diff | Phase B Items authority depends on this phase; not started per current scope. |
| 2026-08-28 | B — Items authority | `bf40394` | Full test suite green; architecture + item authority gates pass; item/replay diff green; capability/native-coordinator/checkpoint contracts tested | Phase C protocol/save switch is next; native coordinator patch-site absorption and full capability row expansion are Phase C follow-ups. |
| 2026-08-28 | C — Protocol & save switch | See current commit | Full test suite green after the legacy item handler/DTO/NetMsg removal; build/format/architecture/event/entity gates pass; container/cook/snapshot network simulation on the new envelopes; carried/correction projection and command-rejection feedback covered. | Phase D full domain migration is next. |

## How to update this file

A phase is only "completed" after:

1. The phase doc's exit criteria are met and verified.
2. The phase self-check fact sheet exists under `docs/selfchecks/`.
3. Relevant decisions are recorded in `docs/tech-decisions.md`.
4. The row above is appended.

The next session should start from `Current phase` and the active phase doc's
`Next actions`, not from this file alone.
