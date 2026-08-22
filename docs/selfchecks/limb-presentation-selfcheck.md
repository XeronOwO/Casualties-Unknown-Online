# Death-Pose / Limb / Bleed / Mining Presentation-State Sync — Self-Check (2026-08-16)

Delivery fact sheet for the character-presentation backlog closeout. The
remote clone now renders the owner's limb wound state (break / dislocation /
dismember / blood / infection) from the 1 Hz character snapshot, every limb
latch travels as a dedicated full-state event (never the 1 Hz snapshot),
rapid mining swings each replay their ArmsSwing clip, and the death/
unconscious lying-pose rule is L0-locked.

## 1. Mechanism inventory

| # | Mechanism | Evidence |
|---|---|---|
| 1 | Death pose on a proxy: the owner's `Standing`/`Alive`/`Sleeping` flags ride the 20 Hz entity stream; `SessionStatePump` replays `ExperimentLayDown`/`ArmsLayDown` when `(!standing || !alive) && !sleeping` | SessionStatePump.cs:117-130; Body.Ragdoll sets `standing=false` (Body.cs:1713-1730); `PlayerEntity.ToEntityStateMsg` packs the flags |
| 2 | The local limb latches live in `Limb.BreakBone` / `MendBone` / `Dislocate` / `UnDislocate` / `Dismember` (falls, traps, bites, amputation, and the natural-heal calls inside `Limb.Update`) | Limb.cs:193-273, 518-522 |
| 3 | `Dismember` mutates MORE than the reported limb: lower limbs are deactivated + zeroed, connected limbs get skin/muscle/bleed/pain writes | Limb.cs:91-145 |
| 4 | The latch operations also mutate body state: BreakBone writes adrenaline + internalBleeding (head: possibly `Disfigure`), MendBone writes happiness, Dismember writes traumaAmount, Dislocate writes adrenaline | Limb.cs:221-243, 262-273, 91-145, 193-209 |
| 5 | Dedicated limb-latch channel: `LimbStateEventMsg` carries the owner's FULL post-event limb set + full body `CharacterHealthMsg`; `NetMsg.LimbStateEvent = 93` is bidirectional (guest → host report, host → guest relay with source excluded) | LimbStateEventMsg.cs; NetMsg.cs:155; LimbStateEventHandler.cs |
| 6 | Star relay + immediate host save merge mirror `EnemyBiteHandler`/`CharacterDataStore.ApplyEnemyBite`: host adopts the report, updates the saved character + fact table, relays | LimbStateEventHandler.cs; CharacterDataStore.cs:277-312 |
| 7 | The clone's `Limb.Update` is skipped (BodyPatches.LimbUpdatePatch), so the clone never simulates wounds/RNG and never heals/mends on its own — every limb visual must come from the synced state | BodyPatches.cs (LimbUpdatePatch); Limb.Update simulation (Limb.cs:498+) |
| 8 | The clone's limb visuals: broken bone sprite (`Limb.MakeBoneSprite` is private), dismembered `SetActive(false)`, `_SkinDamage`/`_MuscleDamage`/`_InfectionPercent`/`_SnowAmount`/`_Dirtyness`/`_Pain` shader params, `_BloodOverlay`/`_Wetness`, and the blood-drip particle threshold | CloneLimbRenderer.cs; LimbPresentation.cs; Limb.cs:250-259, 407-408, 445-476, 487-488, 501-506 |
| 9 | Continuous bleed presentation is the snapshot's legitimate channel: `furBloodAmount` evolves on the owner at 5 Hz and rides the 1 Hz character snapshot; the clone applies it, never simulates it | CharacterDataSync.CaptureCharacterData (Limb loop); CloneLimbRenderer.cs:118-126 |
| 10 | Mining swing clips: `Body.Attack` (Body.cs:1887) and `Body.ThrowItem` (Body.cs:1665) play the one-shot `ArmsSwing`; `AttackSwingState` holds `IsAttacking` for six stream ticks, so rapid swings inside one held window used to merge into one rising edge | BodyPatches.cs AttackPatch; BodyItemPatches.cs ThrowItemPatch; AttackSwingState.cs |
| 11 | Wire addition `EntityStateMsg.SwingSeq` (protobuf field 7) rolls per swing; the receiver replays on sequence change with the flag edge as the old-sender fallback | EntitySyncService.cs:58,130,138; EntityStateMsg.cs:41,59; SwingReplay.cs |
| 12 | Protocol compatibility: new message + new entity-state field mean a v15 peer could not see limb latches until the next snapshot and could not replay rapid swings — mixed-version sessions are refused | ProtocolVersion.cs:6 |

