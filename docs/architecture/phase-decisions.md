# Phase A–E Decision Record

Compressed record of the architecture-evolution decisions (#126–#158). Full prose is in git history; the active normative subset is in `docs/decisions/active.md`.

## Phase A — Shadow kernel

> Evidence: docs/evidence/selfchecks/architecture/phase-a-kernel-foundation-selfcheck.md

| # | Decision | One-line essence |
|---:|---|---|
| 126 | Phase A shadow kernel — typed deterministic GameState beside the item path | Closed 2026-08-27. The architecture evolution Phase A is complete: a new `CasualtiesUnknownOnline.GameState` project owns a typed deterministic kernel and an Items first slice, while the old item path… |

## Phase B — Items authority

> Evidence: docs/evidence/selfchecks/architecture/phase-b-item-authority-selfcheck.md

| # | Decision | One-line essence |
|---:|---|---|
| 127 | Phase B — items become the first authoritative kernel domain | Closed 2026-08-28. Phase B switches authority from the scattered legacy item tables to the typed deterministic kernel while keeping the current wire protocol and save untouched. - **Kernel payload** —… |

## Phase C — Protocol/save switch

> Evidence: docs/evidence/selfchecks/architecture/phase-c-protocol-core-selfcheck.md

| # | Decision | One-line essence |
|---:|---|---|
| 128 | Phase C protocol/save core — completed cutover (2026-08-28) | Started 2026-08-28. This entry records the Phase C decisions through the final cutover. The phase is complete; old item packet handlers, old item DTOs, and the corresponding `NetMsg` item enums have b… |

## Phase D — Full domain migration

> Evidence: docs/evidence/selfchecks/architecture/phase-d-full-domain-migration-selfcheck.md

| # | Decision | One-line essence |
|---:|---|---|
| 129 | High-frequency player/enemy streams moved to StateStreamEnvelope (2026-08-29) | The Phase D high-frequency stream unification slice now routes player and enemy continuous/presentation streams through the Phase C `StateStreamEnvelope` instead of the legacy direct entity messages. … |
| 130 | Push is transient presentation, not a kernel fact (2026-08-29) | Cross-player push/shove is a presentation-only operation. - **No kernel command/event** — push creates/updates no durable relation, ownership, health, or item fact; therefore it does not enter the Pha… |
| 131 | Take/heal/use results ride journal-only kernel events (2026-08-29) | Cross-player inventory take, heal, and consumable/wearable use are host operations whose durable item/player facts already ride the item and player kernel domains. The remaining result messages are no… |
| 132 | Enemy combat results ride journal-only kernel events (2026-08-29) | Enemy bite, crystal lunge, and proximity side-effect results are local-compute terminal-state facts. They are now routed through the Phase C kernel protocol so no legacy bidirectional result wire surv… |
| 133 | Fluid guest kernel read projection (2026-08-29) | The coarse fluid-region checkpoint needs a guest-side rebuildable read view separate from the high-frequency RLE grid stream. - **Projection** — `FluidKernelReadProjection` mirrors `FluidStateTable` o… |
| 134 | Destroyed building entities cannot be revived by health reports (2026-08-29) | WorldEntities adds the first 4.2 lifecycle invariant. - **Rule** — once `BuildingEntityHealthFact.Health` is recorded as `0`, a later positive health report for the same position is rejected with `Rej… |
| 135 | Enemy aggregate removal rides kernel batches (2026-08-29) | The enemy 20 Hz stream is update-only, so aggregate lifecycle must be explicit. It no longer uses a dedicated `NetMsg.EnemyRemoved` frame; removals are committed as kernel `EnemyRemovedEvent` and trav… |
| 136 | Enemy removal terminal tombstones in kernel (2026-08-29) | Enemy lifecycle is now final in the kernel, not only in the guest runtime buffer. - **Rule** — `EnemyStateTable.Removed` holds terminal tombstones. A later `UpsertEnemyCommand` for a removed id is rej… |
| 137 | Protocol version baseline before first release (2026-08-29) | Since CUO has no released compatibility surface, `ProtocolVersion.Current` is reset to 1 instead of continuing the pre-release bump sequence. New wire/save shapes do not need a monotonic version until… |
| 138 | Trap state machine kernel shadow (2026-08-29) | Phase D 4.2 starts with a kernel-shadow state vocabulary for traps/mechanisms; the native adapter remains the trigger source until the production authority switch. - **State model** — `TrapPhase` (`Ar… |
| 139 | Trap state live production reporting (2026-08-29) | The 4.2 shadow state machine is now fed by the live entity-event channel. - **Mapping** — `TrapStateProfiles` maps `EntityEventKind` to `TrapPhase`: pre-trigger edges (`MinePressed`, `CrystalUnstableT… |
| 140 | Guest checkpoint projection of non-one-shot trap states (2026-08-29) | Late-joiner trap replay now includes kernel trap state-machine facts, not only one-shot consumptions. - **Rule** — `WorldEntityKernelProjection` emits one-shot consumption facts as before, plus non-on… |
| 141 | Atomic composite kernel commands (2026-08-29) | Cross-domain batches need a kernel primitive before 4.2 trap damage/drop can be made atomic. - **Command** — `CompositeGameCommand` carries a list of inner typed domain commands and is not sent over t… |
| 142 | Trap trigger kernel facts ride one atomic composite (2026-08-29) | The first production 4.2 use of `CompositeGameCommand`: a live trap trigger's kernel facts are now committed as one batch instead of two separate batches. - **Host-local path** — `EntityEventChannel.S… |
| 143 | Building-death drop provenance markers (2026-08-29) | The first prep step for the trap-drop atomic collection: distinguish the cross-frame item drops produced by `BuildingEntity.Update`'s local death branch from block drops and ordinary runtime spawns. -… |
| 144 | Destructive trap item drops ride one atomic composite (2026-08-29) | The 4.2 cross-domain sub-step that was open after the atomic trap trigger composite: the item drops produced asynchronously by a destructive trap's `BuildingEntity.Update` death branch now travel with… |
| 145 | Guest fluid kernel read projection reaches the Game Adapter (2026-08-29) | The `FluidKernelReadProjection` existed as a Runtime rebuildable view; this adds the Game Adapter-side consumer so the coarse fluid facts are observable and can drive future guest local-simulation/res… |
| 146 | Enemy combat policy extraction (2026-08-29) | First behavior-preserving step toward absorbing `EnemyCombatDirector`/targeting into kernel processes: pull the pure thresholds out of the 553-line adapter coordinator and split the local lunge-trace … |
| 147 | Enemy target resolver extraction (2026-08-29) | Second behavior-preserving step toward kernelizing `EnemyCombatDirector`: move the target-view responsibility out of the adapter coordinator. - **Resolver** — `EnemyTargetResolver` owns the cached can… |
| 148 | Enemy combat order policy extraction (2026-08-30) | Third behavior-preserving step: extract the remaining host-side apply-path branches from `EnemyCombatDirector` into a pure Runtime decision surface. - **Order policy** — `EnemyCombatOrderPolicy` (Runt… |
| 149 | Spider-bite local-path handoff to the order policy (2026-08-30) | The spider-bite arbitration no longer encodes the local-exclusion rule; the order policy owns the apply path. - **Arbitration** — `EnemyCombatArbitration.SelectBiteVictim` returns the nearest in-range… |
| 150 | Fluids guest projection and convergence semantics (2026-08-30) | The Phase D fluids checklist is closed by recording the existing design rather than adding a second grid path. - **Guest projection** — the guest never simulates fluid; `FluidRegionApplication` applie… |
| 151 | WorldEntities 4.2 checklist closure (2026-08-30) | The remaining 4.2 items are already implemented by the existing WorldEntities kernel and adapter projection path. - **Building lifecycle** — `BuildingEntityHealthFact` / `BuildingEntityHealthUpdatedEv… |
| 152 | Player durable skills move into the Players kernel domain (2026-08-30) | The first concrete 4.3 slice closes the "skills" open question in the player domain boundary: durable skill facts are now kernel-owned instead of remaining exclusively in the character-data snapshot p… |
| 153 | Player kernel identity floor on entity-sync start (2026-08-30) | A second 4.3 slice makes player identity explicit in the kernel: the host creates a default `PlayerState` row as soon as a member's entity sync starts. - **Ensure** — `PlayerKernelStatusProjection.Ens… |
| 154 | Explicit cross-player interaction authority policies (2026-08-30) | The 4.3 "define authority policies" item gains a named runtime contract and is used by the result journal path. - **Policies** — `PlayerInteractionAuthority` distinguishes `HostValidatedNoPrediction`,… |
| 155 | Carry carrier liveness invariant (2026-08-30) | The player domain now enforces one missing piece of relation consistency: a player who acts as a carrier in a kernel carry relation must be alive and conscious. - **Invariant** — `PlayerDomainModule.A… |
| 156 | Player/item ownership consistency and death preservation (2026-08-30) | The 4.3 death + inventory consistency item is addressed as a kernel ownership consistency rule plus a behavior lock, without inventing a nonexistent death-drop path. - **Ownership invariant** — when t… |
| 157 | Phase D 4.3 closure: cross-player prediction/rollback boundary (2026-08-30) | The final 4.3 item is resolved as a boundary decision, not a new runtime slice: cross-player operations are not client-predicted, and the generic Prediction Runtime is deferred to Phase E. - **No cros… |

## Phase E — Delete dual architecture

> Evidence: docs/evidence/selfchecks/architecture/phase-e-legacy-inventory-selfcheck.md

| # | Decision | One-line essence |
|---:|---|---|
| 158 | Phase E kernel reset centralization and guard suite (2026-08-30) | Phase E cleanup decisions recorded as the first concrete batches land. - **Kernel reset ownership** — `ItemKernelAuthority.ResetForSession()` is the authoritative fresh-epoch reset. It now lives in `K… |

