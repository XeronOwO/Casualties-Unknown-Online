# Architecture Evolution Status

Live tracker for the CUO architecture iteration.

## Summary

| Field | Value |
|---|---|
| Source baseline | `208df31` (2026-08-27) |
| Current phase | Phase E — Delete dual architecture |
| Current phase status | In progress — Phase E legacy inventory landed; first dead seam (`ItemCheckpointStore`) removed; generic Prediction Runtime deferred to Phase E (see tech-decisions.md #157) |
| Last status update | 2026-08-30 |
| Next work session | Continue Phase E: rename remaining Shadow-compatible production names, audit session reset paths, and add the no-legacy guard before further deletions. |
| Protocol/save compatibility | The new four-envelope protocol and checkpoint save stack are the production item paths. Old item packet handlers/DTOs and the corresponding `NetMsg` item enums have been fully removed; the only legacy item-frame survivor is `ItemReject` for block-break drop refusal. World/Run, WorldEntities, Players (status, limb latches, body latches, skills, carry), Enemy, and Fluids kernel/checkpoint baselines now exist; WorldEntities world-entry backfill rides the kernel checkpoint, carry state now rides `KernelEnvelope` committed batches (`NetMsg.PlayerCarryState` removed), enemy combat results (bite/lunge/proximity) now ride journal-only kernel events (`NetMsg.EnemyBite`/`EnemyLunge`/`EnemyEffect` removed), and enemy aggregate removal rides `EnemyRemovedEvent` (`NetMsg.EnemyRemoved` removed). |

## Phase status

| Phase | Status | Last updated | Evidence / notes |
|---|---|---|---|
| A — Shadow kernel | Completed | 2026-08-27 | GameState project + typed kernel + Items first slice; production shadow wired into item decision path; replay differential green on all item `.replay` files; kernel/invariant tests + defect-family mapping; isolation gate. See phase doc and self-check. |
| B — Items authority | Completed | 2026-08-28 | Kernel owns full item payload/location/revision; `ItemKernelAuthority` + `ItemProjection`; world/transfer tables are kernel-first projections; `NativeOperationCoordinator`; capability registry; temporary item checkpoint store; item authority gate. See phase doc and `docs/selfchecks/phase-b-item-authority-selfcheck.md`. |
| C — Protocol & save switch | Completed | 2026-08-28 | Protocol project + four envelopes + golden tests; `KernelProtocolService`/`KernelProtocolCommandHandler`; host wire commands, checkpoint+tail, RunEpoch/version/gap filters; guest range request/out-of-order buffering/journal fallback; named random streams in checkpoint/save/wire; checkpoint projection rebuild; latency/duplicate simulation; `KernelSaveFileStore`; spawn/pickup/drop/destroy; ItemUse/Slot/ContainerSync via CommandEnvelope; carried-fact and world-correction batch projection; item snapshot StateStream; atomic Cook batch; command-rejection feedback; old item handlers/DTOs/NetMsg enums fully removed. See `docs/selfchecks/phase-c-protocol-core-selfcheck.md`. |
| D — Full domain migration | Completed | 2026-08-30 | World/Run/Epoch and WorldEntities authority switches complete. Players terminal-status, body-level terminal latches, limb latches, skill facts and carry relations, Enemy/Entity lifecycle-health, and Fluids region-checkpoint kernel baselines landed; the carry wire is fully cut over to kernel committed batches; cross-player take/heal/use now sync host-recipient and wear-to-host item ownership/state into the item kernel, guest replay kernel receives the same facts, and the take/heal/use result messages themselves are routed through journal-only kernel events with host+guest projection (legacy `NetMsg.PlayerInventoryTransfer` / `PlayerHealResult` / `PlayerItemUseResult` and handlers removed); player stream lifecycle audit (update-only + explicit join/leave) is covered; player/enemy high-frequency streams now ride `StateStreamEnvelope` over `KernelEnvelope` with the old `PlayerState`/`PlayerStateReport`/`EnemyState` high-frequency wire paths removed; Fluids host grid derives and commits the coarse kernel checkpoint; enemy 20 Hz stream is update-only with explicit kernel `EnemyRemovedEvent` removal, a session-scoped resurrection guard, and a terminal revision guard preventing stale stream rollback of kernel health; guest WorldEntities checkpoint projection landed and the legacy WorldEntities snapshot wire was removed; Players reconnect/re-entry restores now project kernel terminal facts (`PlayerKernelRestoreProjection`) over the saved snapshot, while continuous snapshot fields remain snapshot-owned and carry continues through the checkpoint/committed-batch carry projection; Enemy world-entry/reconnect snapshots and guest full-snapshot application now project kernel enemy terminal facts (`EnemyKernelRestoreProjection`) over the runtime enemy buffers, while continuous enemy presentation fields remain snapshot/stream-owned; enemy bite/lunge/proximity combat results are now journal-only Entities domain events (`RecordEnemyBiteCommand`/`RecordEnemyLungeCommand`/`RecordEnemyEffectCommand`) projected back through `EnemyCombatKernelProjection`, with legacy `NetMsg.EnemyBite`/`EnemyLunge`/`EnemyEffect` direct result wire and handlers removed; the guest-side `FluidKernelReadProjection` now rebuilds the kernel fluid-region read model from checkpoint/guest batches, WorldEntities rejects positive health reports for destroyed building entities (idempotent zero reports remain valid), and enemy aggregate removal now rides `EnemyRemovedEvent` through `KernelEnvelope` (`NetMsg.EnemyRemoved` removed); enemy removal is terminal in the kernel with persisted `EnemyStateTable.Removed` tombstones, post-removal upserts rejected, replay-safe event reduction, and guest checkpoint-restore seeding of the runtime removed set (`ProtocolVersion.Current` reset to 1 as the unreleased baseline); the WorldEntities kernel now has a trap state-machine shadow, live production reporting, and guest checkpoint projection (`TrapPhase` + `RecordTrapStateCommand`/`TrapStateChangedEvent`, checkpoint/wire/save round-trip, illegal-transition and disabled-terminal invariants, `TrapStateProfiles` mapping `EntityEventKind` edges into kernel commands, and non-one-shot state replay on guest checkpoint restore; epoch-isolation property tests cover fresh-epoch kernels with no old-epoch residue and rejection of old-epoch commands/batches; trap-state profile classification is locked by explicit tests; the kernel has atomic `CompositeGameCommand` multi-domain batches). 1794 tests green. See the phase-d selfcheck set in `docs/selfchecks/`. |
| E — Delete dual architecture | In progress | 2026-08-30 | Depends on D. No legacy surfaces may remain. Inventory + first dead-seam removal landed in `docs/selfchecks/phase-e-legacy-inventory-selfcheck.md`. |

## Phase completion log

Each completed phase should append one row here.

| Date | Phase | Commit / artifacts | Key evidence | Handoff |
|---|---|---|---|---|
| 2026-08-27 | A — Shadow kernel | `91efd68` foundation; `00d6791` defect-family tests; `89eebf1` production shadow + replay differential | 1594 tests green; build/format/architecture/event/isolation gates pass; all 30 item replays produce zero kernel semantic diff | Phase B Items authority depends on this phase; not started per current scope. |
| 2026-08-28 | B — Items authority | `bf40394` | Full test suite green; architecture + item authority gates pass; item/replay diff green; capability/native-coordinator/checkpoint contracts tested | Phase C protocol/save switch is next; native coordinator patch-site absorption and full capability row expansion are Phase C follow-ups. |
| 2026-08-28 | C — Protocol & save switch | See current commit | Full test suite green after the legacy item handler/DTO/NetMsg removal; build/format/architecture/event/entity gates pass; container/cook/snapshot network simulation on the new envelopes; carried/correction projection and command-rejection feedback covered. | Phase D full domain migration is next. |
| 2026-08-30 | D — Full domain migration | See current commit | 1794 tests green before closure; all Phase D checklist items resolved; 4.3 prediction/rollback boundary closed by deferring the generic Prediction Runtime to Phase E (tech-decisions.md #157); per-domain selfchecks plus `docs/selfchecks/phase-d-full-domain-migration-selfcheck.md` | Phase E delete dual architecture is next; start with the legacy inventory including ad-hoc prediction/rollback caches. |


## How to update this file

A phase is only "completed" after:

1. The phase doc's exit criteria are met and verified.
2. The phase self-check fact sheet exists under `docs/selfchecks/`.
3. Relevant decisions are recorded in `docs/tech-decisions.md`.
4. The row above is appended.

The next session should start from `Current phase` and the active phase doc's
`Next actions`, not from this file alone.