Whole-family audit: the five patched methods are the ONLY writers of
`broken` / `dislocated` / `dismembered` in the game assembly
(`grep "\.broken =|\.dislocated =|\.dismembered ="` over the decompiled
source). Splinted and blockedBleeding are deliberately not evented: splint is
a WoundView UI icon with no clone body visual (`SplintLimb.cs:14-25`,
`WoundView.cs:474`), and blockedBleeding only changes the owner's future bleed
evolution (the snapshot's `furBloodAmount` already carries its visual result).

## 2. Design

- **One latch operation = one full-terminal-state message.** The event carries
  every limb (index-stamped) plus the post-event `CharacterHealthMsg`, because
  Dismember changes several limbs in one call and every latch writes body
  fields in the same call. The host's saved-character merge and the clone fact
  table share `EnemyTerminalStateApplier.ApplyLimbState` (whole-set replace,
  exact rebuild).
- **Verified transitions only.** Each patch captures the latch in a prefix
  `out bool __state` and the postfix reports only a false→true / true→false
  edge. A repeated `BreakBone` on an already-broken limb refreshes
  `boneHealTimer` but is not a presentation latch edge and is not reported.
  Clones are excluded through the `RemoteBodyDriver` guard.
- **Clone limb renderer owns its own objects.** The replicated broken-bone
  sprite carries the new `RemoteCloneLimbRender` marker — separate from
  `RemoteCloneRender`, so the inventory renderer's worn-item cleanup can never
  destroy the wound sprite. The game's `MakeBoneSprite` is private and is
  replicated with the public surface (same sprite, sorting order, material).
- **Bleed is applied, not simulated.** The clone writes the synced
  `furBloodAmount` into `_BloodOverlay` and sets the drip emission exactly at
  the game's >0.95 threshold. The downward fur-blood transfer and underwater
  emission branches are owner-side continuous simulation; the snapshot's
  terminal per-limb value is the clone's authority.
- **Swing sequence, not only the flag edge.** `EntitySyncService` increments a
  rolling `SwingSeq` per verified swing and publishes it in every entity
  snapshot. The receiver replays on any sequence change — each rapid mining
  swing inside one held flag window gets its clip — and falls back to the
  held flag's rising edge for an old-version sender. The first snapshot only
  seeds the sequence so a historical swing is not replayed when a clone joins
  mid-world; a clone that appears mid-swing still replays on the flag edge.
- **Lying pose extracted.** `LyingPose` is the pure form of the pump's
  existing rule, so death/unconscious presentation is L0-locked without Unity.

## 3. Self-check table

| Mechanism | Change | Evidence |
|---|---|---|
| BreakBone | dedicated full-state event on verified false→true | LimbStatePatches.cs:25-31; LimbStateSyncTests |
| MendBone (manual + natural heal) | dedicated full-state event on verified true→false | LimbStatePatches.cs:34-40; Limb.cs:518-522 |
| Dislocate / UnDislocate | dedicated full-state event on verified transitions | LimbStatePatches.cs:43-58; Limb.cs:193-218 |
| Dismember (multi-limb) | full limb-set capture replaces the whole saved/fact-table set — lower limbs can never stay stale | CharacterDataSync.cs:125-143; EnemyTerminalStateApplier.ApplyLimbState |
| Body side effects of a latch | full post-event `CharacterHealthMsg` in the same message | LimbStateEventMsg.cs; CharacterDataSync.cs:125-131 |
| Host accept-first + relay | handler applies saved merge + fires adapter event + broadcasts except source | LimbStateEventHandler.cs; LimbStateSyncTests |
| Clone broken bone | replicated `MakeBoneSprite`, marker-isolated | CloneLimbRenderer.cs:97-135 |
| Clone dismembered | both-direction `SetActive` toggle | CloneLimbRenderer.cs:60-65; LimbPresentation.MustSetActive |
| Clone wound shading | all seven game shader params mirrored from snapshot/event | CloneLimbRenderer.cs:109-120; LimbPresentation.cs |
| Clone bleed drip | game's >0.95 threshold + rate 5 | CloneLimbRenderer.cs:124-129; LimbPresentation.BloodEmissionRate |
| Divergence monitor | broken/dismembered/dislocated snapshot changes without an event warn loudly | CloneFactTable.cs (limb latch block) |
| Death pose | pure `LyingPose` used by the pump, L0-locked | SessionStatePump.cs:117-130; LyingPoseTests |
| Rapid mining swings | per-swing `SwingSeq` + seeded first snapshot | EntitySyncService.cs:130-139; EntityStateMsg.cs; SwingReplay.cs; SwingReplayTests |
| Old-version swing sender | flag rising edge fallback stays | SwingReplay.cs:23-24; SwingReplayTests |
| Protocol | NetMsg 93 classified bidirectional; ProtocolVersion 15→16 | NetMsg.cs:155; DirectionTests; ProtocolVersion.cs:6 |
| Structure | all touched classes pass the 600-line gate | tools/check-architecture.ps1 |

## 4. Verification design

- **L0 wire/simulation**: `LimbStateSyncTests` — protobuf roundtrip, limb
  index 0 omission roundtrip, guest→host apply+relay, host→guests broadcast,
  guest-side relay apply, immediate full saved-character merge.
- **L0 pure machines**: `LyingPoseTests` (4) and `SwingReplayTests` (6) lock
  the pose rule, sequence replay, wrap, old-sender fallback, and first-snapshot
  seeding.
- **L0 terminal-state applier**: two new `EnemyTerminalStateApplierTests`
  rows lock whole-limb-set replacement + optional health replacement.
- **L0 entity-state roundtrip**: `SwingSeq` applies into `PlayerEntity` and
  roundtrips back to the wire.
- **L0 patch surface (reflective)**: `LimbStatePatchTests` — every limb-latch
  patch has the prefix `(Limb __instance, out bool __state)` / postfix
  `(Limb __instance, bool __state)` shape, all five contracts are in
  `PatchInventory`, the clone-limb shader/drip/active formulas mirror the
  decompiled formulas, `CloneLimbRenderer.ApplyCloneLimbs(Body, CharacterDataMsg)`
  exists, and `RemoteCloneLimbRender` is a field-less marker.
- **Static evidence**: the decompiled call-site inventory above (whole-family
  latch-writer search and the `Limb.cs` visual formulas).
- **Runtime evidence**: development-period rule — L0 simulation + static
  evidence + the real-game-dir deploy; **no manual acceptance**
  (user 2026-08-16 mandate).

## 5. Accepted residuals (recorded, not re-discovered)

- The clone's body-level `FacialExpression` latches (`Disfigured`,
  `EyeGone`, `BothEyesGone` and the owner's random `disfiguredIndex`) are
  carried by `CharacterHealthMsg` for save/restore, but the remote clone's
  face sprites remain template-driven in this cycle; disfigurement is a
  body-presentation follow-up, not a limb wound visual.
- The underwater bleed particle branch and the owner-side downward fur-blood
  transfer are not replicated on clones — the synced terminal `furBloodAmount`
  is applied directly (the transfer is simulation, not a latch).
- Mine-script press visuals remain the already-accepted local presentation gap
  (backlog); this cycle syncs the repeated `ArmsSwing` clips, not the mine
  press animation.
